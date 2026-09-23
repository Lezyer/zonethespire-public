using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Wriggling;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Infestation event. Each player chooses on their own: a random relic for 12 HP, Wriggling (card reward rate, 4 x cost) on up
/// to 3 cards plus a permanent Infection status card in their deck, or removing a card. Every change goes through the game's
/// own commands, so it is saved and replicated normally.
/// </summary>
public sealed class WrithingPit : ZoneEventModel
{
    private const string PortraitFile = "writhing_pit.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/writhing_pit.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new InfestationBiome().Id };

    internal static string NestPromptKey => ModelDb.GetId<WrithingPit>().Entry + ".nestPrompt";

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    /// <summary>Cards Let Them Nest can give Wriggling to.</summary>
    internal static bool CanNest(CardModel card) => InfestationRules.CanNest(card.Keywords.Contains(CardKeyword.Unplayable));

    /// <summary>The Wriggling a card gains from Let Them Nest: the card reward rate (4 x cost, 4 for 0-cost and X-cost cards).</summary>
    internal static int NestAmount(CardModel card) =>
        WrigglingRules.RewardAmount(card.EnergyCost.GetResolved(), card.EnergyCost.CostsX);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption reachIn = InfestationRules.CanReachIn(Owner!.Creature.CurrentHp)
            ? Option(ReachIn)
            : LockedOption("REACH_IN_LOCKED");
        EventOption nest = Owner.Deck.Cards.Any(CanNest)
            ? Option(LetThemNest, HoverTipFactory.FromCardWithCardHoverTips<Infection>(), "INITIAL")
            : LockedOption("LET_THEM_NEST_LOCKED");
        EventOption burn = Owner.Deck.Cards.Any(card => card.IsRemovable)
            ? Option(BurnThePit)
            : LockedOption("BURN_THE_PIT_LOCKED");

        return new[] { reachIn, nest, burn };
    }

    private async Task ReachIn()
    {
        RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner!).ToMutable();
        await RelicCmd.Obtain(relic, Owner!);
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            InfestationRules.ReachInHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
        SetEventFinished(PageDescription("REACHED_IN"));
    }

    private async Task LetThemNest()
    {
        var prefs = new CardSelectorPrefs(new LocString("events", NestPromptKey), 1, InfestationRules.NestMaxCards)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };

        List<CardModel> cards = (await CardSelectCmd.FromDeckGeneric(Owner!, prefs, CanNest)).ToList();
        foreach (CardModel card in cards)
        {
            WrigglingModifier.AddOrIncrease(card, NestAmount(card));
        }

        CardModel infection = Owner!.RunState.CreateCard<Infection>(Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(infection, PileType.Deck), 2f);
        SetEventFinished(PageDescription("NESTED"));
    }

    private async Task BurnThePit()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1);
        List<CardModel> removed = (await CardSelectCmd.FromDeckForRemoval(Owner!, prefs)).ToList();
        await CardPileCmd.RemoveFromDeck(removed);
        SetEventFinished(PageDescription("BURNED"));
    }
}
