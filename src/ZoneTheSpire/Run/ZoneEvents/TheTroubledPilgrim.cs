using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Devas;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Deva's Domain event: a pilgrim who has done everything right and still can't let go of the world. Each player chooses on
/// their own: tell her it doesn't matter (2 random cards gain Chakra 2, Powers included, preferring cards that cost something),
/// that it does (obtain Worldly Attachment, and an Eternal Doubt with it), or that it is all that matters (lose every point of Chakra in the deck for 50 gold
/// each; the option shows the exact gold). The random picks use the event's own RNG over the deck order, so every peer gets the
/// same result. Every change goes through the game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class TheTroubledPilgrim : ZoneEventModel
{
    private const string PortraitFile = "the_troubled_pilgrim.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/the_troubled_pilgrim.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new DevasDomainBiome().Id };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new GoldVar(0) };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    /// <summary>The gold option shows what this player's own Chakra is worth.</summary>
    public override void CalculateVars()
    {
        DynamicVars.Gold.BaseValue = PilgrimRules.GoldFor(ChakraAmounts());
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption give = Owner!.Deck.Cards.Any(ChakraModifier.CanHaveAny)
            ? Option(DoesntMatter)
            : LockedOption("DOESNT_MATTER_LOCKED");
        EventOption keep = Option(DoesMatter, HoverTipFactory.FromRelic<WorldlyAttachment>().Concat(HoverTipFactory.FromCardWithCardHoverTips<Doubt>()).ToList(), "INITIAL");
        EventOption sell = ChakraAmounts().Any(amount => amount > 0)
            ? Option(AllThatMatters)
            : LockedOption("ALL_THAT_MATTERS_LOCKED");

        return new[] { give, keep, sell };
    }

    private IEnumerable<int> ChakraAmounts() => Owner!.Deck.Cards.Select(ChakraModifier.AmountOf);

    /// <summary>Chakra 2 on 2 random cards. 0-cost cards are only picked once there is nothing else left.</summary>
    private Task DoesntMatter()
    {
        List<CardModel> costing = Owner!.Deck.Cards
            .Where(card => ChakraModifier.CanHaveAny(card) && (card.EnergyCost.CostsX || card.EnergyCost.GetResolved() > 0))
            .ToList();
        List<CardModel> free = Owner.Deck.Cards
            .Where(card => ChakraModifier.CanHaveAny(card) && !costing.Contains(card))
            .ToList();

        int count = PilgrimRules.GiftCountFor(costing.Count + free.Count);
        for (int i = 0; i < count; i++)
        {
            List<CardModel> pool = PilgrimRules.DrawsFromCostingCards(costing.Count) ? costing : free;
            CardModel card = pool[Rng.NextInt(pool.Count)];
            pool.Remove(card);
            ChakraModifier.AddOrIncrease(card, PilgrimRules.ChakraGiftAmount);
        }

        SetEventFinished(PageDescription("GAVE"));
        return Task.CompletedTask;
    }

    /// <summary>The relic, and the doubt that comes with holding on: a Doubt that can never be removed.</summary>
    private async Task DoesMatter()
    {
        await RelicCmd.Obtain(ModelDb.Relic<WorldlyAttachment>().ToMutable(), Owner!);
        foreach (CardPileAddResult result in await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Doubt>() }, Owner!))
        {
            if (result.success && result.cardAdded is { } doubt)
            {
                EternalModifier.Apply(doubt);
            }
        }

        SetEventFinished(PageDescription("KEPT"));
    }

    /// <summary>Every card gives up its Chakra; only positive Chakra pays, but negative Chakra goes too.</summary>
    private async Task AllThatMatters()
    {
        int gold = PilgrimRules.GoldFor(ChakraAmounts());
        foreach (CardModel card in Owner!.Deck.Cards.ToList())
        {
            ChakraModifier.Remove(card);
        }

        await PlayerCmd.GainGold(gold, Owner);
        SetEventFinished(PageDescription("SOLD"));
    }
}
