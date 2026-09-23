using System;

namespace ZoneTheSpire.Core.ForgottenEmpire;

/// <summary>Which Forgotten Empire card modifier a card reward gets.</summary>
public enum MarbleRewardModifier
{
    None,
    Marbled,
    Marbling,
}

/// <summary>
/// Engine-free Forgotten Empire rules: Marbled (a layer that takes damage after Block and before HP, and grows each turn),
/// Forgotten Statues, the Marbled and Marbling card modifiers and Sculpt. Nothing here is random, so every multiplayer peer
/// computes the same values from the same synced state.
/// </summary>
public static class ForgottenEmpireRules
{
    public const int StatueMarbledPercent = 30;
    public const int StatueHpPercent = 80;
    public const int MarbledCardBlockPercent = 50;
    public const int PolishingPerPlayer = 10;
    public const int MarblingPerEnergy = 5;
    public const int StatueDamageTakenPercent = 75;

    /// <summary>How much less damage a Marbled statue takes, for its text.</summary>
    public const int StatueDamageReductionPercent = 100 - StatueDamageTakenPercent;
    public const int BrokenStatueWeak = 1;
    public const int BrokenStatueVulnerable = 1;

    public static string MarbledCardText => "[gold]Marbled[/gold].";

    /// <summary>The line a Marbled card adds below its text; <paramref name="amount"/> may already carry colour tags.</summary>
    public static string MarbledGainText(string amount) => $"Gain {amount} [gold]Marbled[/gold].";

    public static string MarblingCardText(int amount) => $"[gold]Marbling[/gold] [blue]{amount}[/blue].";

    /// <summary>
    /// Marbled a Forgotten Statue starts with: 30% of its original Max HP, rounded down, at least 1. Together with
    /// <see cref="StatueMaxHp"/> (80%) the statue has 110% of its original HP in total.
    /// </summary>
    public static int StartingMarbled(int maxHp) => maxHp <= 0 ? 0 : Math.Max(1, maxHp * StatueMarbledPercent / 100);

    /// <summary>A Forgotten Statue's Max HP (and HP): 80% of its original Max HP, rounded down, at least 1.</summary>
    public static int StatueMaxHp(int maxHp) => maxHp <= 0 ? 0 : Math.Max(1, maxHp * StatueHpPercent / 100);

    /// <summary>Block a Marbled card still gives: 50% of its Block, rounded down (it also gives 100% as Marbled).</summary>
    public static int MarbledCardBlock(int block) => Math.Max(0, block * MarbledCardBlockPercent / 100);

    /// <summary>Polishing a Forgotten Statue starts with: 10 per player (it grants that much Marbled each turn, then ticks down).</summary>
    public static int PolishingAmount(int playerCount) => PolishingPerPlayer * Math.Max(1, playerCount);

    /// <summary>
    /// Damage multiplier for a Forgotten Statue: 25% less damage while it still has Marbled, normal damage once it breaks.
    /// Unblockable HP loss (which also skips Marbled) is never reduced.
    /// </summary>
    public static decimal StatueDamageMultiplier(int marbled, bool unblockable) =>
        marbled > 0 && !unblockable ? StatueDamageTakenPercent / 100m : 1m;

    /// <summary>How much of an HP loss the Marbled layer takes: all of it, up to the Marbled left.</summary>
    public static int AbsorbedDamage(int marbled, int hpLoss) => Math.Max(0, Math.Min(marbled, hpLoss));

    /// <summary>
    /// Marbling on a card: 5 per energy it costs. 0-cost and X-cost cards count as costing 1, so the modifier is never
    /// worthless.
    /// </summary>
    public static int MarblingAmount(int cost, bool costsX) => MarblingPerEnergy * (costsX ? 1 : Math.Max(1, cost));

    /// <summary>Block Marbling converts into Marbled: up to its amount, limited by the Block the player has.</summary>
    public static int ConvertedBlock(int block, int marbling) => Math.Max(0, Math.Min(block, marbling));

    /// <summary>
    /// Card rewards: cards that gain Block become Marbled; other playable cards get Marbling. A card that already has either
    /// modifier gets nothing more.
    /// </summary>
    public static MarbleRewardModifier RewardModifier(bool gainsBlock, bool unplayable, bool alreadyMarbled, bool alreadyMarbling)
    {
        if (alreadyMarbled || alreadyMarbling)
        {
            return MarbleRewardModifier.None;
        }

        if (gainsBlock)
        {
            return MarbleRewardModifier.Marbled;
        }

        return unplayable ? MarbleRewardModifier.None : MarbleRewardModifier.Marbling;
    }

    /// <summary>Shop cards: only cards that gain Block (and aren't Marbled yet) become Marbled.</summary>
    public static bool CanMarble(bool gainsBlock, bool alreadyMarbled) => gainsBlock && !alreadyMarbled;

    public const int NoseHealPercent = 50;
    public const int CrownMarbledPercent = 50;

    /// <summary>The Forgotten Emperor: Break Its Nose heals half of Max HP, rounded down.</summary>
    public static int NoseHeal(int maxHp) => System.Math.Max(0, maxHp) * NoseHealPercent / 100;

    /// <summary>
    /// Emperor's Will can take a Power that can be removed and played on its own: not Unplayable, and not X-cost (an autoplayed
    /// X-cost card would count X as 0).
    /// </summary>
    public static bool CanBecomeWill(bool isPower, bool removable, bool unplayable, bool costsX) =>
        isPower && removable && !unplayable && !costsX;

    /// <summary>Calcified Crown: Marbled gained from Block about to be cleared at the start of a turn (half, rounded down).</summary>
    public static int CrownMarbled(int blockCleared) => System.Math.Max(0, blockCleared) * CrownMarbledPercent / 100;
}
