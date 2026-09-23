using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Judgement: the Hallowed death, built like the game's Doom death (the creature freezes on its hurt frame, its own effect
/// replaces the normal death animation, and a screen overlay accompanies it). About two seconds:
/// <list type="number">
/// <item>0 - 0.75 s: the creature freezes mid-hurt, trembles and brightens while a halo descends onto it, light gathers inward
/// and a golden glare floods the screen.</item>
/// <item>0.75 s: impact. A pillar of light slams down with a white flash, a shockwave ring on the ground and a burst of rays
/// behind the creature.</item>
/// <item>0.85 - 1.9 s: a creature that leaves the fight dissolves into light from a burning golden edge while motes stream
/// upward and the halo ascends; others (players, reviving enemies) just glow back down and die normally.</item>
/// </list>
/// Local rendering only; never affects gameplay.
/// </summary>
internal static partial class HolyVisuals
{
    private const string OverlayName = "ZoneTheSpireJudgementOverlay";
    private const double DescentSeconds = 0.75;
    private const double DissolveSeconds = 1.05;

    private const string PillarShader = @"shader_type canvas_item;
render_mode blend_add;
uniform float life = 0.0;
void fragment() {
    float core = 1.0 - smoothstep(0.0, 0.5, abs(UV.x - 0.5));
    // Slams down in the first tenth of its life, holds, then thins and fades.
    float reach = smoothstep(0.0, 0.1, life);
    float fall = step(1.0 - reach, 1.0 - UV.y);
    float fade = 1.0 - smoothstep(0.45, 1.0, life);
    float thin = mix(1.0, 0.35, smoothstep(0.3, 1.0, life));
    float flicker = 0.9 + 0.1 * sin(TIME * 40.0 + UV.y * 30.0);
    float beam = pow(core, 4.0 / thin) * 1.4 + 0.45 * pow(core, 0.9 / thin);
    COLOR = vec4(vec3(1.0, 0.95, 0.78) * beam * fall * fade * flicker, 1.0);
}
";

    private const string RaysShader = @"shader_type canvas_item;
render_mode blend_add;
uniform float life = 0.0;
void fragment() {
    vec2 d = UV - 0.5;
    float r = length(d) * 2.0;
    float a = atan(d.y, d.x);
    float rays = pow(0.5 + 0.5 * sin(a * 14.0 + TIME * 1.5), 6.0) + 0.6 * pow(0.5 + 0.5 * sin(a * 5.0 - TIME * 0.8), 8.0);
    float reach = smoothstep(0.0, 0.25, life);
    float radial = (1.0 - smoothstep(0.1 * reach, reach + 0.001, r)) * smoothstep(0.0, 0.08, r);
    float fade = 1.0 - smoothstep(0.5, 1.0, life);
    float core = (1.0 - smoothstep(0.0, 0.35, r)) * 0.8;
    COLOR = vec4(vec3(1.0, 0.88, 0.55) * (rays * radial + core * reach) * fade, 1.0);
}
";

    private const string OverlayShader = @"shader_type canvas_item;
render_mode blend_add;
uniform float intensity = 0.0;
void fragment() {
    vec2 uv = UV;
    vec2 d = uv - vec2(0.5, -0.2);
    float a = atan(d.x, d.y);
    float rays = pow(0.5 + 0.5 * sin(a * 18.0 + TIME * 0.6), 5.0) * (1.0 - smoothstep(0.2, 1.3, length(d)));
    float edge = smoothstep(0.3, 0.8, length((uv - 0.5) * vec2(1.0, 1.4)));
    float wash = 0.10;
    COLOR = vec4(vec3(1.0, 0.9, 0.62) * (rays * 0.28 + edge * 0.35 + wash) * intensity, 1.0);
}
";

