using System;

namespace ZoneTheSpire.Core.Infestation;

/// <summary>Engine-free Wriggling rules: amounts on reward cards, amounts when played, and the card text line.</summary>
public static class WrigglingRules
{
    public const int AmountPerEnergy = 4;
    public const int ShopAmountPerEnergy = 2;
    public const int SmithAmountPerEnergy = 2;
    public const int OwnerDeathDamagePercent = 25;

    /// <summary>Wriggling a Festering Smith adds to the upgraded card: 2 per energy of its cost, 0-cost and X-cost cards counting as 1.</summary>
    public static int SmithAmount(int energyCost, bool costsX) => Amount(SmithAmountPerEnergy, energyCost, costsX);

    /// <summary>Wriggling put on an Infestation reward card: 4 per energy of its cost, 0-cost and X-cost cards counting as 1.</summary>
    public static int RewardAmount(int energyCost, bool costsX) => Amount(AmountPerEnergy, energyCost, costsX);

    /// <summary>Wriggling put on a card sold in an Infestation shop: 2 per energy of its cost, 0-cost and X-cost cards counting as 1.</summary>
    public static int ShopAmount(int energyCost, bool costsX) => Amount(ShopAmountPerEnergy, energyCost, costsX);

    private static int Amount(int perEnergy, int energyCost, bool costsX) =>
        perEnergy * Math.Max(1, costsX ? 1 : energyCost);

    /// <summary>Wriggler HP summoned when the card is played: X-cost cards multiply by the energy spent (at least 1).</summary>
    public static int PlayAmount(int amount, bool costsX, int energySpent) =>
        costsX ? amount * Math.Max(1, energySpent) : amount;

    /// <summary>Damage the Wriggler deals to its owner when it dies: 25% of its Max HP, rounded down, at least 1.</summary>
    public static int OwnerDeathDamage(int maxHp) => maxHp <= 0 ? 0 : Math.Max(1, maxHp * OwnerDeathDamagePercent / 100);

    /// <summary>The line shown at the top of the card's text.</summary>
    public static string CardText(int amount, bool costsX) =>
        costsX ? $"[gold]{InfestationText.Wriggling}[/gold] [blue]{amount}[/blue]X" : $"[gold]{InfestationText.Wriggling}[/gold] [blue]{amount}[/blue]";
}
