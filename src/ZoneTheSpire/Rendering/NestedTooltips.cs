using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.BloodRain;
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Core.ForgottenEmpire;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Core.Midas;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Core.Tooltips;
using ZoneTheSpire.Run;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Crusader Kings style nested tooltips (research spike). A hover tip set that mentions one of the mod's keywords gets a small
/// ring that fills while it is shown; once full the set is locked: it stays when the mouse leaves its owner, until the mouse
/// leaves the set itself. In a locked set, gold keywords are links: hovering one opens that keyword's tip beside it, which
/// locks the same way and can be nested further. Every other hover tip set is left exactly as the game made it. Local UI only.
/// With a controller, holding a button locks and focuses a tip instead (see NestedTooltips.Controller.cs).
/// </summary>
internal static partial class NestedTooltips
{
    /// <summary>Spike switch: false restores the flat tooltips (keyword tips listed beside the main tip).</summary>
    public static readonly bool Enabled = true;

    private const float LockSeconds = 0.9f;
    private const float GraceSeconds = 0.35f;
    private const float HitPadding = 10f;
    private const float RingSize = 30f;
    private const float RingInset = 5f;
    private const string RingName = "ZoneTheSpireNestedLockRing";

    private static readonly FieldInfo? ActiveField = AccessTools.Field(typeof(NHoverTipSet), "_activeHoverTips");
    private static readonly FieldInfo? TextContainerField = AccessTools.Field(typeof(NHoverTipSet), "_textHoverTipContainer");
    private static readonly FieldInfo? FollowOwnerField = AccessTools.Field(typeof(NHoverTipSet), "_followOwner");
    private static readonly FieldInfo? OwnerField = AccessTools.Field(typeof(NHoverTipSet), "_owner");

    /// <summary>How long a hovered thing's ring progress survives the game re-creating its tip (it does on every card played).</summary>
    private const float CarryOverSeconds = 1.0f;

    private static (Control? Owner, float Progress, ulong Ticks) _lastProgress;

    private sealed class Level
    {
        public required NHoverTipSet Set;
        public Control? Owner; // what the game shows this set for (the hovered creature, card, ...)
        public NHoverTipSet? Replacement; // the game re-created this locked set's tip (a refresh); ours stays, that one is hidden
        public Control? Anchor; // our own owner control, for nested levels
        public string? Keyword; // the keyword whose link opened this level
        public bool Locked;
        public bool Detached; // the game let go of it (its owner was unhovered) and we keep it alive
        public float Progress;
        public float Grace;
        public string? HoveredKeyword;
        public ColorRect? Ring;
        public Control? FirstTip; // the ring sits on this tip's top-right corner
        public readonly List<RichTextLabel> Labels = new();
        public readonly Dictionary<RichTextLabel, string> LinkedTexts = new(); // label text with links, before any highlight
        public readonly Dictionary<RichTextLabel, Control> TipOf = new(); // each label's own tip (nested tips open beside it)
        public readonly List<string> Keywords = new(); // linked keywords in reading order (controller cycling)
        public bool Focused; // controller focus: the border is drawn and the movement buttons cycle its keywords
        public int Selected = -1;
        public TextureRect? Icon; // controller button icon in the corner
        public Panel? Border;
    }

    private static readonly List<Level> Chain = new();
    private static bool _forceRemove;
    private static bool _spawningChild;
    private static bool _plainOnly; // set while creating a tip set that must stay a normal, non-nested one
    private static Level? _pendingChild; // the level AfterInit built for the nested set OpenChild is creating
    private static bool _ticking;
    private static ulong _lastTicks;
    private static ShaderMaterial? _ringMaterial;

    /// <summary>Keyword name -> its tip. Mod glossary keywords, their powers (with icons) and Doom, which Hallowed names.</summary>
    private static Dictionary<string, Func<IHoverTip>>? _tips;

    /// <summary>The language changed: keyword names (the link texts) are rebuilt on next use.</summary>
    internal static void ResetKeywords()
    {
        _tips = null;
        _modKeywords = null;
    }

    /// <summary>Keywords whose mention makes a set nested: the mod's own. Vanilla ones (Doom) only link inside nested sets.</summary>
    private static HashSet<string>? _modKeywords;

