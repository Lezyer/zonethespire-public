using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Hallowed;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Blinding Hallows event: you kneel before a golden scale and a vast eye that demands you speak. Each player chooses on their
/// own (see TribunalRules for the numbers):
/// <list type="bullet">
/// <item>Confess: lose 10 HP; every card loses Blasphemous and Redemption (Hallowing stays); gain 2 Max HP per Redemption lost.</item>
/// <item>Deny: every card that isn't upgraded is upgraded (the game's grid preview), and each also gains Blasphemous unless it
/// has Redemption.</item>
/// <item>Devote: add Inquisitor's Wrath to the deck, then 2 random upgraded cards (seeded per node and player) are downgraded,
/// shown before and after.</item>
/// </list>
/// No leave option: choosing is the point, and Deny or Devote is always open when Confess is locked. Every change goes through
/// the game's own commands and card modifiers, so it is saved and replicated normally.
/// </summary>
public sealed class TheSacredTribunal : ZoneEventModel
{
    private const string PortraitFile = "sacred_tribunal.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/sacred_tribunal.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new HallowedBiome().Id };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption confess = TribunalRules.CanConfess(Owner!.Creature.CurrentHp) ? ConfessOption() : LockedOption("CONFESS_LOCKED");
        EventOption deny = TribunalRules.CanDeny(DeniableCards().Count) ? Option(Deny, ModifierTips(), "INITIAL") : LockedOption("DENY_LOCKED");
        EventOption devote = TribunalRules.CanDevote(UpgradedCards().Count)
            ? Option(Devote, HoverTipFactory.FromCardWithCardHoverTips<InquisitorsWrath>().ToArray(), "INITIAL")
            : LockedOption("DEVOTE_LOCKED");
        return new[] { confess, deny, devote };
    }

    /// <summary>The Confess option, naming the Max HP it really gives (or leaving it out when no card has Redemption).</summary>
    private EventOption ConfessOption()
    {
        int maxHp = TribunalRules.ConfessMaxHp(Owner!.Deck.Cards.Count(RedemptionModifier.Has));
        // Option loc keys are stored exactly as EventOptionLoc declares them (upper snake case), under the event's model id.
        string key = maxHp > 0 ? "CONFESS" : "CONFESS_NOTHING";
        var description = new LocString("events", $"{ModelDb.GetId<TheSacredTribunal>().Entry}.pages.INITIAL.options.{key}.description");
        description.Add("MaxHp", maxHp);
        var title = new LocString("events", $"{ModelDb.GetId<TheSacredTribunal>().Entry}.pages.INITIAL.options.{key}.title");
        return Option(Confess, title, description, ModifierTips());
    }

    private static IHoverTip[] ModifierTips()
    {
        var tips = new List<IHoverTip>();
        CardModifier.Get<BlasphemousModifier>().AddTips(tips);
        CardModifier.Get<RedemptionModifier>().AddTips(tips);
        return tips.ToArray();
    }

    /// <summary>Cards Deny upgrades: those not upgraded yet that can be.</summary>
    private List<CardModel> DeniableCards() => Owner!.Deck.Cards.Where(card => !card.IsUpgraded && card.IsUpgradable).ToList();

    private List<CardModel> UpgradedCards() => Owner!.Deck.Cards.Where(card => card.IsUpgraded).ToList();

    private async Task Confess()
    {
        await CreatureCmd.Damage(
            new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            TribunalRules.ConfessHpCost,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

        int redemptions = 0;
        int blasphemies = 0;
        foreach (CardModel card in Owner.Deck.Cards.ToList())
        {
            redemptions += RedemptionModifier.TryRemove(card) ? 1 : 0;
            blasphemies += BlasphemousModifier.TryRemove(card) ? 1 : 0;
        }

        int maxHp = TribunalRules.ConfessMaxHp(redemptions);
        if (maxHp > 0)
        {
            await CreatureCmd.GainMaxHp(Owner.Creature, maxHp);
        }

        SetEventFinished(PageDescription("CONFESSED"));
    }

    private Task Deny()
    {
        List<CardModel> cards = DeniableCards();
        int blasphemed = 0;
        foreach (CardModel card in cards)
        {
            // Before the upgrade, so the game's upgrade preview shows the card as it ends up.
            if (TribunalRules.DenyBlasphemes(upgradedByDeny: true, RedemptionModifier.Has(card), BlasphemousModifier.CanBlaspheme(card))
                && BlasphemousModifier.TryAdd(card))
            {
                blasphemed++;
            }
        }

        CardCmd.Upgrade(cards, CardPreviewStyle.GridLayout);
        SetEventFinished(PageDescription("DENIED"));
        return Task.CompletedTask;
    }

    private async Task Devote()
    {
        List<CardModel> upgraded = UpgradedCards();
        IRunState runState = Owner!.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
        var changes = new List<CardChangePreview.Change>();
        foreach (int index in TribunalRules.PickDowngrades(runState.Rng.Seed, location, Owner.NetId, upgraded.Count))
        {
            CardModel card = upgraded[index];
            CardModel before = CardChangePreview.Snapshot(card);
            CardCmd.Downgrade(card);
            changes.Add(new CardChangePreview.Change(before, CardChangePreview.Snapshot(card)));
        }

        CardModel wrath = runState.CreateCard<InquisitorsWrath>(Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(wrath, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
        if (LocalContext.IsMe(Owner) && changes.Count > 0)
        {
            // Local display only: never awaited, so the synced event doesn't wait on a click.
            _ = CardChangePreview.Show(global::ZoneTheSpire.Run.Localization.ModLocalization.GameText("events", $"{ModelDb.GetId<TheSacredTribunal>().Entry}.pages.INITIAL.options.DEVOTE.title"), changes);
        }

        SetEventFinished(PageDescription("DEVOTED"));
    }
}
