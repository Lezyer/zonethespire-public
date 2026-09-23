using System;
using System.Collections.Generic;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Prismatic;

/// <summary>Buffs a Prismatic Storm enemy can start with.</summary>
public enum PrismaticBuff
{
    CurlUp,
    Enrage,
    Ritual,
    Strength,
    Regen,
    Intangible,
    Thorns,
    Plating,
    PersonalHive,
    Slippery,
}

/// <summary>Pure Prismatic Storm rules. Uses the mod's own seeded RNG so every multiplayer peer computes the same result.</summary>
public static class PrismaticRules
{
    public static IReadOnlyList<PrismaticBuff> AllBuffs { get; } = Enum.GetValues<PrismaticBuff>();

    /// <summary>The buff for the enemy at <paramref name="enemyIndex"/> (combat order) in the fight at <paramref name="locationKey"/>.</summary>
    public static PrismaticBuff PickBuff(ulong runSeed, string locationKey, int enemyIndex) =>
        AllBuffs[ZoneRandom.ForStream(runSeed, "prismatic.buff:" + locationKey + ":" + StableHash.Inv(enemyIndex)).NextInt(AllBuffs.Count)];

    /// <summary>
    /// Buff amount before multiplayer scaling, by act (acts after the third use act 3 values). Curl Up is a percentage of the
    /// enemy's Max HP before multiplayer scaling (15% / 15% / 20%), rounded to nearest, at least 1.
    /// </summary>
    public static int BuffAmount(PrismaticBuff buff, int actIndex, int baseMaxHp)
    {
        int act = Math.Clamp(actIndex, 0, 2);
        return buff switch
        {
            PrismaticBuff.CurlUp => Math.Max(1, (Math.Max(0, baseMaxHp) * ByAct(act, 15, 15, 20) + 50) / 100),
            PrismaticBuff.Enrage => ByAct(act, 1, 1, 2),
            PrismaticBuff.Ritual => ByAct(act, 1, 2, 3),
            PrismaticBuff.Strength => ByAct(act, 3, 4, 5),
            PrismaticBuff.Regen => ByAct(act, 4, 5, 7),
            PrismaticBuff.Intangible => ByAct(act, 1, 1, 2),
            PrismaticBuff.Thorns => ByAct(act, 3, 4, 5),
            PrismaticBuff.Plating => ByAct(act, 10, 15, 20),
            PrismaticBuff.PersonalHive => 1,
            PrismaticBuff.Slippery => ByAct(act, 2, 3, 5),
            _ => 0,
        };
    }

    private static int ByAct(int act, int act1, int act2, int act3) => act switch
    {
        0 => act1,
        1 => act2,
        _ => act3,
    };

    public const int SnatchHpLoss = 8;
    public const int ColourThiefChoices = 3;

    /// <summary>The Colour Thief: Snatch the Bundle needs more HP than it costs.</summary>
    public static bool CanSnatch(int currentHp) => currentHp > SnatchHpLoss;

    /// <summary>
    /// Trade Colours only rolls cards that naturally cost 0: printed cost 0, not X-cost and not Unplayable. No card's cost is
    /// changed.
    /// </summary>
    public static bool IsTradeReplacement(int printedCost, bool costsX, bool unplayable) => printedCost == 0 && !costsX && !unplayable;

    /// <summary>
    /// Colour Thief's Bundle: a card is from another character when it belongs to a character pool that isn't the player's own
    /// (Colorless and other non-character cards never count).
    /// </summary>
    public static bool IsFromAnotherCharacter(bool inCharacterPool, bool inOwnPool) => inCharacterPool && !inOwnPool;

    /// <summary>Colour Thief's Bundle: only the first card from another character each turn is played an extra time.</summary>
    public static bool BundleReplays(int offColourCardsPlayedThisTurn) => offColourCardsPlayedThisTurn == 0;
}
