using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Hallowed;

namespace ZoneTheSpire.Run.Campfire;

/// <summary>
/// Blinding Hallows Repent (extra rest site option): every removable curse leaves the deck (Eternal ones stay), up to 3 random
/// attacks without Hallowing gain it, then lose 10 HP (unblockable). Needs more than 10 HP and something to do. Seeded by run,
/// location and player, so every peer repents the same way. Runs on every peer through the synced rest site choice.
/// </summary>
public sealed class RepentRestSiteOption : RestSiteOption
{
    private readonly HealRestSiteOption _vanillaAssets;
    private IReadOnlyList<CardModel> _hallowed = new List<CardModel>();
    private readonly List<CardChangePreview.Change> _changes = new();

    public RepentRestSiteOption(Player owner)
        : base(owner)
    {
        _vanillaAssets = new HealRestSiteOption(owner);
        RestSiteLoc.EnsureInjected();
    }

    public override string OptionId => RestSiteLoc.RepentId;

    public override IEnumerable<string> AssetPaths => _vanillaAssets.AssetPaths;

    private List<CardModel> RemovableCurses => Owner.Deck.Cards.Where(card => card.Type == CardType.Curse && card.IsRemovable).ToList();

    private List<CardModel> FreshAttacks => Owner.Deck.Cards.Where(card => HallowingModifier.CanHallow(card) && !HallowingModifier.Has(card)).ToList();

    public override bool IsEnabled => HallowedRules.CanRepent(Owner.Creature.CurrentHp) && (RemovableCurses.Count > 0 || FreshAttacks.Count > 0);

    public override LocString Description => IsEnabled
        ? new LocString("rest_site_ui", "OPTION_" + OptionId + ".description")
        : new LocString("rest_site_ui", "OPTION_" + OptionId + ".descriptionDisabled");

    public override async Task<bool> OnSelect()
    {
        if (!IsEnabled)
        {
            return false;
        }

        List<CardModel> fresh = FreshAttacks;
        IRunState runState = Owner.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, runState.CurrentRoom?.Id ?? 0);
        _hallowed = HallowedRules
            .PickIndices(runState.Rng.Seed, HallowedRules.RepentStream(location), Owner.NetId, fresh.Count, HallowedRules.RepentAttackCount)
            .Select(index => fresh[index])
            .ToList();
        _changes.Clear();
        foreach (CardModel card in _hallowed)
        {
            CardModel before = CardChangePreview.Snapshot(card);
            HallowingModifier.TryAdd(card);
            _changes.Add(new CardChangePreview.Change(before, CardChangePreview.Snapshot(card)));
        }

        List<CardModel> curses = RemovableCurses;
        foreach (CardModel curse in curses)
        {
            _changes.Add(new CardChangePreview.Change(CardChangePreview.Snapshot(curse), null));
            await CardPileCmd.RemoveFromDeck(curse);
        }

        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner.Creature, HallowedRules.RepentHpCost, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        return true;
    }

    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        // No choice was made, so show every hallowed attack before and after, and every removed curse.
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
