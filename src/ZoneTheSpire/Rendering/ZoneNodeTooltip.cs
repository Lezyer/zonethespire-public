using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run;
using ZoneTheSpire.Run.Effects;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Extra hover tip on map nodes affected by their zone, followed by a tip for each zone glossary keyword it names. Uses its own child anchor as the tip owner, because the game keys
/// hover tips by owner and the node may already show its history tip. Local UI only.
/// </summary>
internal static class ZoneNodeTooltip
{
    private const string AnchorName = "ZoneTheSpireTipAnchor";

    private static readonly FieldInfo? RunStateField = AccessTools.Field(typeof(NMapPoint), "_runState");
    private static bool _warnedMissingField;

    public static void Show(NMapPoint mapPoint)
    {
        try
        {
            if (RunStateField == null)
            {
                if (!_warnedMissingField)
                {
                    _warnedMissingField = true;
                    Log.Warn("NMapPoint._runState not found; zone node tooltips disabled.");
                }

                return;
            }

            if (RunStateField.GetValue(mapPoint) is not IRunState runState || mapPoint.Point is not MapPoint point)
            {
                return;
            }

            // Already visited nodes (drawn with the black circle) keep only the game's own history tip.
            if (mapPoint.State == MapPointState.Traveled)
            {
                return;
            }

            ZoneContext? context = ZoneContext.For(runState, runState.CurrentActIndex, point.coord, point.PointType);
            if (context == null)
            {
                return;
            }

            // A hidden Shadow Corruption node doesn't reveal its kind: the tip lists what every node kind does there instead.
            string? description = ShadowPath.IsHidden(runState, point)
                ? ZoneTooltip.BuildHiddenDescription(context.Biome, BiomeColors.TooltipHex(context.Biome), ZoneEffectRegistry.ResolveTooltipToken)
                : ZoneTooltip.BuildDescription(
                    context.Biome,
                    context.NodeKind,
                    BiomeColors.TooltipHex(context.Biome),
                    ZoneEffectRegistry.ResolveTooltipToken);
            if (description == null)
            {
                return;
            }

            Control anchor = GetOrCreateAnchor(mapPoint);
            NHoverTipSet.Remove(anchor);
            var tips = new List<IHoverTip> { new HoverTip(ZoneTheSpireModifier.ZoneTitle(context.Biome), description) };
            // With nested tooltips the keywords are links inside the zone tip instead of tips listed beside it.
            if (!NestedTooltips.Enabled)
            {
                tips.AddRange(ZoneTooltip.KeywordsIn(context.Biome, description).Select(keyword => (IHoverTip)new HoverTip(ZoneTheSpireModifier.KeywordTitle(keyword), keyword.Description)));
            }

            NHoverTipSet? tip = NHoverTipSet.CreateAndShow(anchor, tips);
            if (tip == null)
            {
                return;
            }

            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(tip) && GodotObject.IsInstanceValid(anchor))
                {
                    tip.SetAlignment(anchor, HoverTip.GetHoverTipAlignment(mapPoint));
                }
            }).CallDeferred();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to show zone node tooltip: {ex}");
        }
    }

    public static void Hide(NMapPoint mapPoint)
    {
        try
        {
            if (mapPoint.GetNodeOrNull<Control>(AnchorName) is { } anchor)
            {
                NHoverTipSet.Remove(anchor);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to hide zone node tooltip: {ex}");
        }
    }

    private static Control GetOrCreateAnchor(NMapPoint mapPoint)
    {
        if (mapPoint.GetNodeOrNull<Control>(AnchorName) is { } existing)
        {
            existing.Size = mapPoint.Size;
            return existing;
        }

        var anchor = new Control
        {
            Name = AnchorName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = mapPoint.Size,
        };
        mapPoint.AddChild(anchor);
        return anchor;
    }
}
