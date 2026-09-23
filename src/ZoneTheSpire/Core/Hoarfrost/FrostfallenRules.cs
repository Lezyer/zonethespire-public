using System;
using System.Globalization;
using ZoneTheSpire.Core.Generation;

namespace ZoneTheSpire.Core.Hoarfrost;

/// <summary>
/// Engine-free rules of Frostfallen (Hoarfrost): the price of each way out of the ice. Ditching your gear costs a relic and half
/// your gold and binds Biting Cold to 3 cards at Frostbind's rate; struggling through costs HP and binds it to 1 at the card
/// reward rate; cutting yourself out costs nothing now and an Injury forever. The relic is picked from the run seed and node, so
/// every peer loses the same one and the option can name it before it is chosen.
/// </summary>
public static class FrostfallenRules
{
    public const int StruggleHpLoss = 15;
    public const int DitchCards = 3;
    public const int StruggleCards = 1;

    /// <summary>Biting Cold each ditched-gear card gains, per energy of its cost (Frostbind's rate).</summary>
    public const int DitchColdPerCost = FrostRules.FrostbindColdPerCost;

    /// <summary>Biting Cold the struggled-for card gains, per energy of its cost (the card reward rate).</summary>
    public const int StruggleColdPerCost = FrostRules.RewardColdPerCost;

    /// <summary>
    /// Gold lost when you ditch your gear: half of it, rounded down, but never nothing while you still have some. With no gold
    /// there is nothing to lose and the option doesn't mention it.
    /// </summary>
    public static int GoldLoss(int gold) => gold <= 0 ? 0 : Math.Max(1, gold / 2);

    /// <summary>Struggling through needs more HP than it costs.</summary>
    public static bool CanStruggle(int currentHp) => currentHp > StruggleHpLoss;

    /// <summary>How many cards a way out binds Biting Cold to: its own count, or every eligible card when fewer remain.</summary>
    public static int BindCountFor(int eligibleCards, int wanted) => Math.Clamp(eligibleCards, 0, wanted);

    /// <summary>Biting Cold a card gains, at <paramref name="perCost"/> per energy of its cost (0-cost cards count as 1).</summary>
    public static int BoundCold(int perCost, int energyCost, bool costsX) => FrostRules.ColdFor(perCost, energyCost, costsX);

    /// <summary>
    /// Which of the relics you could lose the ice takes, or -1 when there are none (the option is then locked). Decided by the
    /// run seed, node and player, so it never changes while you look at it and every peer loses the same relic.
    /// </summary>
    public static int PickDoomedRelicIndex(ulong runSeed, string locationKey, ulong playerId, int losableRelics) =>
        losableRelics <= 0
            ? -1
            : ZoneRandom.ForStream(
                    runSeed,
                    "hoarfrost.frostfallen.relic:" + locationKey + ":" + playerId.ToString(CultureInfo.InvariantCulture))
                .NextInt(losableRelics);
}
