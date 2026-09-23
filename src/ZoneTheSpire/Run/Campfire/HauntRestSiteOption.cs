using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Phantasmal;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Phantasmal Tombs Haunt: 3 random cards in the deck (or every hauntable card when fewer are left) become Phantasm-Haunted,
/// then lose 5 HP. Shown disabled with 5 HP or less or no hauntable card. The pick uses the run seed, the map location and the
/// player's id, so every peer haunts the same cards. Runs on every peer through the synced rest site choice.
/// </summary>
public sealed class HauntRestSiteOption : RestSiteOption
{
    private readonly HealRestSiteOption _vanillaAssets;
    private IReadOnlyList<CardModel> _selection = new List<CardModel>();
    private readonly List<CardChangePreview.Change> _changes = new();

    public HauntRestSiteOption(Player owner)
        : base(owner)
    {
        _vanillaAssets = new HealRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.HauntId;

    public override IEnumerable<string> AssetPaths => _vanillaAssets.AssetPaths;

    public override bool IsEnabled => PhantasmalRules.CanPerformHaunt(Owner.Creature.CurrentHp, EligibleCards.Count);

    public override LocString Description => IsEnabled
        ? new LocString("rest_site_ui", "OPTION_" + OptionId + ".description")
        : new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");

    public override async Task<bool> OnSelect()
    {
        List<CardModel> eligible = EligibleCards;
        int count = PhantasmalRules.HauntSelectCount(eligible.Count);
        if (!PhantasmalRules.CanPerformHaunt(Owner.Creature.CurrentHp, count))
        {
            return false;
        }

        IRunState runState = Owner.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, runState.CurrentRoom?.Id ?? 0);
        _selection = PhantasmalRules
            .PickHauntIndices(runState.Rng.Seed, location, Owner.NetId, eligible.Count, count)
            .Select(index => eligible[index])
            .ToList();
        _changes.Clear();
        foreach (CardModel card in _selection)
        {
            CardModel before = CardChangePreview.Snapshot(card);
            PhantasmHauntedModifier.TryAdd(card);
            _changes.Add(new CardChangePreview.Change(before, CardChangePreview.Snapshot(card)));
        }

        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner.Creature, PhantasmalRules.HauntHpCost, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        return true;
    }

    private List<CardModel> EligibleCards => Owner.Deck.Cards.Where(PhantasmHauntedModifier.CanHaunt).ToList();

    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        // No choice was made, so show every haunted card before and after.
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
