using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rewards;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Phantasmal Tombs and Mirrorlands event. Each player chooses on their own: inherit a relic, a potion and 50 gold with Doubt,
/// gain 20 Max HP and drop to 1 HP (Max HP first, since gaining Max HP also heals; HP is then set directly, which isn't HP
/// loss), or remove a random card and then upgrade 2 random cards (removal first, so an upgraded card is never removed).
/// Random picks use the event's own RNG over the deck order, so every peer changes the same cards.
/// </summary>
public sealed class YourOwnFuneral : ZoneEventModel
{
    private const string PortraitFile = "your_own_funeral.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/your_own_funeral.png";

    private static readonly IReadOnlyList<string> Zones = new[]
    {
        new PhantasmalTombsBiome().Id,
        new MirrorlandsBiome().Id,
    };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption eulogy = Owner!.Deck.Cards.Any(card => card.IsRemovable)
            ? Option(GiveAEulogy)
            : LockedOption("GIVE_A_EULOGY_LOCKED");

        return new[]
        {
            Option(ReadTheWill, HoverTipFactory.FromCardWithCardHoverTips<Doubt>(), "INITIAL"),
            Option(ClimbIn),
            eulogy,
        };
    }

    private async Task ReadTheWill()
    {
        RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner!).ToMutable();
        await RelicCmd.Obtain(relic, Owner!);
        await PlayerCmd.GainGold(FuneralRules.WillGold, Owner!);
        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Doubt>() }, Owner!);
        await RewardsCmd.OfferCustom(Owner!, new List<Reward> { new PotionReward(Owner!) });
        SetEventFinished(PageDescription("READ_THE_WILL"));
    }

    private async Task ClimbIn()
    {
        // Max HP first: gaining Max HP also heals, which would undo the HP drop if it came second.
        await CreatureCmd.GainMaxHp(Owner!.Creature, FuneralRules.ClimbInMaxHp);
        await CreatureCmd.SetCurrentHp(Owner!.Creature, FuneralRules.ClimbInHp);
        SetEventFinished(PageDescription("CLIMBED_IN"));
    }

    private async Task GiveAEulogy()
    {
        // Remove first, so a card upgraded below can never be the one removed.
        List<CardModel> removable = Owner!.Deck.Cards.Where(card => card.IsRemovable).ToList();
        if (removable.Count > 0)
        {
            CardModel removed = removable[Rng.NextInt(removable.Count)];
            await CardPileCmd.RemoveFromDeck(removed);
        }

        List<CardModel> upgradable = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        int count = FuneralRules.EulogyUpgradeCount(upgradable.Count);
        for (int i = 0; i < count; i++)
        {
            CardModel card = upgradable[Rng.NextInt(upgradable.Count)];
            upgradable.Remove(card);
            CardCmd.Upgrade(card);
        }

        SetEventFinished(PageDescription("EULOGY"));
    }
}
