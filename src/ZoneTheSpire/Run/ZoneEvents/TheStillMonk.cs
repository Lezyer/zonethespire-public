using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Devas;
using ZoneTheSpire.Run.Hoarfrost;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Zone event shared by Hoarfrost and Deva's Domain: a monk meditating in a snowdrift. Each player chooses on their own (see
/// StillMonkRules for the numbers and the balance notes):
/// <list type="bullet">
/// <item>Sit With Him: lose 15 HP; a chosen Attack or Skill gains Chakra 2 and Biting Cold 3 per energy it costs (the selection
/// screen shows the card as it will end up).</item>
/// <item>Share Your Fire: pay 90 Gold; 2 random upgradable Attacks or Skills (seeded per node and player) are upgraded and gain
/// Chakra 1, shown before and after.</item>
/// <item>Empty Your Pack: lose 5 Max HP; remove 2 chosen cards (the game's removal screen).</item>
/// <item>Walk On: nothing; offered only when the other three are all locked.</item>
/// </list>
/// Every change goes through the game's own commands and the mod's card modifiers, so it is saved and replicated normally.
/// </summary>
public sealed class TheStillMonk : ZoneEventModel
{
    private const string PortraitFile = "still_monk.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/still_monk.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new HoarfrostBiome().Id, new DevasDomainBiome().Id };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    internal static string SitPromptKey => ModelDb.GetId<TheStillMonk>().Entry + ".sitPrompt";

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool canSit = StillMonkRules.CanSit(Owner!.Creature.CurrentHp, Owner.Deck.Cards.Count(CanSitWith));
        bool canShare = StillMonkRules.CanShare(Owner.Gold, Shareable().Count);
        bool canEmptyPack = StillMonkRules.CanEmptyPack(Owner.Deck.Cards.Count(card => card.IsRemovable));

        var options = new List<EventOption>
        {
            canSit ? Option(SitWithHim, SitTips(), "INITIAL") : LockedOption("SIT_WITH_HIM_LOCKED"),
            canShare ? Option(ShareYourFire, ChakraTips(), "INITIAL") : LockedOption("SHARE_YOUR_FIRE_LOCKED"),
            canEmptyPack ? Option(EmptyYourPack, System.Array.Empty<IHoverTip>(), "INITIAL") : LockedOption("EMPTY_YOUR_PACK_LOCKED"),
        };
        if (StillMonkRules.ShowsWalkOn(canSit, canShare, canEmptyPack))
        {
            options.Add(Option(WalkOn, System.Array.Empty<IHoverTip>(), "INITIAL"));
        }

        return options;
    }

    /// <summary>Cards Sit With Him can take: an Attack or Skill that can carry both Chakra and Biting Cold.</summary>
    internal static bool CanSitWith(CardModel card) => ChakraModifier.CanHave(card) && BitingColdModifier.CanIce(card);

    /// <summary>Sit With Him's result on a card (also the selection screen's preview).</summary>
    internal static void ApplySit(CardModel card)
    {
        ChakraModifier.AddOrIncrease(card, StillMonkRules.SitChakra);
        BitingColdModifier.AddOrIncrease(card, StillMonkRules.SitCold(card.EnergyCost.GetResolved(), card.EnergyCost.CostsX));
    }

    private List<CardModel> Shareable() => Owner!.Deck.Cards.Where(card => ChakraModifier.CanHave(card) && card.IsUpgradable).ToList();

    private static IHoverTip[] ChakraTips()
    {
        var tips = new List<IHoverTip>();
        CardModifier.Get<ChakraModifier>().AddTips(tips);
        return tips.ToArray();
    }

    private static IHoverTip[] SitTips()
    {
        var tips = new List<IHoverTip>();
        CardModifier.Get<ChakraModifier>().AddTips(tips);
        CardModifier.Get<BitingColdModifier>().AddTips(tips);
        return tips.ToArray();
    }

    private async Task SitWithHim()
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, StillMonkRules.SitHpLoss, ValueProp.Unblockable | ValueProp.Unpowered, null, null);

        var prefs = new CardSelectorPrefs(new LocString("events", SitPromptKey), 1, 1)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };
        CardModel? card = (await CardSelectCmd.FromDeckGeneric(Owner, prefs, CanSitWith)).FirstOrDefault();
        if (card != null)
        {
            ApplySit(card);
        }

        SetEventFinished(PageDescription("SAT"));
    }

    private async Task ShareYourFire()
    {
        await PlayerCmd.LoseGold(StillMonkRules.ShareGold, Owner!, GoldLossType.Spent);

        List<CardModel> shareable = Shareable();
        IRunState runState = Owner.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
        var changes = new List<CardChangePreview.Change>();
        foreach (int index in StillMonkRules.PickShared(runState.Rng.Seed, location, Owner.NetId, shareable.Count))
        {
            CardModel card = shareable[index];
            CardModel before = CardChangePreview.Snapshot(card);
            CardCmd.Upgrade(card, CardPreviewStyle.None);
            ChakraModifier.AddOrIncrease(card, StillMonkRules.ShareChakra);
            changes.Add(new CardChangePreview.Change(before, CardChangePreview.Snapshot(card)));
        }

        if (LocalContext.IsMe(Owner) && changes.Count > 0)
        {
            // Local display only: never awaited, so the synced event doesn't wait on a click.
            _ = CardChangePreview.Show(global::ZoneTheSpire.Run.Localization.ModLocalization.GameText("events", $"{ModelDb.GetId<TheStillMonk>().Entry}.pages.INITIAL.options.SHARE_YOUR_FIRE.title"), changes);
        }

        SetEventFinished(PageDescription("SHARED"));
    }

    private async Task EmptyYourPack()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, StillMonkRules.PackCards, StillMonkRules.PackCards)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };
        List<CardModel> removed = (await CardSelectCmd.FromDeckForRemoval(Owner!, prefs)).ToList();
        await CardPileCmd.RemoveFromDeck(removed);
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature, StillMonkRules.PackMaxHpLoss, isFromCard: false);
        SetEventFinished(PageDescription("EMPTIED"));
    }

    private Task WalkOn()
    {
        SetEventFinished(PageDescription("WALKED_ON"));
        return Task.CompletedTask;
    }
}