    private static Dictionary<string, Func<IHoverTip>> Tips
    {
        get
        {
            if (_tips != null)
            {
                return _tips;
            }

            var tips = new Dictionary<string, Func<IHoverTip>>();
            foreach (ZoneKeyword keyword in BiomeRegistry.All.SelectMany(biome => biome.Keywords))
            {
                ZoneKeyword captured = keyword;
                tips.TryAdd(keyword.Name, () => new HoverTip(ZoneTheSpireModifier.KeywordTitle(captured), captured.Description));
            }

            // Keywords that are powers open the power's own tip (its icon and text).
            tips[HallowedText.Hallowed] = () => HoverTipFactory.FromPower<HallowedPower>();
            tips[HallowedText.Blasphemer] = () => HoverTipFactory.FromPower<BlasphemerPower>();
            tips[HallowedText.Zealous] = () => HoverTipFactory.FromPower<ZealousPower>();
            tips[MirrorText.Mirrored] = () => HoverTipFactory.FromPower<MirroredPower>();
            tips[MirrorText.Reflection] = () => HoverTipFactory.FromPower<ReflectionPower>();
            tips[BloodRainText.BloodDrinker] = () => HoverTipFactory.FromPower<BloodDrinkerPower>();
            tips[HallsOfMidasText.TouchOfMidas] = () => HoverTipFactory.FromPower<TouchOfMidasPower>();
            tips[PhantasmalText.Phantasm] = () => HoverTipFactory.FromPower<PhantasmPower>();
            tips[FerrosandText.Magnetized] = () => HoverTipFactory.FromPower<MagnetizedPower>();
            tips[FerrosandText.Ferroform] = () => HoverTipFactory.FromPower<FerroformPower>();
            tips[ForgottenEmpireText.ForgottenStatue] = () => HoverTipFactory.FromPower<ForgottenStatuePower>();
            tips[ForgottenEmpireText.Marbled] = () => HoverTipFactory.FromPower<MarbledPower>();
            tips[ForgottenEmpireText.Polishing] = () => HoverTipFactory.FromPower<PolishingPower>();
            tips[ShadowText.ShadowBrutality] = () => HoverTipFactory.FromPower<ShadowBrutalityPower>();
            tips[DevasText.Samsara] = () => HoverTipFactory.FromPower<SamsaraPower>();
            tips[DevasText.DevasBlessing] = () => HoverTipFactory.FromPower<DevasBlessingPower>();
            tips[FrostText.Hoarfrost] = () => HoverTipFactory.FromPower<HoarfrostPower>();

            // Vanilla powers the mod's text names (by their title in the current language): they link inside mod tips, but never
            // make a tip set nested.
            tips[VanillaTitle<DoomPower>()] = () => HoverTipFactory.FromPower<DoomPower>();
            tips[VanillaTitle<StrengthPower>()] = () => HoverTipFactory.FromPower<StrengthPower>();
            tips[VanillaTitle<WeakPower>()] = () => HoverTipFactory.FromPower<WeakPower>();
            tips[VanillaTitle<VulnerablePower>()] = () => HoverTipFactory.FromPower<VulnerablePower>();
            _modKeywords = BiomeRegistry.All.SelectMany(biome => biome.Keywords).Select(keyword => keyword.Name).ToHashSet();
            return _tips = tips;
        }
    }

    private static string VanillaTitle<T>() where T : MegaCrit.Sts2.Core.Models.PowerModel =>
        global::ZoneTheSpire.Run.Localization.ModLocalization.GameText("powers", MegaCrit.Sts2.Core.Models.ModelDb.GetId<T>().Entry + ".title");

    private static HashSet<string> ModKeywords
    {
        get
        {
            _ = Tips;
            return _modKeywords!;
        }
    }

