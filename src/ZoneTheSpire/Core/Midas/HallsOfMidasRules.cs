using System;

namespace ZoneTheSpire.Core.Midas;

/// <summary>Engine-free Halls of Midas rules: Touch of Midas, the gilded shop markup, Luxurious Rest and The Golden Throne event.</summary>
public static class HallsOfMidasRules
{
    public const int StrengthPerTurn = 1;
    public const int ShopMarkupPercent = 30;
    public const int LuxuriousRestGoldCost = 50;
    public const int LuxuriousHealMultiplier = 2;

    public const int GoldPerDamage = 3;

    /// <summary>Gold a player gains when a Touch of Midas enemy deals them unblocked damage: 3 gold per point of that damage.</summary>
    public static int GoldForDamage(int unblockedDamage) => Math.Max(0, unblockedDamage) * GoldPerDamage;

    /// <summary>A Halls of Midas shop card price: the normal price (after sales) plus 30%, rounded to nearest (halves up).</summary>
    public static decimal CardPrice(decimal cost) =>
        cost <= 0 ? cost : Math.Round(cost * (100 + ShopMarkupPercent) / 100m, MidpointRounding.AwayFromZero);

    public static bool CanAffordLuxuriousRest(int gold) => gold >= LuxuriousRestGoldCost;

    /// <summary>Luxurious Rest heals twice the (hook-modified) vanilla rest heal.</summary>
    public static decimal LuxuriousHealAmount(decimal restHealAmount) => restHealAmount * LuxuriousHealMultiplier;

    public const int TributeCost = 100;
    public const int GildedGold = 5;
    public const int GildedHpLoss = 1;

    /// <summary>The Golden Throne: Pay Tribute needs the full 100 gold.</summary>
    public static bool CanPayTribute(int gold) => gold >= TributeCost;

    /// <summary>The Golden Throne: Pocket the Coins gives 200 / 250 / 280 gold in acts 1 / 2 / 3 (and later).</summary>
    public static int PlunderGold(int actIndex) => actIndex <= 0 ? 200 : actIndex == 1 ? 250 : 280;

    /// <summary>A card can be Gilded when it can be played and isn't Gilded yet.</summary>
    public static bool CanGild(bool unplayable, bool alreadyGilded) => !unplayable && !alreadyGilded;

    /// <summary>The line a Gilded card shows at the top of its text.</summary>
    public static string GildedCardText => GildedCardTextFor(GildedGold);

    /// <summary>The Gilded line for a given gold amount (a Shadow Corrupted card doubles the gold, not the HP loss).</summary>
    public static string GildedCardTextFor(int gold) =>
        $"[gold]Gilded[/gold]: gain [gold]{gold} Gold[/gold], lose [red]{GildedHpLoss}[/red] HP.";
}
