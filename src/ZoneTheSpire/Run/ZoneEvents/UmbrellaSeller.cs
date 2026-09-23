using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.BloodRain;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Blood Rain event. Each player chooses on their own: pay 150 gold for Black Umbrella, lose 10 Max HP for Jar of Blood, or
/// heal 15 HP. Every change goes through the game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class UmbrellaSeller : ZoneEventModel
{
    private const string PortraitFile = "umbrella_seller.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/umbrella_seller.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new BloodRainBiome().Id };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new HealVar(BloodRainRules.ShrugHeal),
    };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption buy = BloodRainRules.CanBuyUmbrella(Owner!.Gold)
            ? Option(BuyAnUmbrella, HoverTipFactory.FromRelic<BlackUmbrella>(), "INITIAL")
            : LockedOption("BUY_AN_UMBRELLA_LOCKED");
        EventOption bottle = BloodRainRules.CanBottle(Owner.Creature.MaxHp)
            ? Option(BottleYourBlood, HoverTipFactory.FromRelic<JarOfBlood>(), "INITIAL")
            : LockedOption("BOTTLE_YOUR_BLOOD_LOCKED");

        return new[] { buy, bottle, Option(ShrugOffTheRain) };
    }

    private async Task BuyAnUmbrella()
    {
        await PlayerCmd.LoseGold(BloodRainRules.UmbrellaGoldCost, Owner!, GoldLossType.Spent);
        await RelicCmd.Obtain(ModelDb.Relic<BlackUmbrella>().ToMutable(), Owner!);
        SetEventFinished(PageDescription("BOUGHT"));
    }

    private async Task BottleYourBlood()
    {
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature, BloodRainRules.BottleMaxHpLoss, isFromCard: false);
        await RelicCmd.Obtain(ModelDb.Relic<JarOfBlood>().ToMutable(), Owner!);
        SetEventFinished(PageDescription("BOTTLED"));
    }

    private async Task ShrugOffTheRain()
    {
        await CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.IntValue);
        SetEventFinished(PageDescription("SHRUGGED"));
    }
}