    /// <summary>After the game fills a tip set: make it nested when it is about a mod keyword (or is one of our nested levels).</summary>
    internal static void AfterInit(NHoverTipSet set)
    {
        if (!Enabled || _plainOnly)
        {
            return;
        }

        try
        {
            if (TextContainerField?.GetValue(set) is not Control container)
            {
                return;
            }

            var tips = new List<(Control Tip, RichTextLabel Label, string? Title)>();
            foreach (Control tip in container.GetChildren().OfType<Control>())
            {
                if (tip.GetNodeOrNull<RichTextLabel>("%Description") is { } label)
                {
                    tips.Add((tip, label, tip.GetNodeOrNull<Label>("%Title") is { Visible: true } title ? title.Text : null));
                }
            }

            if (!_spawningChild && !tips.Any(tip => NestedTooltipText.Mentions(tip.Title, tip.Label.Text, ModKeywords)))
            {
                return;
            }

            Control? owner = OwnerField?.GetValue(set) as Control;
            if (!_spawningChild && owner != null && Chain.Count > 0 && Chain[0].Locked && Chain[0].Owner == owner
                && GodotObject.IsInstanceValid(Chain[0].Set))
            {
                // The game refreshed the tip of the thing whose tip is locked (a card was played): keep the locked one and its
                // nested tips, and hide the new copy. While that copy exists, the thing is still hovered.
                set.Visible = false;
                Chain[0].Replacement = set;
                Chain[0].Detached = false;
                return;
            }

            if (!_spawningChild)
            {
                // A new top-level nested tip replaces whatever chain was open.
                CloseFrom(0);
            }

            var level = new Level { Set = set, Owner = owner };
            foreach ((Control tip, RichTextLabel label, string? title) in tips)
            {
                string linked = NestedTooltipText.Linkify(label.Text, Tips.Keys, title);
                if (linked != label.Text)
                {
                    label.Text = linked;
                }

                label.MetaUnderlined = true;
                level.Labels.Add(label);
                level.LinkedTexts[label] = label.Text;
                level.Keywords.AddRange(NestedTooltipText.LinkedKeywords(label.Text).Where(keyword => !level.Keywords.Contains(keyword)));
                level.TipOf[label] = tip;
                RichTextLabel source = label;
                label.MetaHoverStarted += meta => OnKeywordHover(level, meta.AsString(), source);
                label.MetaHoverEnded += meta => OnKeywordUnhover(level, meta.AsString());
            }

            if (tips.Count > 0)
            {
                level.FirstTip = tips[0].Tip;
                level.Ring = CreateRing(set);
                CreateControllerVisuals(level, set);
                PlaceRing(level);
            }

            if (!_spawningChild)
            {
                // The same thing's tip was re-created a moment ago (the game refreshes it when a card is played): keep filling.
                if (owner != null && _lastProgress.Owner == owner
                    && (Time.GetTicksMsec() - _lastProgress.Ticks) / 1000f < CarryOverSeconds)
                {
                    SetProgress(level, _lastProgress.Progress);
                }

                Chain.Add(level);
            }
            else
            {
                _pendingChild = level;
            }

            EnsureTicking();
        }
        catch (Exception ex)
        {
            Log.Warn($"Nested tooltips failed to prepare a tip set: {ex}");
        }
    }

    /// <summary>
    /// The tips of the registered keywords a text names as [gold]Name[/gold], in reading order, each once (not
    /// <paramref name="exclude"/>). For surfaces that have no tips of their own, like campfire options.
    /// </summary>
    internal static List<IHoverTip> TipsNamedIn(string text, string? exclude = null) =>
        NestedTooltipText.LinkedKeywords(NestedTooltipText.Linkify(text, Tips.Keys, exclude))
            .Select(keyword => Tips[keyword]())
            .ToList();

    /// <summary>Shows a tip set that stays a normal one (no ring, no locking, no links).</summary>
    internal static NHoverTipSet? CreatePlain(Control owner, IEnumerable<IHoverTip> tips)
    {
        _plainOnly = true;
        try
        {
            return NHoverTipSet.CreateAndShow(owner, tips);
        }
        finally
        {
            _plainOnly = false;
        }
    }

    /// <summary>Whether a controller is driving the UI (nested tips then lock by holding Peek).</summary>
    internal static bool UsingController => ControllerActive;

    /// <summary>Before the game removes a tip set: a locked set in our chain stays (we take it over), unless we are closing it.</summary>
    internal static bool BeforeRemove(Control owner)
    {
        if (_forceRemove || ActiveField?.GetValue(null) is not Dictionary<Control, NHoverTipSet> active
            || !active.TryGetValue(owner, out NHoverTipSet? set))
        {
            return true;
        }

        Level? replaced = Chain.FirstOrDefault(l => l.Replacement == set);
        if (replaced != null)
        {
            // The thing whose tip is locked is no longer hovered: the locked tip now lives on its own.
            replaced.Replacement = null;
            replaced.Detached = true;
            return true;
        }

        Level? level = Chain.FirstOrDefault(l => l.Set == set);
        if (level == null || !level.Locked)
        {
            if (level != null)
            {
                if (Chain.IndexOf(level) == 0)
                {
                    _lastProgress = (level.Owner, level.Progress, Time.GetTicksMsec());
                }

                CloseFrom(Chain.IndexOf(level), removeSets: false);
            }

            return true;
        }

        active.Remove(owner);
        level.Detached = true;
        return false;
    }

