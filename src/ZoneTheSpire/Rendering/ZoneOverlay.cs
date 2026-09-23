using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Geometry;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Run;
using NVector2 = System.Numerics.Vector2;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Draws zones as smooth filled, outlined blobs hugging their member nodes, a ring around each member node, and a
/// biome name label kept clear of node icons. The overlay is the first child of NMapScreen._points: it shares the
/// point nodes' coordinate space, scrolls/fades with them and draws behind them.
/// </summary>
internal static class ZoneOverlay
{
    private const string OverlayName = "ZoneTheSpireOverlay";
    private const float FallbackPointSize = 78f;
    private const float IconObstaclePadding = 6f;
    private const float RingGap = 6f;
    private const int RingSegments = 40;
    private const int LabelFontSize = 24;
    private static readonly NVector2 LabelSize = new(250f, 38f);

    /// <summary>The font of card titles (card scene TitleLabel).</summary>
    private const string CardTitleFontPath = "res://themes/kreon_bold_glyph_space_one.tres";

    private static Font? _cardTitleFont;
    private static bool _triedCardTitleFont;

    private static readonly FieldInfo? RunStateField = AccessTools.Field(typeof(NMapScreen), "_runState");
    private static readonly FieldInfo? PointsField = AccessTools.Field(typeof(NMapScreen), "_points");
    private static readonly FieldInfo? PointDictionaryField = AccessTools.Field(typeof(NMapScreen), "_mapPointDictionary");
    private static bool _warnedMissingFields;

    private sealed record ZoneStyle(Color Base, Color Fill, Color Outline, float FillAlpha, float OutlineAlpha, float LabelAlpha);

    /// <summary>A zone's outline and the node centres it was built from.</summary>
    private sealed record CachedOutline(NVector2[] Centers, Vector2[] Polygon);

    /// <summary>
    /// Outlines by zone id, valid for <see cref="_outlineZones"/>. Building an outline is expensive (over a second for a zone
    /// covering the whole map) and the map is refreshed at every room start and end, so outlines are only rebuilt when the zones
    /// change (ZoneService hands out a new zone list only then: generation, loading, zone_map) or a member node moved (a new
    /// map layout).
    /// </summary>
    private static readonly Dictionary<string, CachedOutline> Outlines = new(StringComparer.Ordinal);

    private static IReadOnlyList<Zone>? _outlineZones;

    /// <summary>Between runs (RunLifecycle): drops the last run's outlines.</summary>
    internal static void Reset()
    {
        Outlines.Clear();
        _outlineZones = null;
    }

    public static void RefreshDeferred(NMapScreen screen)
    {
        try
        {
            Callable.From(() => Refresh(screen)).CallDeferred();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to schedule zone overlay refresh: {ex}");
        }
    }

