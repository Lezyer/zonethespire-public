using System;
using System.Collections.Generic;
using System.Globalization;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Scrapyard;

/// <summary>The act 3 bots the Scrapyard adds to fights.</summary>
public enum ScrapBot
{
    Noisebot,
    Guardbot,
    Stabbot,
    Zapbot,
}

/// <summary>Independent Rummage outcomes (each reward is rolled separately).</summary>
public readonly record struct RummageRolls(bool Gold, bool Potion, bool Card, bool RareCard, bool Relic)
{
    public bool Any => Gold || Potion || Card || RareCard || Relic;
}

/// <summary>Pure Scrapyard rules. Uses the mod's own seeded RNG so every multiplayer peer computes the same result.</summary>
public static class ScrapyardRules
{
    public const int GoldChance = 60;
    public const int PotionChance = 50;
    public const int CardChance = 50;
    public const int RareCardChance = 30;
    public const int RelicChance = 30;

    /// <summary>1–2 bots of random types for the fight at <paramref name="locationKey"/> (same on every peer).</summary>
    public static IReadOnlyList<ScrapBot> PickBots(ulong runSeed, string locationKey)
    {
        ZoneRandom random = ZoneRandom.ForStream(runSeed, "scrapyard.bots:" + locationKey);
        int count = 1 + random.NextInt(2);
        var bots = new List<ScrapBot>(count);
        for (int i = 0; i < count; i++)
        {
            bots.Add((ScrapBot)random.NextInt(4));
        }

        return bots;
    }

    /// <summary>Act 3 bots are weaker earlier: 70% Max HP in act 1, 80% in act 2, full HP afterwards.</summary>
    public static int BotHpPercent(int actIndex) => actIndex switch
    {
        0 => 70,
        1 => 80,
        _ => 100,
    };

    /// <summary><paramref name="percent"/>% of <paramref name="maxHp"/>, rounded to nearest, at least 1.</summary>
    public static int ScaledHp(int maxHp, int percent) =>
        Math.Max(1, (Math.Max(0, maxHp) * Math.Max(0, percent) + 50) / 100);

    /// <summary>
    /// Rummage rolls for one player: different per player (player id in the stream) but identical on every peer.
    /// </summary>
    public static RummageRolls RollRummage(ulong runSeed, string locationKey, ulong playerId)
    {
        ZoneRandom random = ZoneRandom.ForStream(runSeed, "scrapyard.rummage:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture));
        bool gold = random.NextInt(100) < GoldChance;
        bool potion = random.NextInt(100) < PotionChance;
        bool card = random.NextInt(100) < CardChance;
        bool rareCard = random.NextInt(100) < RareCardChance;
        bool relic = random.NextInt(100) < RelicChance;
        return new RummageRolls(gold, potion, card, rareCard, relic);
    }

    /// <summary>Evenly spread centre X positions for <paramref name="count"/> floating bots between the two limits.</summary>
    public static IReadOnlyList<float> FloatingBotXs(int count, float minX, float maxX)
    {
        var xs = new List<float>(Math.Max(0, count));
        for (int i = 0; i < count; i++)
        {
            xs.Add(minX + (maxX - minX) * (i + 1) / (count + 1));
        }

        return xs;
    }

    public const int BuryMaxCards = 2;
    public const int BuryHpLoss = 11;
    public const int AutomatonCombats = 3;
    public const int AutomatonEnergyLoss = 2;
    public const int AutomatonFormStrength = 3;
    public const int AutomatonFormArtifact = 3;
    public const int AutomatonFormPlating = 15;
    public const int AutomatonFormUpgradedStrength = 4;
    public const int AutomatonFormUpgradedArtifact = 4;
    public const int AutomatonFormUpgradedPlating = 20;

    /// <summary>The Buried Automaton: Bury It needs more HP than it costs and a card that can be removed.</summary>
    public static bool CanBury(int currentHp, int removableCards) => currentHp > BuryHpLoss && removableCards > 0;

    /// <summary>Combats left before the Scrapyard Automaton curse becomes Automaton Form (never negative).</summary>
    public static int AutomatonCombatsLeft(int combatsSeen) => Math.Max(0, AutomatonCombats - combatsSeen);

    /// <summary>Whether the Scrapyard Automaton curse has seen enough combats in the deck to become Automaton Form.</summary>
    public static bool AutomatonAwakens(int combatsSeen) => combatsSeen >= AutomatonCombats;
}
