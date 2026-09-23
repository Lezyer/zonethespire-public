using System.Globalization;

using ZoneTheSpire.Core.Localization;

namespace ZoneTheSpire.Core.Fermentory;

/// <summary>
/// The Fermentory's player-facing mechanic text, written once: the zone glossary (FermentoryBiome.Keywords), the special
/// slots' hover tips, the enemy potions' hover lines and the shop and campfire text use these strings. Numbers come from
/// <see cref="FermentoryRules"/>.
/// </summary>
public static class FermentoryText
{
    public static string SpecialSlot => ModText.Get("fermentory.special_slot");
    public static string Brewing => ModText.Get("fermentory.brewing");
    public static string Bottle => ModText.Get("fermentory.bottle");
    public static string Distil => ModText.Get("fermentory.distil");

    public static string SpecialSlotDescription => ModText.Get("fermentory.special_slot_description");

    public static string BrewingDescription => ModText.Get("fermentory.brewing_description");

    public static string BottleDescription => ModText.Get("fermentory.bottle_description");

    public static string DistilDescription => ModText.Get("fermentory.distil_description");

    public static string DistilNoSlotDescription => ModText.Get("fermentory.distil_no_slot_description");

    /// <summary>"DuplicatingSolution" -> "duplicating_solution", the key part for an effect or potion.</summary>
    private static string KeyPart(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();

    public static string NameKey(SlotEffect effect) => $"fermentory.slot.{KeyPart(effect.ToString())}.name";

    public static string Name(SlotEffect effect) => ModText.Get(NameKey(effect));

    public static string Description(SlotEffect effect) => ModText.Get($"fermentory.slot.{KeyPart(effect.ToString())}.description");

    /// <summary>
    /// What an enemy's brewed potion will do, for its hover tip ("After acting, it gains 2 Strength."); harmful ones name every
    /// player in multiplayer and "you" alone.
    /// </summary>
    public static string EnemyPotionLine(EnemyPotion potion, int amount, bool multiplayer) =>
        ModText.Format(
            $"fermentory.enemy_potion.{KeyPart(potion.ToString())}",
            ("Amount", amount.ToString(CultureInfo.InvariantCulture)),
            ("Players", ModText.Get(multiplayer ? "fermentory.enemy_potion.targets.all" : "fermentory.enemy_potion.targets.solo")));
}
