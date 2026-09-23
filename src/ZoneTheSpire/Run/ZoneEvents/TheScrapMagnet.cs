using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Ferrosand;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Scrapyard and Ferrosand event: a junk crane's electromagnet. Each player chooses on their own: make 2 random cards Magnetic,
/// remove a card and gain 40 gold, or shake it loose (a random potion; 50%: also a random relic, otherwise lose 5 HP).
/// Random picks and the 50% roll use the event's own RNG over the deck order, so every peer gets the same result. Every change
/// goes through the game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class TheScrapMagnet : ZoneEventModel
{
    private const string PortraitFile = "the_scrap_magnet.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/the_scrap_magnet.png";

    private static readonly IReadOnlyList<string> Zones = new[]
    {
        new ScrapyardBiome().Id,
        new FerrosandBiome().Id,
    };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption lever = Owner!.Deck.Cards.Any(MagneticModifier.CanMagnetize)
            ? Option(PullTheLever)
            : LockedOption("PULL_THE_LEVER_LOCKED");
        EventOption throwScrap = Owner.Deck.Cards.Any(card => card.IsRemovable)
            ? Option(ThrowScrapAtIt)
            : LockedOption("THROW_SCRAP_AT_IT_LOCKED");
        EventOption shake = ScrapMagnetRules.CanShake(Owner.Creature.CurrentHp)
            ? Option(ShakeItLoose)
            : LockedOption("SHAKE_IT_LOOSE_LOCKED");

        return new[] { lever, throwScrap, shake };
    }

    private Task PullTheLever()
    {
        List<CardModel> eligible = Owner!.Deck.Cards.Where(MagneticModifier.CanMagnetize).ToList();
        int count = ScrapMagnetRules.LeverMagnetizeCountFor(eligible.Count);
        for (int i = 0; i < count; i++)
        {
            CardModel card = eligible[Rng.NextInt(eligible.Count)];
            eligible.Remove(card);
            MagneticModifier.TryAdd(card);
        }

        SetEventFinished(PageDescription("PULLED"));
        return Task.CompletedTask;
    }

    private async Task ThrowScrapAtIt()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1);
        List<CardModel> removed = (await CardSelectCmd.FromDeckForRemoval(Owner!, prefs)).ToList();
        await CardPileCmd.RemoveFromDeck(removed);
        await PlayerCmd.GainGold(ScrapMagnetRules.ThrowScrapGold, Owner!);
        SetEventFinished(PageDescription("THROWN"));
    }

    private async Task ShakeItLoose()
    {
        bool lucky = ScrapMagnetRules.ShakeFindsRelic(Rng.NextInt(100));
        if (lucky)
        {
            RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner!).ToMutable();
            await RelicCmd.Obtain(relic, Owner!);
        }
        else
        {
            await CreatureCmd.Damage(
                new ThrowingPlayerChoiceContext(),
                Owner!.Creature,
                ScrapMagnetRules.ShakeHpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                null,
                null);
        }

        await RewardsCmd.OfferCustom(Owner!, new List<Reward> { new PotionReward(Owner!) });
        SetEventFinished(PageDescription(lucky ? "SHAKEN_LUCKY" : "SHAKEN_HIT"));
    }
}
