using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Run.Ferrosand;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Ferrosand Magnetize (an extra rest site option): 3 random magnetizable cards in your deck (or every magnetizable card when
/// fewer are left) become Magnetic, with no choice. Disabled when no card can be magnetized. The pick uses the run seed, the
/// map location and the player's id, so every peer magnetizes the same cards. Runs on every peer through the synced rest site
/// choice.
/// </summary>
public sealed class MagnetizeRestSiteOption : RestSiteOption
{
    private readonly SmithRestSiteOption _vanillaAssets;
    private IReadOnlyList<CardModel> _magnetized = new List<CardModel>();
    private readonly List<CardChangePreview.Change> _changes = new();

    public MagnetizeRestSiteOption(Player owner)
        : base(owner)
    {
        _vanillaAssets = new SmithRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.MagnetizeId;

    public override IEnumerable<string> AssetPaths => _vanillaAssets.AssetPaths;

    private List<CardModel> EligibleCards => Owner.Deck.Cards.Where(MagneticModifier.CanMagnetize).ToList();

    public override bool IsEnabled => EligibleCards.Count > 0;

    public override LocString Description => IsEnabled
        ? new LocString("rest_site_ui", "OPTION_" + OptionId + ".description")
        : new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");

    public override Task<bool> OnSelect()
    {
        List<CardModel> eligible = EligibleCards;
        if (eligible.Count == 0)
        {
            return Task.FromResult(false);
        }

        IRunState runState = Owner.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, runState.CurrentRoom?.Id ?? 0);
        _magnetized = FerrosandRules
            .PickMagnetizeIndices(runState.Rng.Seed, location, Owner.NetId, eligible.Count, FerrosandRules.MagnetizeCount(eligible.Count))
            .Select(index => eligible[index])
            .ToList();
        _changes.Clear();
        foreach (CardModel card in _magnetized)
        {
            CardModel before = CardChangePreview.Snapshot(card);
            MagneticModifier.TryAdd(card);
            _changes.Add(new CardChangePreview.Change(before, CardChangePreview.Snapshot(card)));
        }

        return Task.FromResult(true);
    }

    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        // No choice was made, so show every magnetized card before and after.
        await CardChangePreview.Show(global::ZoneTheSpire.Run.Localization.ModLocalization.GameText("rest_site_ui", $"OPTION_{OptionId}.name"), _changes, ct);
    }

    public override Task DoRemotePostSelectVfx()
    {
        NRestSiteCharacter? character = NRestSiteRoom.Instance?.Characters.FirstOrDefault(c => c.Player == Owner);
        NCardSmithVfx? vfx = NCardSmithVfx.Create();
        if (vfx != null)
        {
            character?.AddChildSafely(vfx);
            vfx.Position = Vector2.Zero;
        }

        return Task.CompletedTask;
    }
}