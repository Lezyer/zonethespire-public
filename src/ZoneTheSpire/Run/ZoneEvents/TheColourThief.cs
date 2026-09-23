using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Prismatic;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Prismatic;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Prismatic Storm event. Each player chooses on their own: transform every Strike and Defend into naturally 0-cost cards from
/// other characters (picked with the event's own RNG, seeded per player from the run seed like vanilla events, so every peer
/// gets the same cards), snatch Colour Thief's Bundle for 8 HP, or take the Colour Thief card. Every change goes through the
/// game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class TheColourThief : ZoneEventModel
{
    private const string PortraitFile = "colour_thief_event.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/colour_thief_event.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new PrismaticStormBiome().Id };

    private static string TradeInfoKey => ModelDb.GetId<TheColourThief>().Entry + ".tradeInfo";

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    private static bool CanTrade(CardModel card) => card.IsBasicStrikeOrDefend && card.IsRemovable;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption trade = Owner!.Deck.Cards.Any(CanTrade)
            ? Option(TradeColours)
            : LockedOption("TRADE_COLOURS_LOCKED");
        EventOption snatch = PrismaticRules.CanSnatch(Owner.Creature.CurrentHp)
            ? Option(SnatchTheBundle, HoverTipFactory.FromRelic<ColourThiefsBundle>(), "INITIAL")
            : LockedOption("SNATCH_THE_BUNDLE_LOCKED");

        return new[]
        {
            trade,
            snatch,
            Option(PatItsHead, HoverTipFactory.FromCardWithCardHoverTips<ColourThief>(), "INITIAL"),
        };
    }

    private async Task TradeColours()
    {
        List<CardModel> originals = Owner!.Deck.Cards.Where(CanTrade).ToList();
        List<CardModel> options = OtherCharacterCards.NaturallyFree(Owner).ToList();
        if (originals.Count > 0 && options.Count > 0)
        {
            List<CardTransformation> transformations = originals
                .Select(card => new CardTransformation(card, CardFactory.CreateRandomCardForTransform(card, options, isInCombat: false, Rng)))
                .ToList();
            List<CardPileAddResult> results = (await CardCmd.Transform(transformations, null, CardPreviewStyle.None)).ToList();
            if (results.Count > 0 && LocalContext.IsMe(Owner))
            {
                NSimpleCardsViewScreen.ShowScreen(results, new LocString("events", TradeInfoKey));
            }
        }

        SetEventFinished(PageDescription("TRADED"));
    }

    private async Task SnatchTheBundle()
    {
        await RelicCmd.Obtain(ModelDb.Relic<ColourThiefsBundle>().ToMutable(), Owner!);
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            PrismaticRules.SnatchHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
        SetEventFinished(PageDescription("SNATCHED"));
    }

    private async Task PatItsHead()
    {
        CardModel card = Owner!.RunState.CreateCard<ColourThief>(Owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 2f);
        SetEventFinished(PageDescription("PATTED"));
    }
}