    /// <summary>The game clears every tip (screen changes): close our chain too.</summary>
    internal static void BeforeClear()
    {
        CloseFrom(0);
    }

    private static void OnKeywordHover(Level level, string meta, RichTextLabel? source = null)
    {
        if (NestedTooltipText.KeywordOf(meta) is not { } keyword || !level.Locked || !Chain.Contains(level) || ControllerActive)
        {
            return;
        }

        level.HoveredKeyword = keyword;
        int index = Chain.IndexOf(level);
        if (index + 1 < Chain.Count)
        {
            if (Chain[index + 1].Keyword == keyword)
            {
                return;
            }

            CloseFrom(index + 1);
        }

        OpenChild(level, keyword, source);
    }

    private static void OnKeywordUnhover(Level level, string meta)
    {
        if (level.HoveredKeyword == NestedTooltipText.KeywordOf(meta))
        {
            level.HoveredKeyword = null;
        }
    }

    private static void OpenChild(Level parent, string keyword, RichTextLabel? source = null)
    {
        if (!Tips.TryGetValue(keyword, out Func<IHoverTip>? make) || NGame.Instance?.HoverTipsContainer is not { } host)
        {
            return;
        }

        // Beside the tip holding the hovered link, not the whole stack (a creature can show several columns of tips).
        Rect2 parentRect = source != null && parent.TipOf.TryGetValue(source, out Control? sourceTip) && GodotObject.IsInstanceValid(sourceTip)
            ? sourceTip.GetGlobalRect()
            : RectOf(parent) ?? new Rect2(parent.Set.GetGlobalMousePosition(), Vector2.Zero);
        // Beside the hovered link with the mouse; level with the parent's top with a controller.
        float mouseY = ControllerActive ? parentRect.Position.Y + 24f : parent.Set.GetGlobalMousePosition().Y;
        bool fitsRight = parentRect.End.X + 360f + 10f < parent.Set.GetViewportRect().Size.X;
        var anchor = new Control { Name = "ZoneTheSpireNestedAnchor", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(10f, 10f) };
        host.AddChild(anchor);
        anchor.GlobalPosition = fitsRight ? new Vector2(parentRect.End.X - 5f, mouseY - 24f) : new Vector2(parentRect.Position.X - 5f, mouseY - 24f);

        _spawningChild = true;
        _pendingChild = null;
        try
        {
            NHoverTipSet? set = NHoverTipSet.CreateAndShow(anchor, make(), fitsRight ? HoverTipAlignment.Right : HoverTipAlignment.Left);
            if (set == null || _pendingChild == null)
            {
                anchor.QueueFree();
                return;
            }

            _pendingChild.Anchor = anchor;
            _pendingChild.Keyword = keyword;
            Chain.Add(_pendingChild);
        }
        finally
        {
            _spawningChild = false;
            _pendingChild = null;
        }
    }

    private static void EnsureTicking()
    {
        if (_ticking || Engine.GetMainLoop() is not SceneTree tree)
        {
            return;
        }

        _ticking = true;
        _lastTicks = Time.GetTicksMsec();
        tree.ProcessFrame += Tick;
    }

