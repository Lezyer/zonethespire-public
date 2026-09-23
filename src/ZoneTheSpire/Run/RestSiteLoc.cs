using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using ZoneTheSpire.Core.BloodRain;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Core.Midas;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Core.Scrapyard;
using ZoneTheSpire.Core.Shadow;

namespace ZoneTheSpire.Run;

/// <summary>
/// Names and descriptions of the mod's rest site options, written into the game's rest_site_ui table (the same reflection
/// technique BaseLib uses). RestSiteOption.Title is not virtual and always reads OPTION_&lt;id&gt;.name from that table.
/// Mirrored descriptions reuse the raw vanilla HEAL/MEND text so their variables still resolve.
/// LocManager.SetLanguageInternal rebuilds the tables, so a patch calls Inject again after every table swap.
/// </summary>
internal static class RestSiteLoc
{
    public const string MirroredHealId = "ZTS_MIRRORED_HEAL";
    public const string MirroredMendId = "ZTS_MIRRORED_MEND";
    public const string RummageId = "ZTS_RUMMAGE";
    public const string StareAtPrismId = "ZTS_STARE_AT_PRISM";
    public const string FesteringSmithId = "ZTS_FESTERING_SMITH";
    public const string BloodSacrificeId = "ZTS_BLOOD_SACRIFICE";
    public const string LuxuriousRestId = "ZTS_LUXURIOUS_REST";
    public const string HauntId = "ZTS_HAUNT";
    public const string MagnetizeId = "ZTS_MAGNETIZE";
    public const string SculptId = "ZTS_SCULPT";
    public const string TroubledDreamsId = "ZTS_TROUBLED_DREAMS";
    public const string MeditateId = "ZTS_MEDITATE";
    public const string FrostbindId = "ZTS_FROSTBIND";
    public const string RepentId = "ZTS_REPENT";
    public const string BlasphemeId = "ZTS_BLASPHEME";
    public const string DistilId = "ZTS_DISTIL";

    /// <summary>Brew Extractor's card reward alternative (the game reads its button text from card_reward_ui).</summary>
    public const string ExtractAlternativeId = "ZTS_EXTRACT";
    private const string CardRewardTable = "card_reward_ui";
    private const string BlasphemePromptKey = "ZTS_TO_BLASPHEME";
    private const string SculptPromptKey = "ZTS_TO_SCULPT";
    private const string FesteringSmithPromptKey = "ZTS_TO_FESTERING_SMITH";
    private const string MagnetizeRewardKey = "ZTS_COMBAT_REWARD_MAGNETIZE";
    private const string PotionSlotRewardKey = "ZTS_COMBAT_REWARD_POTION_SLOT";
    private const string SpecialSlotRewardKey = "ZTS_COMBAT_REWARD_SPECIAL_SLOT";
    private const string GameplayTable = "gameplay_ui";
    private const string TableName = "rest_site_ui";
    private const string CardSelectionTable = "card_selection";
    private const string MeditatePromptKey = "ZTS_TO_MEDITATE";
    private const string FrostbindPromptKey = "ZTS_TO_FROSTBIND";

    /// <summary>Reward screen text for the Ferrosand Magnetize fight reward (written into the game's gameplay_ui table by Inject).</summary>
    public static LocString MagnetizeRewardDescription => new(GameplayTable, MagnetizeRewardKey);

    /// <summary>Reward screen text for the Fermentory potion slot fight reward.</summary>
    public static LocString PotionSlotRewardDescription => new(GameplayTable, PotionSlotRewardKey);

    /// <summary>Reward screen text for the Fermentory special potion slot fight reward.</summary>
    public static LocString SpecialSlotRewardDescription => new(GameplayTable, SpecialSlotRewardKey);

    /// <summary>Card selection prompt for Sculpt (written into the game's card_selection table by Inject).</summary>
    public static LocString SculptPrompt => new(CardSelectionTable, SculptPromptKey);

    /// <summary>Card selection prompt for Meditate (written into the game's card_selection table by Inject).</summary>
    public static LocString MeditatePrompt => new(CardSelectionTable, MeditatePromptKey);

    /// <summary>Card selection prompt for Frostbind (written into the game's card_selection table by Inject).</summary>
    public static LocString FrostbindPrompt => new(CardSelectionTable, FrostbindPromptKey);