    // The body burns away into light: value noise over the mesh's local position (Spine atlas UVs jump between pieces), a
    // whitened body, and a bright golden rim along the dissolving edge.
    private const string DissolveShader = @"shader_type canvas_item;
uniform float progress = 0.0;
uniform float glow = 0.0;
varying vec2 local_pos;
void vertex() {
    local_pos = VERTEX;
}
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}
float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
void fragment() {
    vec4 base = COLOR;
    float n = vnoise(local_pos / 42.0) * 0.65 + vnoise(local_pos / 12.0) * 0.35;
    float edge = progress - n;
    vec3 lit = mix(base.rgb, vec3(1.0, 0.96, 0.84), clamp(glow, 0.0, 1.0));
    float rim = 1.0 - smoothstep(0.0, 0.09, -edge);
    vec3 color = mix(lit, vec3(1.0, 0.8, 0.32) * 2.4, rim);
    float alpha = edge > 0.0 ? 0.0 : base.a;
    COLOR = vec4(color, alpha);
}
";

    private static Shader? _pillarShader;
    private static Shader? _raysShader;
    private static Shader? _overlayShader;
    private static Shader? _dissolveShader;

    /// <summary>
    /// Plays Judgement on a creature Hallowed is about to kill. When <paramref name="disappears"/> (a monster that really leaves
    /// the fight, the same rule Doom uses) the effect becomes the creature's death animation: it dissolves, its node is removed
    /// afterwards, and the returned task is set as its DeathAnimationTask so the normal death animation is skipped.
    /// </summary>
    public static Task Judgement(Creature creature, bool disappears)
    {
        try
        {
            if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
            {
                return Task.CompletedTask;
            }

            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            (Vector2 centre, Vector2 size) = Layout(node);
            Node2D body = node.Visuals.Body;
            Vector2 home = body.Position;
            Color original = body.Modulate;

            ShowOverlay();
            Freeze(node, creature);
            if (disappears)
            {
                node.DisableInteractionForDeath();
                node.AnimDisableUi();
                NCombatRoom.Instance.RemoveCreatureNode(node);
                node.DeathAnimationTask = done.Task;
            }

            // 1. Descent: halo comes down, light gathers inward, the body trembles and brightens.
            Vector2 haloScale = new(size.X / 128f * 0.6f, size.X / 128f * 0.2f);
            var halo = new Sprite2D
            {
                Texture = Ring(),
                Position = centre + new Vector2(0f, -size.Y * 1.25f),
                Scale = haloScale * 1.8f,
                Modulate = new Color(1f, 0.9f, 0.55f, 0f),
                Material = Additive(),
            };
            node.AddChild(halo);

            var gather = new CpuParticles2D
            {
                Amount = 40,
                Lifetime = 0.6,
                Texture = Mote(),
                Material = Additive(),
                Position = centre,
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
                EmissionSphereRadius = Math.Max(size.X, size.Y) * 0.85f,
                RadialAccelMin = -900f,
                RadialAccelMax = -700f,
                ScaleAmountMin = 0.1f,
                ScaleAmountMax = 0.22f,
                ColorRamp = Fade(new Color(1f, 0.93f, 0.7f), 0.9f),
                Emitting = true,
            };
            node.AddChild(gather);

            Tween descent = node.CreateTween().SetParallel(true);
            descent.TweenProperty(halo, "modulate:a", 1f, 0.3);
            descent.TweenProperty(halo, "position:y", centre.Y - size.Y * 0.62f, DescentSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            descent.TweenProperty(halo, "scale", haloScale, DescentSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            descent.TweenProperty(body, "modulate", new Color(original.R * 1.7f, original.G * 1.6f, original.B * 1.25f, original.A), DescentSeconds);

            Tween tremble = body.CreateTween().SetLoops(12);
            tremble.TweenProperty(body, "position", home + new Vector2(3f, -1f), 0.03);
            tremble.TweenProperty(body, "position", home + new Vector2(-3f, 1f), 0.03);

            descent.Chain().TweenCallback(Callable.From(() =>
            {
                try
                {
                    gather.Emitting = false;
                    body.Position = home;
                    Impact(node, centre, size, halo, body, original, disappears, done);
                }
                catch (Exception ex)
                {
                    Log.Warn($"Judgement impact failed: {ex}");
                    done.TrySetResult();
                }
            }));

            // Never leave a death animation hanging, whatever happens to the node.
            node.GetTree().CreateTimer(DescentSeconds + DissolveSeconds + 1.0).Timeout += () => done.TrySetResult();
            return done.Task;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to play Judgement: {ex}");
            return Task.CompletedTask;
        }
    }

    private static void Impact(NCreature node, Vector2 centre, Vector2 size, Sprite2D halo, Node2D body, Color original, bool disappears, TaskCompletionSource done)
    {
        // The pillar slams down.
        var pillarMaterial = new ShaderMaterial { Shader = _pillarShader ??= new Shader { Code = PillarShader } };
        var pillar = new ColorRect
        {
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = pillarMaterial,
            Size = new Vector2(size.X * 0.95f, 2600f),
            Position = centre + new Vector2(-size.X * 0.475f, -2600f + size.Y * 0.55f),
        };
        node.AddChild(pillar);
        Tween pillarTween = pillar.CreateTween();
        pillarTween.TweenMethod(Callable.From<float>(t => pillarMaterial.SetShaderParameter("life", t)), 0f, 1f, 1.2);
        pillarTween.TweenCallback(Callable.From(pillar.QueueFree));

        // Rays burst behind the creature.
        var raysMaterial = new ShaderMaterial { Shader = _raysShader ??= new Shader { Code = RaysShader } };
        float raysSize = Math.Max(size.X, size.Y) * 2.6f;
        var rays = new ColorRect
        {
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = raysMaterial,
            Size = new Vector2(raysSize, raysSize),
            Position = centre - new Vector2(raysSize, raysSize) / 2f,
        };
        node.AddChild(rays);
        node.MoveChild(rays, 0);
        Tween raysTween = rays.CreateTween();
        raysTween.TweenMethod(Callable.From<float>(t => raysMaterial.SetShaderParameter("life", t)), 0f, 1f, 1.4);
        raysTween.TweenCallback(Callable.From(rays.QueueFree));

        // A shockwave ring spreads across the ground.
        var wave = new Sprite2D
        {
            Texture = Ring(),
            Position = centre + new Vector2(0f, size.Y * 0.48f),
            Scale = new Vector2(size.X / 128f * 0.3f, size.X / 128f * 0.08f),
            Modulate = new Color(1f, 0.92f, 0.62f, 1f),
            Material = Additive(),
        };
        node.AddChild(wave);
        Tween waveTween = wave.CreateTween().SetParallel(true);
        waveTween.TweenProperty(wave, "scale", new Vector2(size.X / 128f * 3.2f, size.X / 128f * 0.8f), 0.7).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        waveTween.TweenProperty(wave, "modulate:a", 0f, 0.7).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        waveTween.Chain().TweenCallback(Callable.From(wave.QueueFree));

        // A burst of light motes.
        var burst = new CpuParticles2D
        {
            OneShot = true,
            Explosiveness = 0.95f,
            Amount = 60,
            Lifetime = 1.3,
            Texture = Mote(),
            Material = Additive(),
            Position = centre,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = size * 0.3f,
            Direction = new Vector2(0f, -1f),
            Spread = 180f,
            Gravity = new Vector2(0f, -90f),
            InitialVelocityMin = 150f,
            InitialVelocityMax = 420f,
            DampingMin = 90f,
            DampingMax = 160f,
            ScaleAmountMin = 0.12f,
            ScaleAmountMax = 0.34f,
            ColorRamp = Fade(new Color(1f, 0.95f, 0.78f), 1f),
            Emitting = false,
        };
        node.AddChild(burst);
        burst.Emitting = true;

        // White flash on the body.
        body.Modulate = new Color(original.R * 3.4f, original.G * 3.2f, original.B * 2.6f, original.A);

        if (!disappears)
        {
            Tween settle = node.CreateTween().SetParallel(true);
            settle.TweenProperty(body, "modulate", original, 0.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            settle.TweenProperty(halo, "modulate:a", 0f, 0.8);
            settle.Chain().TweenCallback(Callable.From(() =>
            {
                halo.QueueFree();
                done.TrySetResult();
            }));
            return;
        }

        // 3. The body dissolves into light while motes stream upward and the halo ascends.
        var dissolve = new ShaderMaterial { Shader = _dissolveShader ??= new Shader { Code = DissolveShader } };
        dissolve.SetShaderParameter("glow", 0.9f);
        // Set directly on this node: it has already left the combat room's creature list, so a lookup by creature would miss it.
        if (node.Visuals.SpineBody != null)
        {
            node.Visuals.SpineBody.SetNormalMaterial(dissolve);
        }
        else
        {
            node.Visuals.Body.Material = dissolve;
        }

        var stream = new CpuParticles2D
        {
            Amount = 70,
            Lifetime = 1.2,
            Texture = Mote(),
            Material = Additive(),
            Position = centre,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = size * 0.42f,
            Direction = new Vector2(0f, -1f),
            Spread = 18f,
            Gravity = new Vector2(0f, -160f),
            InitialVelocityMin = 40f,
            InitialVelocityMax = 110f,
            ScaleAmountMin = 0.08f,
            ScaleAmountMax = 0.2f,
            ColorRamp = Fade(new Color(1f, 0.88f, 0.5f), 1f),
            Emitting = true,
        };
        node.AddChild(stream);

        Tween ascend = node.CreateTween().SetParallel(true);
        ascend.TweenProperty(body, "modulate", new Color(original.R * 1.6f, original.G * 1.5f, original.B * 1.2f, original.A), 0.25);
        ascend.TweenMethod(Callable.From<float>(p => dissolve.SetShaderParameter("progress", p)), 0f, 1.05f, DissolveSeconds)
            .SetDelay(0.1).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        ascend.TweenProperty(halo, "position:y", centre.Y - size.Y * 1.4f, DissolveSeconds + 0.1).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        ascend.TweenProperty(halo, "modulate:a", 0f, DissolveSeconds + 0.1).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        ascend.Chain().TweenCallback(Callable.From(() => stream.Emitting = false));
        ascend.Chain().TweenInterval(0.6);
        ascend.Chain().TweenCallback(Callable.From(() =>
        {
            done.TrySetResult();
            if (GodotObject.IsInstanceValid(node))
            {
                node.QueueFree();
            }
        }));
    }

    /// <summary>Freezes the creature on its hurt frame, the way Doom does.</summary>
    private static void Freeze(NCreature node, Creature creature)
    {
        try
        {
            if (!node.HasSpineAnimation || !node.SpineAnimation.IsValid)
            {
                return;
            }

            node.SetAnimationTrigger("Hit");
            using MegaTrackEntry? track = node.SpineAnimation.GetCurrentTrack();
            if (track?.GetAnimationName() == "hurt")
            {
                track.SetTrackTime(creature.Monster?.HurtAnimationTrackOffsetForDoom ?? 0.1f);
                track.SetTimeScale(0f);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to freeze a creature for Judgement: {ex.Message}");
        }
    }

    /// <summary>One golden glare over the combat, shared by a whole batch of judgements.</summary>
    private static void ShowOverlay()
    {
        if (NCombatRoom.Instance?.CombatVfxContainer is not Control host || !GodotObject.IsInstanceValid(host))
        {
            return;
        }

        if (host.GetNodeOrNull<ColorRect>(OverlayName) is { } existing)
        {
            existing.Visible = true;
            return;
        }

        var material = new ShaderMaterial { Shader = _overlayShader ??= new Shader { Code = OverlayShader } };
        var overlay = new ColorRect { Name = OverlayName, Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore, Material = material };
        host.AddChild(overlay);
        overlay.GlobalPosition = Vector2.Zero;
        overlay.Size = host.GetViewportRect().Size;
        Tween tween = overlay.CreateTween();
        tween.TweenMethod(Callable.From<float>(v => material.SetShaderParameter("intensity", v)), 0f, 1f, 0.5).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(1.2);
        tween.TweenMethod(Callable.From<float>(v => material.SetShaderParameter("intensity", v)), 1f, 0f, 0.7);
        tween.TweenCallback(Callable.From(overlay.QueueFree));
    }

    private static CanvasItemMaterial Additive() => new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
}