    public static void Refresh(NMapScreen screen)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(screen) || !FieldsAvailable())
            {
                return;
            }

            if (PointsField!.GetValue(screen) is not Control points || !GodotObject.IsInstanceValid(points))
            {
                return;
            }

            RemoveFrom(points);
            if (RunStateField!.GetValue(screen) is not RunState runState ||
                PointDictionaryField!.GetValue(screen) is not Dictionary<MapCoord, NMapPoint> pointNodes ||
                pointNodes.Count == 0)
            {
                return;
            }

            IReadOnlyList<Zone> zones = ZoneService.GetZones(runState, runState.CurrentActIndex);
            if (!ReferenceEquals(zones, _outlineZones))
            {
                Outlines.Clear();
                _outlineZones = zones;
            }

            if (zones.Count == 0)
            {
                return;
            }

            var overlay = new Control
            {
                Name = OverlayName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            points.AddChild(overlay);
            points.MoveChild(overlay, 0);

            int currentRow = runState.CurrentMapCoord?.row ?? -1;
            List<LabelBox> iconObstacles = pointNodes.Values
                .Where(node => GodotObject.IsInstanceValid(node))
                .Select(IconBox)
                .ToList();
            List<Zone> ordered = zones.OrderBy(zone => zone.Id, StringComparer.Ordinal).ToList();

            // Pass 1: shapes and rings for every zone; pass 2: labels, so no zone's fill can cover another zone's label.
            var labelJobs = new List<(Zone Zone, BiomeDefinition Biome, ZoneStyle Style, NVector2 Anchor)>();
            foreach (Zone zone in ordered)
            {
                if (DrawZoneShape(overlay, zone, pointNodes, currentRow) is { } drawn)
                {
                    labelJobs.Add((zone, drawn.Biome, drawn.Style, drawn.Anchor));
                }
            }

            var usedLabels = new List<LabelBox>();
            foreach (var job in labelJobs)
            {
                DrawLabel(overlay, job.Zone, job.Biome, job.Style, job.Anchor, usedLabels, iconObstacles);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to draw zone overlay: {ex}");
        }
    }

    public static void Remove(NMapScreen screen)
    {
        try
        {
            if (GodotObject.IsInstanceValid(screen) && PointsField?.GetValue(screen) is Control points && GodotObject.IsInstanceValid(points))
            {
                RemoveFrom(points);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to remove zone overlay: {ex}");
        }
    }

    private static bool FieldsAvailable()
    {
        if (RunStateField != null && PointsField != null && PointDictionaryField != null)
        {
            return true;
        }

        if (!_warnedMissingFields)
        {
            _warnedMissingFields = true;
            Log.Warn("NMapScreen fields (_runState/_points/_mapPointDictionary) not found; zone overlay disabled.");
        }

        return false;
    }

    private static void RemoveFrom(Control points)
    {
        Node? existing = points.GetNodeOrNull(OverlayName);
        if (existing != null)
        {
            points.RemoveChild(existing);
            existing.QueueFree();
        }
    }

    private static Vector2 PointSize(NMapPoint node) =>
        node.Size.X > 0f && node.Size.Y > 0f ? node.Size : new Vector2(FallbackPointSize, FallbackPointSize);

    private static LabelBox IconBox(NMapPoint node)
    {
        Vector2 size = PointSize(node);
        return new LabelBox(
            node.Position.X - IconObstaclePadding,
            node.Position.Y - IconObstaclePadding,
            size.X + IconObstaclePadding * 2f,
            size.Y + IconObstaclePadding * 2f);
    }

    private static (BiomeDefinition Biome, ZoneStyle Style, NVector2 Anchor)? DrawZoneShape(
        Control overlay, Zone zone, Dictionary<MapCoord, NMapPoint> pointNodes, int currentRow)
    {
        BiomeDefinition? biome = BiomeRegistry.Get(zone.BiomeId);
        if (biome == null)
        {
            return null;
        }

        var members = new List<(GridCoord Coord, NVector2 Center, float IconRadius)>();
        foreach (GridCoord coord in zone.Nodes)
        {
            if (pointNodes.TryGetValue(new MapCoord(coord.Col, coord.Row), out NMapPoint? node) && GodotObject.IsInstanceValid(node))
            {
                Vector2 size = PointSize(node);
                Vector2 center = node.Position + size * 0.5f;
                members.Add((coord, new NVector2(center.X, center.Y), MathF.Max(size.X, size.Y) * 0.5f));
            }
        }

        if (members.Count == 0)
        {
            return null;
        }

        var links = new List<(int A, int B)>();
        for (int i = 0; i < members.Count; i++)
        {
            for (int j = i + 1; j < members.Count; j++)
            {
                if (members[i].Coord.IsAdjacentTo(members[j].Coord))
                {
                    links.Add((i, j));
                }
            }
        }

        Vector2[] polygon = Outline(zone.Id, members.Select(m => m.Center).ToArray(), links);
        if (polygon.Length < 3)
        {
            return null;
        }

        bool passed = currentRow >= 0 && zone.Nodes.Max(coord => coord.Row) < currentRow;
        // A custom fill (Blinding Hallows's white) needs more opacity than a coloured wash to read on the parchment map.
        bool customFill = biome.MapFillColorHex != null;
        var style = new ZoneStyle(
            BiomeColors.Base(biome),
            BiomeColors.Fill(biome),
            BiomeColors.Outline(biome),
            FillAlpha: customFill ? (passed ? 0.22f : 0.45f) : (passed ? 0.10f : 0.22f),
            OutlineAlpha: passed ? 0.35f : 0.75f,
            LabelAlpha: passed ? 0.55f : 1f);

        overlay.AddChild(new Polygon2D
        {
            Name = $"Fill_{zone.Id}",
            Polygon = polygon,
            Color = WithAlpha(style.Fill, style.FillAlpha),
        });
        overlay.AddChild(PolyLine($"Outline_{zone.Id}", polygon, 3f, WithAlpha(style.Outline, style.OutlineAlpha)));

        foreach (var member in members)
        {
            var ring = new Vector2[RingSegments];
            float ringRadius = member.IconRadius + RingGap;
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = MathF.PI * 2f * i / RingSegments;
                ring[i] = new Vector2(member.Center.X + MathF.Cos(angle) * ringRadius, member.Center.Y + MathF.Sin(angle) * ringRadius);
            }

            overlay.AddChild(PolyLine($"Ring_{zone.Id}_{member.Coord.Col}_{member.Coord.Row}", ring, 3f, WithAlpha(style.Outline, style.OutlineAlpha)));
        }

        NVector2 anchor = members.Aggregate(NVector2.Zero, (sum, m) => sum + m.Center) / members.Count;
        return (biome, style, anchor);
    }

    /// <summary>The zone's cached outline, built only when there is none for these node centres.</summary>
    private static Vector2[] Outline(string zoneId, NVector2[] centers, List<(int A, int B)> links)
    {
        if (Outlines.TryGetValue(zoneId, out CachedOutline? cached) && cached.Centers.SequenceEqual(centers))
        {
            return cached.Polygon;
        }

        Vector2[] polygon = BlobShape.Build(centers, links).Select(point => new Vector2(point.X, point.Y)).ToArray();
        Outlines[zoneId] = new CachedOutline(centers, polygon);
        return polygon;
    }

    private static void DrawLabel(
        Control overlay, Zone zone, BiomeDefinition biome, ZoneStyle style, NVector2 anchor, List<LabelBox> usedLabels, IReadOnlyList<LabelBox> iconObstacles)
    {
        LabelBox box = LabelLayout.Place(anchor, LabelSize, usedLabels, iconObstacles);
        var label = new Label
        {
            Name = $"Label_{zone.Id}",
            Text = biome.DisplayName,
            Position = new Vector2(box.X, box.Y),
            Size = new Vector2(box.Width, box.Height),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeColorOverride("font_color", WithAlpha(style.Base, style.LabelAlpha));
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, style.LabelAlpha));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        label.AddThemeFontSizeOverride("font_size", LabelFontSize);
        try
        {
            if (CardTitleFont() is { } font)
            {
                label.AddThemeFontOverride("font", font);
            }

            // Languages with their own fonts (CJK, Thai, Cyrillic) get the game's bold substitute, like card titles do.
            label.ApplyLocaleFontSubstitution(FontType.Bold, "font");
        }
        catch (Exception ex)
        {
            // Never lose the label over a font problem: it falls back to the default font.
            _cardTitleFont = null;
            Log.Warn($"Zone label font could not be applied; using the default font: {ex.Message}");
        }

        overlay.AddChild(label);
    }

    /// <summary>
    /// The card title font. The game can free font resources (e.g. when it swaps fonts), so a kept reference is only reused
    /// while still valid; otherwise it is loaded again (ResourceLoader returns the game's cached copy when there is one).
    /// </summary>
    private static Font? CardTitleFont()
    {
        if (_cardTitleFont != null && GodotObject.IsInstanceValid(_cardTitleFont))
        {
            return _cardTitleFont;
        }

        _cardTitleFont = null;
        try
        {
            _cardTitleFont = ResourceLoader.Load<Font>(CardTitleFontPath, null, ResourceLoader.CacheMode.Reuse);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to load the card title font for zone labels: {ex.Message}");
        }

        if (_cardTitleFont == null && !_triedCardTitleFont)
        {
            Log.Warn($"Card title font {CardTitleFontPath} not found; zone labels use the default font.");
        }

        _triedCardTitleFont = true;
        return _cardTitleFont;
    }

    private static Line2D PolyLine(string name, Vector2[] points, float width, Color color)
    {
        var line = new Line2D
        {
            Name = name,
            Width = width,
            Antialiased = true,
            Closed = true,
            DefaultColor = color,
        };
        foreach (Vector2 point in points)
        {
            line.AddPoint(point);
        }

        return line;
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
}
