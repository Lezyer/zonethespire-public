using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZoneTheSpire.Core.Scrapyard;

namespace ZoneTheSpire.Tests;

public class ScrapyardRulesTests
{
    [Fact]
    public void PickBots_PicksOneOrTwoBots_Deterministically_WithEveryTypeAndCountPossible()
    {
        var counts = new HashSet<int>();
        var types = new HashSet<ScrapBot>();
        for (int row = 0; row < 300; row++)
        {
            string location = $"a0:r{row}:c2:id0";
            IReadOnlyList<ScrapBot> bots = ScrapyardRules.PickBots(77UL, location);
            Assert.InRange(bots.Count, 1, 2);
            Assert.Equal(bots, ScrapyardRules.PickBots(77UL, location));
            counts.Add(bots.Count);
            foreach (ScrapBot bot in bots)
            {
                types.Add(bot);
            }
        }

        Assert.Equal(new[] { 1, 2 }, counts.OrderBy(c => c));
        Assert.Equal(4, types.Count);
    }

    [Theory]
    [InlineData(0, 70)]
    [InlineData(1, 80)]
    [InlineData(2, 100)]
    [InlineData(3, 100)]
    public void BotHpPercent_IsReducedInEarlyActs(int actIndex, int expected)
    {
        Assert.Equal(expected, ScrapyardRules.BotHpPercent(actIndex));
    }

    [Theory]
    [InlineData(20, 70, 14)]
    [InlineData(21, 70, 15)]
    [InlineData(24, 80, 19)]
    [InlineData(19, 100, 19)]
    [InlineData(1, 70, 1)]
    [InlineData(0, 70, 1)]
    public void ScaledHp_RoundsToNearest_AtLeastOne(int maxHp, int percent, int expected)
    {
        Assert.Equal(expected, ScrapyardRules.ScaledHp(maxHp, percent));
    }

    [Fact]
    public void RollRummage_IsDeterministic_AndDiffersBetweenPlayers()
    {
        var outcomes = new HashSet<RummageRolls>();
        for (ulong player = 0; player < 50; player++)
        {
            RummageRolls rolls = ScrapyardRules.RollRummage(9UL, "a1:r3:c4:id0", player);
            Assert.Equal(rolls, ScrapyardRules.RollRummage(9UL, "a1:r3:c4:id0", player));
            outcomes.Add(rolls);
        }

        Assert.True(outcomes.Count > 5, "different players should get different rummage outcomes");
    }

    [Fact]
    public void RollRummage_MatchesTheConfiguredChances()
    {
        const int samples = 4000;
        int gold = 0, potion = 0, card = 0, rare = 0, relic = 0;
        for (int i = 0; i < samples; i++)
        {
            RummageRolls rolls = ScrapyardRules.RollRummage(123UL, $"a0:r{i % 15}:c{i / 15}:id0", (ulong)i);
            gold += rolls.Gold ? 1 : 0;
            potion += rolls.Potion ? 1 : 0;
            card += rolls.Card ? 1 : 0;
            rare += rolls.RareCard ? 1 : 0;
            relic += rolls.Relic ? 1 : 0;
        }

        Assert.InRange(gold * 100.0 / samples, 56, 64);
        Assert.InRange(potion * 100.0 / samples, 46, 54);
        Assert.InRange(card * 100.0 / samples, 46, 54);
        Assert.InRange(rare * 100.0 / samples, 26, 34);
        Assert.InRange(relic * 100.0 / samples, 26, 34);
    }

    [Fact]
    public void FloatingBotXs_SpreadsBotsEvenly()
    {
        Assert.Equal(new[] { 500f }, ScrapyardRules.FloatingBotXs(1, 200f, 800f));
        Assert.Equal(new[] { 400f, 600f }, ScrapyardRules.FloatingBotXs(2, 200f, 800f));
        Assert.Empty(ScrapyardRules.FloatingBotXs(0, 200f, 800f));
    }

    [Theory]
    [InlineData(12, 1, true)]
    [InlineData(11, 5, false)]
    [InlineData(40, 0, false)]
    public void BuryIt_NeedsMoreThanElevenHp_AndARemovableCard(int currentHp, int removable, bool expected)
    {
        Assert.Equal(expected, ScrapyardRules.CanBury(currentHp, removable));
        Assert.Equal(2, ScrapyardRules.BuryMaxCards);
    }

    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, false)]
    [InlineData(3, 0, true)]
    [InlineData(5, 0, true)]
    public void ScrapyardAutomaton_AwakensAfterThreeCombats(int combatsSeen, int left, bool awakens)
    {
        Assert.Equal(left, ScrapyardRules.AutomatonCombatsLeft(combatsSeen));
        Assert.Equal(awakens, ScrapyardRules.AutomatonAwakens(combatsSeen));
    }

    [Fact]
    public void AutomatonForm_GivesThreeStrength_ThreeArtifact_FifteenPlating_UpgradedFourFourTwenty()
    {
        Assert.Equal(2, ScrapyardRules.AutomatonEnergyLoss);
        Assert.Equal(3, ScrapyardRules.AutomatonFormStrength);
        Assert.Equal(3, ScrapyardRules.AutomatonFormArtifact);
        Assert.Equal(15, ScrapyardRules.AutomatonFormPlating);
        Assert.Equal(4, ScrapyardRules.AutomatonFormUpgradedStrength);
        Assert.Equal(4, ScrapyardRules.AutomatonFormUpgradedArtifact);
        Assert.Equal(20, ScrapyardRules.AutomatonFormUpgradedPlating);
    }
}
