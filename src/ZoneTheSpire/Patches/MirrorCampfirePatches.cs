using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run;
using ZoneTheSpire.Run.Campfire;

namespace ZoneTheSpire.Patches;

/// <summary>
/// SetLanguageInternal replaces every loc table (SetLanguage and the English override/restore all go through it); add the
/// Mirrorlands rest site text back.
/// </summary>
[HarmonyPatch(typeof(LocManager), "SetLanguageInternal")]
internal static class RestSiteLocLanguagePatch
{
    private static void Postfix()
    {
        try
        {
            RestSiteLoc.Inject();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to re-inject rest site option text after a loc table swap: {ex}");
        }
    }
}

/// <summary>
/// Mirrorlands rest site option icons. RestSiteOption.Icon loads "ui/rest_site/option_&lt;id&gt;.png", which doesn't exist for
/// the Mirrorlands option ids, so the getter returns the mod texture instead (vanilla heal/mend icon as fallback). Earlier
/// attempts on AssetCache didn't work in game: the one-line GetTexture2D is inlined into Icon, and GetAsset returns Resource
/// so a Texture2D-typed patch was rejected by Harmony.
/// </summary>
[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Icon), MethodType.Getter)]
internal static class MirroredRestSiteIconPatch
{
    private static readonly string VanillaHealIconPath = ImageHelper.GetImagePath("ui/rest_site/option_heal.png");
    private static readonly string VanillaMendIconPath = ImageHelper.GetImagePath("ui/rest_site/option_mend.png");
    private static readonly string VanillaSmithIconPath = ImageHelper.GetImagePath("ui/rest_site/option_smith.png");

    private static bool Prefix(RestSiteOption __instance, ref Texture2D __result)
    {
        try
        {
            Texture2D? texture = __instance switch
            {
                MirroredRestRestSiteOption => ModTextures.Get("mirrored_rest.png", VanillaHealIconPath),
                MirroredMendRestSiteOption => ModTextures.Get("mirrored_mend.png", VanillaMendIconPath),
                RummageRestSiteOption => ModTextures.Get("rummage.png", VanillaHealIconPath),
                StareAtPrismRestSiteOption => ModTextures.Get("stare_at_prism.png", VanillaHealIconPath),
                FesteringSmithRestSiteOption => ModTextures.Get("festering_smith.png", VanillaSmithIconPath),
                BloodSacrificeRestSiteOption => ModTextures.Get("blood_sacrifice.png", VanillaHealIconPath),
                LuxuriousRestRestSiteOption => ModTextures.Get("luxurious_rest.png", VanillaHealIconPath),
                HauntRestSiteOption => ModTextures.Get("haunt.png", VanillaHealIconPath),
                MagnetizeRestSiteOption => ModTextures.Get("magnetize.png", VanillaSmithIconPath),
                SculptRestSiteOption => ModTextures.Get("sculpt.png", VanillaSmithIconPath),
                TroubledDreamsRestSiteOption => ModTextures.Get("troubled_dreams.png", VanillaHealIconPath),
                MeditateRestSiteOption => ModTextures.Get("meditate.png", VanillaHealIconPath),
                FrostbindRestSiteOption => ModTextures.Get("frostbind.png", VanillaSmithIconPath),
                RepentRestSiteOption => ModTextures.Get("repent.png", VanillaHealIconPath),
                DistilRestSiteOption => ModTextures.Get("distil.png", VanillaHealIconPath),
                BlasphemeRestSiteOption => ModTextures.Get("blaspheme.png", VanillaSmithIconPath),
                _ => null,
            };
            if (texture == null)
            {
                return true;
            }

            __result = texture;
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to serve a Mirrorlands rest site icon: {ex}");
            return true;
        }
    }
}
