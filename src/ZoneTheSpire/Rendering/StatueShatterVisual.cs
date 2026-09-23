using System;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Forgotten Empire: marble chips fly off a statue enemy when its Marbled takes damage, and when Marbled breaks the statue
/// shell explodes: a bright flash, a burst of tumbling marble chunks falling under gravity and a cloud of stone dust. Local
/// rendering only; never affects gameplay.
/// </summary>
internal static class StatueShatterVisual
{
    private static Texture2D? _chunk;
    private static Texture2D? _dust;
    private static Gradient? _chunkFade;
    private static Gradient? _chunkShades;
    private static Gradient? _dustFade;

    public static void Shatter(Creature creature) => Burst(creature, shatter: true);

    public static void Chip(Creature creature) => Burst(creature, shatter: false);

    private static void Burst(Creature creature, bool shatter)
    {
        try
        {
            if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
            {
                return;
            }

            Control bounds = node.Visuals.Bounds;
            Vector2 size = bounds.Size;
            if (size.X <= 1f || size.Y <= 1f)
            {
                size = new Vector2(200f, 250f);
            }

            float scale = Math.Max(0.0001f, node.GetGlobalTransform().Scale.Y);
            Vector2 center = (bounds.GlobalPosition - node.GlobalPosition) / scale + size / 2f;

            var chunks = new CpuParticles2D
            {
                OneShot = true,
                Explosiveness = 0.92f,
                Amount = shatter ? 48 : 9,
                Lifetime = shatter ? 1.5 : 0.75,
                Texture = Chunk(),
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                EmissionRectExtents = shatter ? size * 0.32f : size * 0.18f,
                Direction = new Vector2(0f, -1f),
                Spread = shatter ? 95f : 70f,
                Gravity = new Vector2(0f, 1500f),
                InitialVelocityMin = shatter ? 280f : 150f,
                InitialVelocityMax = shatter ? 760f : 340f,
                AngleMin = 0f,
                AngleMax = 360f,
                AngularVelocityMin = -620f,
                AngularVelocityMax = 620f,
                ScaleAmountMin = shatter ? 0.55f : 0.3f,
                ScaleAmountMax = shatter ? 1.5f : 0.75f,
                ColorRamp = ChunkFade(),
                ColorInitialRamp = ChunkShades(),
                Emitting = false,
            };
            Spawn(node, chunks, center, shatter ? 2.2 : 1.2);

            if (!shatter)
            {
                return;
            }

            var dust = new CpuParticles2D
            {
                OneShot = true,
                Explosiveness = 0.85f,
                Amount = 24,
                Lifetime = 1.3,
                Texture = Dust(),
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                EmissionRectExtents = size * 0.28f,
                Direction = new Vector2(0f, -1f),
                Spread = 180f,
                Gravity = new Vector2(0f, -40f),
                InitialVelocityMin = 40f,
                InitialVelocityMax = 170f,
                DampingMin = 70f,
                DampingMax = 110f,
                ScaleAmountMin = 1.6f,
                ScaleAmountMax = 3.4f,
                ColorRamp = DustFade(),
                Emitting = false,
            };
            Spawn(node, dust, center, 2.0);

            Node2D body = node.Visuals.Body;
            Color original = body.Modulate;
            body.Modulate = new Color(original.R * 2.2f, original.G * 2.2f, original.B * 2.2f, original.A);
            // The breaking hit already plays the block break sound (MarbleSounds.Hit).
            body.CreateTween().TweenProperty(body, "modulate", original, 0.45).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to play a statue shatter effect: {ex}");
        }
    }

    private static void Spawn(NCreature node, CpuParticles2D particles, Vector2 position, double lifetime)
    {
        particles.Position = position;
        node.AddChild(particles);
        particles.Emitting = true;
        node.GetTree().CreateTimer(lifetime).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(particles))
            {
                particles.QueueFree();
            }
        };
    }

    // A small faceted chip: a diagonal light-to-shadow gradient reads as a lit broken face.
    private static Texture2D Chunk() => _chunk ??= new GradientTexture2D
    {
        Width = 14,
        Height = 10,
        Fill = GradientTexture2D.FillEnum.Linear,
        FillFrom = new Vector2(0f, 0f),
        FillTo = new Vector2(1f, 1f),
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.48f, 0.52f, 1f },
            Colors = new[] { Colors.White, new Color(0.9f, 0.9f, 0.88f), new Color(0.66f, 0.66f, 0.68f), new Color(0.5f, 0.5f, 0.53f) },
        },
    };

    private static Texture2D Dust() => _dust ??= new GradientTexture2D
    {
        Width = 48,
        Height = 48,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(0.5f, 0f),
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.5f, 1f },
            Colors = new[] { new Color(1f, 1f, 1f, 0.55f), new Color(0.9f, 0.9f, 0.88f, 0.25f), new Color(0.85f, 0.85f, 0.83f, 0f) },
        },
    };

    private static Gradient ChunkFade() => _chunkFade ??= new Gradient
    {
        Offsets = new[] { 0f, 0.75f, 1f },
        Colors = new[] { Colors.White, Colors.White, new Color(1f, 1f, 1f, 0f) },
    };

    private static Gradient ChunkShades() => _chunkShades ??= new Gradient
    {
        Offsets = new[] { 0f, 1f },
        Colors = new[] { new Color(1f, 0.99f, 0.96f), new Color(0.7f, 0.7f, 0.72f) },
    };

    private static Gradient DustFade() => _dustFade ??= new Gradient
    {
        Offsets = new[] { 0f, 0.2f, 1f },
        Colors = new[] { new Color(1f, 1f, 1f, 0f), new Color(0.95f, 0.94f, 0.92f, 0.8f), new Color(0.9f, 0.9f, 0.88f, 0f) },
    };
}