    /// <summary>Card selection prompt for Blaspheme (written into the game's card_selection table by Inject).</summary>
    public static LocString BlasphemePrompt => new(CardSelectionTable, BlasphemePromptKey);

    /// <summary>Upgrade prompt unique to Festering Smith, allowing its preview to include the resulting Wriggling.</summary>
    public static LocString FesteringSmithPrompt => new(CardSelectionTable, FesteringSmithPromptKey);

    private static readonly FieldInfo? TranslationsField = AccessTools.Field(typeof(LocTable), "_translations");

    public static void EnsureInjected()
    {
        try
        {
            if (LocManager.Instance != null && !LocManager.Instance.GetTable(TableName).HasEntry($"OPTION_{MirroredHealId}.description"))
            {
                Inject();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to check rest site option text: {ex}");
        }
    }

    public static void Inject()
    {
        try
        {
            if (LocManager.Instance == null || TranslationsField == null)
            {
                return;
            }

            LocTable table = LocManager.Instance.GetTable(TableName);
            if (TranslationsField.GetValue(table) is not Dictionary<string, string> entries)
            {
                Log.Warn("rest_site_ui has no translation dictionary; Zone the Spire rest site text unavailable.");
                return;
            }

            // The mod's own campfire, selection and reward text comes from Localization/<language>/*.json (ModLocalization).
            // Only the options that extend a vanilla option's text are composed here: the game's own text (already in the
            // player's language) plus the mod's added sentence from the mod's table.
            ModText.Reset();
            entries[$"OPTION_{MirroredHealId}.description"] = Raw(table, "OPTION_HEAL.description") + "\n" + ModText.Get("rest_site.mirrored_rest.addition");
            entries[$"OPTION_{MirroredMendId}.description"] = Raw(table, "OPTION_MEND.description") + "\n" + ModText.Get("rest_site.mirrored_mend.addition");
            entries[$"OPTION_{FesteringSmithId}.description"] = Raw(table, "OPTION_SMITH.description") + "\n" + ModText.Get("rest_site.festering_smith.addition");
            entries[$"OPTION_{FesteringSmithId}.descriptionDisabled"] = Raw(table, "OPTION_SMITH.descriptionDisabled");
            entries[$"OPTION_{LuxuriousRestId}.description"] = ModText.Get("rest_site.luxurious_rest.cost") + " " + DoubledHealText(table);
            entries[$"OPTION_{LuxuriousRestId}.descriptionNoGold"] =
                ModText.Get("rest_site.luxurious_rest.cost") + " " + DoubledHealText(table) + "\n" + ModText.Get("rest_site.luxurious_rest.no_gold");
            entries[$"OPTION_{TroubledDreamsId}.description"] =
                WithBeforeExtraText(Raw(table, "OPTION_HEAL.description"), "\n" + ModText.Get("rest_site.troubled_dreams.addition"));
            entries[$"OPTION_{BlasphemeId}.description"] = Raw(table, "OPTION_SMITH.description") + "\n" + ModText.Get("rest_site.blaspheme.addition");
            entries[$"OPTION_{BlasphemeId}.descriptionDisabled"] = Raw(table, "OPTION_SMITH.descriptionDisabled");
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add Zone the Spire rest site text: {ex}");
        }
    }

    /// <summary>
    /// Vanilla rest text for Luxurious Rest. The "30%" is literal text in the loc table (only {Heal} is dynamic, and the option
    /// already doubles that), so the percentage is rewritten to the doubled 60% ("30 %" spacing in some languages kept).
    /// </summary>
    private static string DoubledHealText(LocTable table) =>
        System.Text.RegularExpressions.Regex.Replace(Raw(table, "OPTION_HEAL.description"), @"\b30(\s?%)", "60$1");

    /// <summary>Appends <paramref name="addition"/> to vanilla rest text, before its {ExtraText} slot (relic lines) if it has one.</summary>
    private static string WithBeforeExtraText(string vanilla, string addition)
    {
        int slot = vanilla.IndexOf("{ExtraText}", StringComparison.Ordinal);
        return slot < 0 ? vanilla + addition : vanilla.Insert(slot, addition);
    }

    private static string Raw(LocTable table, string key) => table.HasEntry(key) ? table.GetRawText(key) : string.Empty;
}
