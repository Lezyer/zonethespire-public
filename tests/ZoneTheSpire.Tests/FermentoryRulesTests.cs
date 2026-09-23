using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Fermentory;

namespace ZoneTheSpire.Tests;

public class FermentoryRulesTests
{
    [Fact]
    public void EffectPool_HasNineEffectsSolo_AndDistributionOnlyInMultiplayer()
    {
        Assert.Equal(9, FermentoryRules.EffectPool(multiplayer: false).Count);
        Assert.DoesNotContain(SlotEffect.DistributionSystem, FermentoryRules.EffectPool(multiplayer: false));
        Assert.Equal(Enum.GetValues<SlotEffect>(), FermentoryRules.EffectPool(multiplayer: true));
    }

    [Fact]
    public void RollEffect_IsSeeded_AndEvenOverTheWholePool()
    {
        Assert.Equal(
            FermentoryRules.RollEffect(7UL, "loc", 1UL, 3, multiplayer: true),
            FermentoryRules.RollEffect(7UL, "loc", 1UL, 3, multiplayer: true));

        var counts = Enum.GetValues<SlotEffect>().ToDictionary(effect => effect, _ => 0);
        for (int roll = 0; roll < 2000; roll++)
        {
            counts[FermentoryRules.RollEffect(7UL, "loc:" + roll, 1UL, 0, multiplayer: true)]++;
        }

        Assert.All(counts.Values, count => Assert.InRange(count, 150, 250));

        for (int roll = 0; roll < 500; roll++)
        {
            Assert.NotEqual(SlotEffect.DistributionSystem, FermentoryRules.RollEffect(7UL, "solo:" + roll, 1UL, 0, multiplayer: false));
        }
    }

    [Fact]
    public void NextSpecialSlot_IsTheLeftmostNormalSlot_OrNullWhenAllAreSpecial()
    {
        Assert.Equal(0, FermentoryRules.NextSpecialSlot(3, new Dictionary<int, SlotEffect>()));
        Assert.Equal(1, FermentoryRules.NextSpecialSlot(3, new Dictionary<int, SlotEffect> { [0] = SlotEffect.HealingBalm }));
        Assert.Equal(0, FermentoryRules.NextSpecialSlot(3, new Dictionary<int, SlotEffect> { [1] = SlotEffect.HealingBalm }));
        Assert.Null(FermentoryRules.NextSpecialSlot(2, new Dictionary<int, SlotEffect> { [0] = SlotEffect.HealingBalm, [1] = SlotEffect.WardingFlask }));
        Assert.Null(FermentoryRules.NextSpecialSlot(0, new Dictionary<int, SlotEffect>()));
    }

    [Fact]
    public void EncodeDecode_RoundTrips_AndSkipsUnknownEffects()
    {
        var slots = new Dictionary<ulong, IReadOnlyDictionary<int, SlotEffect>>
        {
            [1UL] = new Dictionary<int, SlotEffect> { [0] = SlotEffect.DuplicatingSolution, [2] = SlotEffect.RefillingStill },
            [42UL] = new Dictionary<int, SlotEffect> { [1] = SlotEffect.HealingBalm },
        };

        string text = FermentoryRules.Encode(slots);
        Assert.Equal("1:0=DuplicatingSolution,2=RefillingStill;42:1=HealingBalm", text);
        var decoded = FermentoryRules.Decode(text);
        Assert.Equal(SlotEffect.RefillingStill, decoded[1UL][2]);
        Assert.Equal(SlotEffect.HealingBalm, decoded[42UL][1]);

        var partial = FermentoryRules.Decode("5:0=NoSuchEffect,1=WardingFlask;garbage");
        Assert.Equal(new[] { 1 }, partial[5UL].Keys.ToArray());
        Assert.Empty(FermentoryRules.Decode(""));
    }

    [Fact]
    public void Brews_AlternatesByEnemyAndRound()
    {
        Assert.True(FermentoryRules.Brews(0, 1));
        Assert.False(FermentoryRules.Brews(0, 2));
        Assert.False(FermentoryRules.Brews(1, 1));
        Assert.True(FermentoryRules.Brews(1, 2));
        Assert.True(FermentoryRules.Brews(2, 3));
    }

    [Fact]
    public void PickEnemyPotion_IsSeeded_AndCoversEveryPotion()
    {
        Assert.Equal(
            FermentoryRules.PickEnemyPotion(7UL, "loc", "enemy0", 3),
            FermentoryRules.PickEnemyPotion(7UL, "loc", "enemy0", 3));
        var seen = Enumerable.Range(0, 400).Select(round => FermentoryRules.PickEnemyPotion(7UL, "loc", "enemy0", round)).ToHashSet();
        Assert.Equal(Enum.GetValues<EnemyPotion>().Length, seen.Count);
    }

    [Theory]
    [InlineData(EnemyPotion.Strength, 2, 3, 4)]
    [InlineData(EnemyPotion.LiquidBronze, 3, 5, 6)]
    [InlineData(EnemyPotion.Weak, 1, 1, 1)]
    [InlineData(EnemyPotion.Vulnerable, 1, 1, 1)]
    [InlineData(EnemyPotion.Poison, 5, 8, 10)]
    [InlineData(EnemyPotion.Fire, 8, 12, 16)]
    [InlineData(EnemyPotion.Blood, 15, 15, 15)]
    public void Amount_ScalesByAct(EnemyPotion potion, int act1, int act2, int act3)
    {
        Assert.Equal(act1, FermentoryRules.Amount(potion, 0, 1));
        Assert.Equal(act2, FermentoryRules.Amount(potion, 1, 1));
        Assert.Equal(act3, FermentoryRules.Amount(potion, 2, 1));
        Assert.Equal(act3, FermentoryRules.Amount(potion, 5, 3));
    }

    [Fact]
    public void Amount_BlockAndRegenScaleWithPlayers()
    {
        Assert.Equal(10, FermentoryRules.Amount(EnemyPotion.Block, 0, 1));
        Assert.Equal(45, FermentoryRules.Amount(EnemyPotion.Block, 1, 3));
        Assert.Equal(20, FermentoryRules.Amount(EnemyPotion.Block, 2, 1));
        Assert.Equal(8, FermentoryRules.Amount(EnemyPotion.Regen, 0, 2));
        Assert.Equal(6, FermentoryRules.Amount(EnemyPotion.Regen, 1, 1));
        Assert.Equal(32, FermentoryRules.Amount(EnemyPotion.Regen, 2, 4));
    }

    [Fact]
    public void PickFightReward_IsEven_AndNeverASpecialSlotWhenNoneCanBeGained()
    {
        var counts = Enum.GetValues<FightReward>().ToDictionary(reward => reward, _ => 0);
        for (int fight = 0; fight < 3000; fight++)
        {
            counts[FermentoryRules.PickFightReward(7UL, "loc:" + fight, 1UL, canGainSpecial: true)]++;
            Assert.NotEqual(FightReward.SpecialSlot, FermentoryRules.PickFightReward(7UL, "loc:" + fight, 1UL, canGainSpecial: false));
        }

        Assert.All(counts.Values, count => Assert.InRange(count, 850, 1150));
    }

    [Theory]
    [InlineData(40, 6)]
    [InlineData(100, 15)]
    [InlineData(7, 2)]
    [InlineData(0, 0)]
    public void BloodHeal_Is15PercentOfMaxHp_RoundedUp(int maxHp, int heal) => Assert.Equal(heal, FermentoryRules.BloodHeal(maxHp));
}
