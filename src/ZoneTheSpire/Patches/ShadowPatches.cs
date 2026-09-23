using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Run.Effects;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Shadow Corruption hidden intents: while an enemy's intent is hidden from the local player (ShadowSight), its intent node shows
/// the game's own Unknown ("?") intent instead, with the Unknown hover tip. Only the displayed intent changes; the monster's
/// move, and every other machine, are untouched.
/// </summary>
[HarmonyPatch(typeof(NIntent), nameof(NIntent.UpdateIntent))]
internal static class ShadowIntentPatch
{
    private static void Prefix(ref AbstractIntent intent, Creature owner)
    {
        try
        {
            if (ShadowSight.ShouldHide(owner))
            {
                intent = ShadowSight.Hidden;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to hide an intent: {ex.Message}");
        }
    }
}

/// <summary>
/// The Shadowed Path: a hidden Shadow Corruption node shows the vanilla "?" icon upside down (icon and outline flipped) and tinted
/// violet (Modulate, on top of the game's own state colour in SelfModulate). Once the
/// node is revealed the game's own icon is shown again. Local presentation only.
/// </summary>
[HarmonyPatch(typeof(NNormalMapPoint), "UpdateIcon")]
internal static class ShadowMapIconPatch
{
    private static readonly string UnknownIconPath = ImageHelper.GetImagePath("atlases/ui_atlas.sprites/map/icons/map_unknown.tres");
    private static readonly string UnknownOutlinePath = ImageHelper.GetImagePath("atlases/compressed.sprites/map/map_unknown_outline.tres");

    private static readonly AccessTools.FieldRef<NNormalMapPoint, TextureRect> Icon = SafeRef.Field<NNormalMapPoint, TextureRect>("_icon");
    private static readonly AccessTools.FieldRef<NNormalMapPoint, TextureRect> Outline = SafeRef.Field<NNormalMapPoint, TextureRect>("_outline");
    private static readonly AccessTools.FieldRef<NNormalMapPoint, IRunState> RunState = SafeRef.Field<NNormalMapPoint, IRunState>("_runState");

    private static void Postfix(NNormalMapPoint __instance)
    {
        try
        {
            bool hidden = RunState(__instance) is { } runState && __instance.Point is { } point && ShadowPath.IsHidden(runState, point);
            TextureRect icon = Icon(__instance);
            TextureRect outline = Outline(__instance);
            if (hidden)
            {
                icon.Texture = ResourceLoader.Load<Texture2D>(UnknownIconPath, null, ResourceLoader.CacheMode.Reuse);
                outline.Texture = ResourceLoader.Load<Texture2D>(UnknownOutlinePath, null, ResourceLoader.CacheMode.Reuse);
            }

            icon.FlipV = hidden;
            icon.Modulate = hidden ? ShroudedItem.Tint : Colors.White;
            outline.FlipV = hidden;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to shade a map node: {ex.Message}");
        }
    }
}

/// <summary>
/// The map legend highlights every node of the hovered type. A hidden node must not give itself away, so it only reacts to
/// the "?" entry, like the upside-down "?" it shows.
/// </summary>
[HarmonyPatch(typeof(NNormalMapPoint), "OnHighlightPointType")]
internal static class ShadowMapLegendPatch
{
    private static readonly AccessTools.FieldRef<NNormalMapPoint, IRunState> RunState = SafeRef.Field<NNormalMapPoint, IRunState>("_runState");

    private static void Prefix(NNormalMapPoint __instance, ref MapPointType pointType)
    {
        try
        {
            if (RunState(__instance) is not { } runState || __instance.Point is not { } point || !ShadowPath.IsHidden(runState, point))
            {
                return;
            }

            // The original highlights when pointType equals the node's real type: map "?" to it, anything else to a type it isn't.
            pointType = pointType == MapPointType.Unknown
                ? point.PointType
                : point.PointType == MapPointType.Boss ? MapPointType.Unassigned : MapPointType.Boss;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to adjust a hidden node's legend highlight: {ex.Message}");
        }
    }
}

/// <summary>A shaded card shows no hover tips (they would name its keywords and effects).</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class ShadedCardHoverTipsPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        try
        {
            if (ShadedCards.IsShaded(__instance))
            {
                __result = Enumerable.Empty<IHoverTip>();
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Shaded card hover tips", ex);
        }
    }
}

/// <summary>
/// Shadow Corruption: the turn's shaded card is picked just before the turn-start hand draw (among the hand and the cards about
/// to be drawn), so it is already shaded on its way into the hand. Runs in the synced turn-start flow.
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Draw), typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool))]
internal static class ShadowHandDrawPatch
{
    private static void Prefix(decimal count, Player player, bool fromHandDraw)
    {
        try
        {
            // ShadedCards decides how many (zone fight, Last-Light Lantern); usually none.
            if (fromHandDraw)
            {
                ShadedCards.ShadeBeforeHandDraw(player, count);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to shade a card before the hand draw: {ex.Message}");
        }
    }
}

/// <summary>
/// Hovering an enemy lists its intents; while its intent is hidden from the local player, only the Unknown intent tip is listed
/// (its power tips stay).
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.HoverTips), MethodType.Getter)]
internal static class ShadowCreatureHoverTipsPatch
{
    private static void Postfix(Creature __instance, ref IEnumerable<IHoverTip> __result)
    {
        try
        {
            if (!__instance.IsMonster || !ShadowSight.ShouldHide(__instance) || __instance.CombatState is not { } combat)
            {
                return;
            }

            var tips = new List<IHoverTip> { ShadowSight.Hidden.GetHoverTip(combat.Allies, __instance) };
            foreach (PowerModel power in __instance.Powers)
            {
                foreach (IHoverTip tip in power.HoverTips)
                {
                    tips.MegaTryAddingTip(tip);
                }
            }

            __result = tips;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to hide an enemy's intent tips: {ex.Message}");
        }
    }
}

/// <summary>
/// Hidden intents stay hidden from other mods too: while an enemy's intent is hidden from the local player, the game's own
/// intent damage API (AttackIntent.GetSingleDamage, which GetTotalDamage of every attack intent builds on) reports 0. In
/// vanilla it is display-only (intent labels, animations and tips, already swapped for the "?" intent here); the attack itself
/// deals its real damage. Mods that sum incoming damage through it (e.g. an incoming-damage counter) then show nothing, without
/// any mod-specific code. The intent type (whether an enemy attacks) is left alone, since cards and relics read it for gameplay.
/// </summary>
[HarmonyPatch(typeof(AttackIntent), nameof(AttackIntent.GetSingleDamage))]
internal static class ShadowIntentDamagePatch
{
    private static void Postfix(Creature owner, ref int __result)
    {
        try
        {
            if (__result > 0 && ShadowSight.ShouldHide(owner))
            {
                __result = 0;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to hide an intent's damage: {ex.Message}");
        }
    }
}