    private static void Tick()
    {
        try
        {
            ulong now = Time.GetTicksMsec();
            float delta = Math.Min((now - _lastTicks) / 1000f, 0.1f);
            _lastTicks = now;

            // Drop levels the game (or a screen change) already freed, with everything nested in them.
            int dead = Chain.FindIndex(level => !GodotObject.IsInstanceValid(level.Set));
            if (dead >= 0)
            {
                CloseFrom(dead);
            }

            if (Chain.Count == 0)
            {
                return;
            }

            foreach (Level level in Chain)
            {
                PlaceRing(level);
            }

            if (ControllerActive)
            {
                TickController(delta);
                return;
            }

            LeaveControllerMode();
            int deepest = -1;
            for (int i = Chain.Count - 1; i >= 0; i--)
            {
                if (RectOf(Chain[i]) is { } rect && rect.Grow(HitPadding).HasPoint(Chain[i].Set.GetGlobalMousePosition()))
                {
                    deepest = i;
                    break;
                }
            }

            // Nested levels, deepest first: keep one while the mouse is on it, on a deeper level, or on the link that opened it.
            for (int i = Chain.Count - 1; i >= 1; i--)
            {
                Level level = Chain[i];
                bool onLink = Chain[i - 1].HoveredKeyword == level.Keyword;
                if (deepest >= i || onLink)
                {
                    level.Grace = 0f;
                }
                else if (!level.Locked || (level.Grace += delta) >= GraceSeconds)
                {
                    CloseFrom(i);
                    continue;
                }

                if (!level.Locked && onLink)
                {
                    Advance(level, delta);
                }
            }

            Level top = Chain[0];
            if (top.Replacement != null && !GodotObject.IsInstanceValid(top.Replacement))
            {
                top.Replacement = null;
                top.Detached = true;
            }

            if (!top.Detached)
            {
                // Still owned by the game: shown means its owner is hovered.
                if (!top.Locked)
                {
                    Advance(top, delta);
                }
            }
            else if (deepest >= 0)
            {
                top.Grace = 0f;
            }
            else if ((top.Grace += delta) >= GraceSeconds)
            {
                CloseFrom(0);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Nested tooltips tick failed: {ex}");
            CloseFrom(0);
        }
    }

    /// <summary>Fills a level's ring; locks it when full. Returns whether it is locked now.</summary>
    private static bool Advance(Level level, float delta)
    {
        SetProgress(level, Math.Min(1f, level.Progress + delta / LockSeconds));
        if (level.Progress >= 1f && !level.Locked)
        {
            Lock(level);
        }

        return level.Locked;
    }

    private static void SetProgress(Level level, float progress)
    {
        level.Progress = progress;
        (level.Ring?.Material as ShaderMaterial)?.SetShaderParameter("progress", progress);
    }

    private static void Lock(Level level)
    {
        level.Locked = true;
        (level.Ring?.Material as ShaderMaterial)?.SetShaderParameter("locked", 1f);
        FollowOwnerField?.SetValue(level.Set, false);

        // Locked sets take the mouse: their links report hovers and the things underneath stop reacting.
        if (TextContainerField?.GetValue(level.Set) is Control container)
        {
            foreach (Control tip in container.GetChildren().OfType<Control>())
            {
                tip.MouseFilter = Control.MouseFilterEnum.Stop;
            }
        }

        foreach (RichTextLabel label in level.Labels.Where(GodotObject.IsInstanceValid))
        {
            label.MouseFilter = Control.MouseFilterEnum.Stop;
        }
    }

    /// <summary>Closes the level at <paramref name="index"/> and every level nested in it.</summary>
    private static void CloseFrom(int index, bool removeSets = true)
    {
        for (int i = Chain.Count - 1; i >= index && i >= 0; i--)
        {
            Level level = Chain[i];
            Chain.RemoveAt(i);
            if (i > 0)
            {
                Chain[i - 1].HoveredKeyword = Chain[i - 1].HoveredKeyword == level.Keyword ? null : Chain[i - 1].HoveredKeyword;
            }

            if (!removeSets)
            {
                continue;
            }

            try
            {
                if (level.Anchor != null)
                {
                    _forceRemove = true;
                    NHoverTipSet.Remove(level.Anchor);
                    _forceRemove = false;
                    if (GodotObject.IsInstanceValid(level.Anchor))
                    {
                        level.Anchor.QueueFree();
                    }
                }
                else if ((level.Detached || level.Replacement != null) && GodotObject.IsInstanceValid(level.Set))
                {
                    if (level.Replacement != null && GodotObject.IsInstanceValid(level.Replacement))
                    {
                        // The thing is still hovered: its refreshed tip takes over.
                        level.Replacement.Visible = true;
                    }

                    level.Set.QueueFree();
                }
                else if (GodotObject.IsInstanceValid(level.Set))
                {
                    // Still the game's: hand it back as a normal tip.
                    foreach (Control extra in new Control?[] { level.Ring, level.Icon, level.Border })
                    {
                        if (extra != null && GodotObject.IsInstanceValid(extra))
                        {
                            extra.QueueFree();
                        }
                    }

                    foreach ((RichTextLabel label, string text) in level.LinkedTexts.Where(pair => GodotObject.IsInstanceValid(pair.Key)))
                    {
                        label.Text = text;
                    }

                    foreach (RichTextLabel label in level.Labels.Where(GodotObject.IsInstanceValid))
                    {
                        label.MouseFilter = Control.MouseFilterEnum.Ignore;
                    }
                }
            }
            catch (Exception ex)
            {
                _forceRemove = false;
                Log.Warn($"Nested tooltips failed to close a level: {ex}");
            }
        }

        AfterChainChanged();
    }

    /// <summary>Screen rectangle covered by a set's text tips.</summary>
    private static Rect2? RectOf(Level level)
    {
        if (!GodotObject.IsInstanceValid(level.Set) || TextContainerField?.GetValue(level.Set) is not Control container)
        {
            return null;
        }

        Rect2? rect = null;
        foreach (Control tip in container.GetChildren().OfType<Control>().Where(tip => tip.Visible))
        {
            Rect2 tipRect = tip.GetGlobalRect();
            rect = rect is { } r ? r.Merge(tipRect) : tipRect;
        }

        return rect;
    }

    /// <summary>
    /// The ring is a child of the tip set, not of the tip: the tip is a container and would stretch it over its whole area.
    /// It is placed on the first tip's top-right corner every frame, since the game positions the tips after Init.
    /// </summary>
    private static ColorRect CreateRing(NHoverTipSet set)
    {
        _ringMaterial ??= new ShaderMaterial { Shader = new Shader { Code = RingShader } };
        var ring = new ColorRect
        {
            Name = RingName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(RingSize, RingSize),
            Material = (ShaderMaterial)_ringMaterial.Duplicate(),
        };
        set.AddChild(ring);
        return ring;
    }

    private static void PlaceRing(Level level)
    {
        if (level.Ring is not { } ring || !GodotObject.IsInstanceValid(ring) || level.FirstTip is not { } tip || !GodotObject.IsInstanceValid(tip))
        {
            return;
        }

        Rect2 rect = tip.GetGlobalRect();
        if (ControllerActive)
        {
            PlaceControllerVisuals(level, ring, rect);
            return;
        }

        ring.Visible = true;
        ring.Size = new Vector2(RingSize, RingSize);
        ring.GlobalPosition = new Vector2(rect.End.X - RingSize - RingInset, rect.Position.Y + RingInset);
        (ring.Material as ShaderMaterial)?.SetShaderParameter("backdrop", 0.75f);
        HideControllerVisuals(level);
    }

    // A thick gold ring on a dark disc that fills clockwise from the top, with a bright leading edge and a soft glow; once
    // the tip is locked the ring is full and a gold dot fills its middle.
    private const string RingShader = @"
shader_type canvas_item;
uniform float progress = 0.0;
uniform float locked = 0.0;
uniform float backdrop = 0.75;

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float a = atan(p.x, -p.y);
    if (a < 0.0) { a += 6.2831853; }
    float frac = a / 6.2831853;

    float disc = 1.0 - smoothstep(0.90, 0.98, r);
    float ring = smoothstep(0.48, 0.56, r) * (1.0 - smoothstep(0.80, 0.88, r));
    float filled = step(frac, progress);
    float head = (1.0 - smoothstep(0.0, 0.06, abs(frac - progress))) * step(0.001, progress) * (1.0 - locked);
    float glow = exp(-pow((r - 0.68) / 0.18, 2.0)) * filled * 0.45;
    float dot_ = (1.0 - smoothstep(0.26, 0.34, r)) * locked;

    vec3 gold = vec3(1.0, 0.84, 0.36);
    vec3 track = vec3(0.45, 0.38, 0.22);
    vec3 col = vec3(0.06, 0.05, 0.04);
    col = mix(col, track, ring * (1.0 - filled));
    col = mix(col, gold, ring * filled);
    col += vec3(1.0, 0.95, 0.75) * head * ring;
    col += gold * glow;
    col = mix(col, gold, dot_);
    COLOR = vec4(col, max(disc * backdrop, max(ring, dot_)));
}";
}

[HarmonyPatch(typeof(NHoverTipSet), "Init")]
internal static class NestedTooltipInitPatch
{
    private static void Postfix(NHoverTipSet __instance) => NestedTooltips.AfterInit(__instance);
}

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.Remove))]
internal static class NestedTooltipRemovePatch
{
    private static bool Prefix(Control owner)
    {
        try
        {
            return NestedTooltips.BeforeRemove(owner);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Nested tooltips (remove)", ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.Clear))]
internal static class NestedTooltipClearPatch
{
    private static void Prefix()
    {
        try
        {
            NestedTooltips.BeforeClear();
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Nested tooltips (clear)", ex);
        }
    }
}
