using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Scrapyard;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Scrapyard;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Scrapyard event. Each player chooses on their own: bury it (remove up to 2 cards for 11 HP), reboot it (take the
/// Scrapyard Automaton curse, which becomes Automaton Form after 3 combats), or strip it for parts (transform a card). The
/// transform uses the event's own RNG (seeded per player from the run seed, like vanilla events), so every
/// peer gets the same card.
/// </summary>
public sealed class BuriedAutomaton : ZoneEventModel
{
    private const string PortraitFile = "buried_automaton.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/buried_automaton.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new ScrapyardBiome().Id };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        int removable = Owner!.Deck.Cards.Count(card => card.IsRemovable);
        EventOption bury = ScrapyardRules.CanBury(Owner.Creature.CurrentHp, removable)
            ? Option(BuryIt)
            : LockedOption("BURY_IT_LOCKED");
        EventOption strip = Owner.Deck.Cards.Any(card => card.Type != CardType.Quest && card.IsTransformable)
            ? Option(StripItForParts)
            : LockedOption("STRIP_IT_FOR_PARTS_LOCKED");

        return new[]
        {
            bury,
            Option(RebootIt, HoverTipFactory.FromCardWithCardHoverTips<ScrapyardAutomaton>(), "INITIAL"),
            strip,
        };
    }

    private async Task BuryIt()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1, ScrapyardRules.BuryMaxCards)
        {
            RequireManualConfirmation = true,
        };

        List<CardModel> removed = (await CardSelectCmd.FromDeckForRemoval(Owner!, prefs)).ToList();
        await CardPileCmd.RemoveFromDeck(removed);
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            ScrapyardRules.BuryHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
        SetEventFinished(PageDescription("BURIED"));
    }

    private async Task RebootIt()
    {
        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<ScrapyardAutomaton>() }, Owner!);
        SetEventFinished(PageDescription("REBOOTED"));
    }

    private async Task StripItForParts()
    {
        CardModel? card = (await CardSelectCmd.FromDeckForTransformation(Owner!, new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, 1))).FirstOrDefault();
        if (card != null)
        {
            await CardCmd.TransformToRandom(card, Rng, CardPreviewStyle.EventLayout);
        }

        SetEventFinished(PageDescription("STRIPPED"));
    }
}
