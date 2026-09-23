using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Powers;
using ZoneTheSpire.Run.Wriggling;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Custom zone power icons are loose 256x256 PNGs. PowerModel's Icon and BigIcon load game resources only, so both getters
/// return the original mod texture instead (with the closest vanilla icon as a defensive fallback).
/// </summary>
[HarmonyPatch]
internal static class BloodDrinkerIconPatch
{
    private static readonly string VanillaBurstIconPath = ImageHelper.GetImagePath("powers/burst_power.png");
    private static readonly string VanillaStrengthIconPath = ImageHelper.GetImagePath("powers/strength_power.png");
    private static readonly string VanillaHauntIconPath = ImageHelper.GetImagePath("powers/haunt_power.png");
    private static readonly string VanillaBlackHoleIconPath = ImageHelper.GetImagePath("powers/black_hole_power.png");
    private static readonly string VanillaPanacheIconPath = ImageHelper.GetImagePath("powers/panache_power.png");
    private static readonly string VanillaRollingBoulderIconPath = ImageHelper.GetImagePath("powers/rolling_boulder_power.png");
    private static readonly string VanillaPlatingIconPath = ImageHelper.GetImagePath("powers/plating_power.png");
    private static readonly string VanillaReflectIconPath = ImageHelper.GetImagePath("powers/reflect_power.png");
    private static readonly string VanillaInfestedIconPath = ImageHelper.GetImagePath("powers/infested_power.png");
    private static readonly string VanillaLoopIconPath = ImageHelper.GetImagePath("powers/loop_power.png");
    private static readonly string VanillaRadianceIconPath = ImageHelper.GetImagePath("powers/radiance_power.png");
    private static readonly string VanillaIntangibleIconPath = ImageHelper.GetImagePath("powers/intangible_power.png");
    private static readonly string VanillaFrailIconPath = ImageHelper.GetImagePath("powers/frail_power.png");
    private static readonly string VanillaDoomIconPath = ImageHelper.GetImagePath("powers/doom_power.png");

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Icon));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.BigIcon));
    }

    private static bool Prefix(PowerModel __instance, ref Texture2D __result)
    {
        try
        {
            Texture2D? texture = __instance switch
            {
                BloodDrinkerPower => ModTextures.Get("blood_drinker.png", VanillaBurstIconPath),
                TouchOfMidasPower => ModTextures.Get("touch_of_midas.png", VanillaStrengthIconPath),
                PhantasmPower => ModTextures.Get("phantasm.png", VanillaHauntIconPath),
                MagnetizedPower => ModTextures.Get("magnetized.png", VanillaBlackHoleIconPath),
                FerroformPower => ModTextures.Get("ferroform.png", VanillaPanacheIconPath),
                ForgottenStatuePower => ModTextures.Get("forgotten_statue.png", VanillaRollingBoulderIconPath),
                MarbledPower => ModTextures.Get("marbled.png", VanillaRollingBoulderIconPath),
                PolishingPower => ModTextures.Get("polishing.png", VanillaPlatingIconPath),
                MirroredPower => ModTextures.Get("mirrored.png", VanillaReflectIconPath),
                MirroredCarapacePower => ModTextures.Get("mirrored_carapace.png", VanillaReflectIconPath),
                ReflectionPower => ModTextures.Get("reflection.png", VanillaReflectIconPath),
                WrigglerGuardPower => ModTextures.Get("wriggler_guard.png", VanillaInfestedIconPath),
                ShadowBrutalityPower => ModTextures.Get("shadow_brutality.png", VanillaStrengthIconPath),
                SamsaraPower => ModTextures.Get("samsara.png", VanillaLoopIconPath),
                DevasBlessingPower => ModTextures.Get("devas_blessing.png", VanillaRadianceIconPath),
                WorldlyAttachmentPower => ModTextures.Get("worldly_attachment_power.png", VanillaIntangibleIconPath),
                BitingColdPower => ModTextures.Get("biting_cold.png", VanillaFrailIconPath),
                HoarfrostPower => ModTextures.Get("hoarfrost.png", VanillaFrailIconPath),
                HallowedPower => ModTextures.Get("hallowed.png", VanillaDoomIconPath),
                ZealousPower => ModTextures.Get("zealous.png", VanillaStrengthIconPath),
                BlasphemerPower => ModTextures.Get("blasphemer.png", VanillaDoomIconPath),
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
            Log.Warn($"Failed to serve a zone power icon: {ex}");
            return true;
        }
    }
}
