using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// A shadowy aura around a creature. Full (Shadow Brutality enemies), in four particle layers. Behind its body: dancing black-violet flames that
/// sway as they rise, and a violet glow licking up their edges. In front: slow shadow mist drifting around it, and violet embers
/// rising off it. Mild (the Last-Light Lantern holder): only a thin dark smoke behind the body and a faint mist around it, so
/// the player stays easy to read. Local rendering only; never affects gameplay. The layers are children of the creature node, so they go away
/// with it.
/// </summary>
internal static class ShadowAura
{
    private const string BackName = "ZoneTheSpireShadowAuraBack";
    private const string FrontName = "ZoneTheSpireShadowAuraFront";
    private const int MaxLayoutRetries = 3;

    private static Texture2D? _flame;
    private static Texture2D? _puff;

    public enum Strength
    {
        Full,
        Mild,
    }

    public static void Attach(Creature creature, Strength strength = Strength.Full, int attempt = 0)
    {
        try
        {
            if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node)
            {
                return;
            }

            if (!node.IsInsideTree())
            {
                if (attempt < MaxLayoutRetries)
                {
                    Callable.From(() => Attach(creature, strength, attempt + 1)).CallDeferred();
                }

                return;
            }

            if (node.GetNodeOrNull(BackName) != null)
            {
                return;
            }

            Control bounds = node.Visuals.Bounds;
            Vector2 size = bounds.Size;
            if (size.X <= 1f || size.Y <= 1f)
            {
                size = new Vector2(200f, 250f);
            }

            Vector2 centre = (bounds.GlobalPosition - node.GlobalPosition) / Math.Max(0.0001f, node.GetGlobalTransform().Scale.Y) + size / 2f;

            var back = new Node2D { Name = BackName, Position = centre };
            bool mild = strength == Strength.Mild;
            back.AddChild(Flames(size, mild));
            if (!mild)
            {
                back.AddChild(FlameGlow(size));
            }

            node.AddChild(back);
            // Children draw in order: first child is drawn first, so the flames sit behind the creature's body.
            node.MoveChild(back, 0);

            var front = new Node2D { Name = FrontName, Position = centre };
            front.AddChild(Mist(size, mild));
            if (!mild)
            {
                front.AddChild(Embers(size));
            }

            node.AddChild(front);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the shadow aura: {ex}");
        }
    }

    /// <summary>Black-violet flames rising from the lower body, swaying from side to side and shrinking as they rise.</summary>
    private static CpuParticles2D Flames(Vector2 size, bool mild) => new()
    {
        Amount = mild ? 16 : 46,
        Lifetime = 1.15,
        Preprocess = 1.2,
        Texture = Flame(),
        Position = new Vector2(0f, size.Y * 0.12f),
        EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
        EmissionRectExtents = new Vector2(size.X * 0.42f, size.Y * 0.32f),
        Direction = new Vector2(0f, -1f),
        Spread = 10f,
        Gravity = new Vector2(0f, -110f),
        InitialVelocityMin = 25f,
        InitialVelocityMax = 70f,
        TangentialAccelMin = -45f,
        TangentialAccelMax = 45f,
        AngleMin = -12f,
        AngleMax = 12f,
        ScaleAmountMin = mild ? 0.7f : 1.1f,
        ScaleAmountMax = mild ? 1.4f : 2.3f,
        ScaleAmountCurve = Shrink(),
        ColorRamp = Ramp(new Color(0.22f, 0.06f, 0.36f), new Color(0.05f, 0.0f, 0.1f), mild ? 0.38f : 0.75f),
        Emitting = true,
    };

    /// <summary>Fewer, larger additive flames in violet, so the dark flames glow along their edges.</summary>
    private static CpuParticles2D FlameGlow(Vector2 size) => new()
    {
        Amount = 18,
        Lifetime = 0.95,
        Preprocess = 1.0,
        Texture = Flame(),
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        Position = new Vector2(0f, size.Y * 0.15f),
        EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
        EmissionRectExtents = new Vector2(size.X * 0.45f, size.Y * 0.3f),
        Direction = new Vector2(0f, -1f),
        Spread = 14f,
        Gravity = new Vector2(0f, -95f),
        InitialVelocityMin = 20f,
        InitialVelocityMax = 55f,
        TangentialAccelMin = -60f,
        TangentialAccelMax = 60f,
        ScaleAmountMin = 1.6f,
        ScaleAmountMax = 2.8f,
        ScaleAmountCurve = Shrink(),
        ColorRamp = Ramp(new Color(0.55f, 0.2f, 0.95f), new Color(0.3f, 0.05f, 0.6f), 0.28f),
        Emitting = true,
    };

    /// <summary>Slow, faint shadow mist curling around the body, in front of it.</summary>
    private static CpuParticles2D Mist(Vector2 size, bool mild) => new()
    {
        Amount = mild ? 8 : 18,
        Lifetime = 3.2,
        Preprocess = 3.2,
        Texture = Puff(),
        EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
        EmissionRectExtents = new Vector2(size.X * 0.5f, size.Y * 0.42f),
        Direction = new Vector2(0f, -1f),
        Spread = 180f,
        Gravity = new Vector2(0f, -8f),
        InitialVelocityMin = 6f,
        InitialVelocityMax = 18f,
        TangentialAccelMin = -12f,
        TangentialAccelMax = 12f,
        ScaleAmountMin = 1.6f,
        ScaleAmountMax = 3.2f,
        ColorRamp = Ramp(new Color(0.12f, 0.03f, 0.2f), new Color(0.03f, 0.0f, 0.06f), mild ? 0.16f : 0.3f),
        Emitting = true,
    };

    /// <summary>Small bright violet embers drifting up and swirling off the body.</summary>
    private static CpuParticles2D Embers(Vector2 size) => new()
    {
        Amount = 16,
        Lifetime = 1.8,
        Preprocess = 1.8,
        Texture = Puff(),
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        Position = new Vector2(0f, size.Y * 0.1f),
        EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
        EmissionRectExtents = new Vector2(size.X * 0.45f, size.Y * 0.35f),
        Direction = new Vector2(0f, -1f),
        Spread = 25f,
        Gravity = new Vector2(0f, -45f),
        InitialVelocityMin = 15f,
        InitialVelocityMax = 45f,
        TangentialAccelMin = -70f,
        TangentialAccelMax = 70f,
        ScaleAmountMin = 0.07f,
        ScaleAmountMax = 0.16f,
        ColorRamp = Ramp(new Color(0.85f, 0.55f, 1f), new Color(0.5f, 0.15f, 0.9f), 0.9f),
        Emitting = true,
    };

    /// <summary>Fades in quickly, holds, and fades out, shifting from <paramref name="start"/> to <paramref name="end"/>.</summary>
    private static Gradient Ramp(Color start, Color end, float alpha) => new()
    {
        Offsets = new[] { 0f, 0.18f, 0.6f, 1f },
        Colors = new[] { new Color(start, 0f), new Color(start, alpha), new Color(end, alpha * 0.7f), new Color(end, 0f) },
    };

    private static Curve Shrink()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 0.7f));
        curve.AddPoint(new Vector2(0.25f, 1f));
        curve.AddPoint(new Vector2(1f, 0.25f));
        return curve;
    }

    /// <summary>A soft teardrop flame tongue (white, alpha-shaped), pointing up.</summary>
    private static Texture2D Flame()
    {
        if (_flame != null)
        {
            return _flame;
        }

        const int width = 48;
        const int height = 80;
        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int y = 0; y < height; y++)
        {
            // t: 0 at the tip (top), 1 at the base (bottom).
            float t = (y + 0.5f) / height;
            float halfWidth = 0.5f * Mathf.Pow(t, 0.7f) * Mathf.Sqrt(Math.Max(0f, 1f - Mathf.Pow(Math.Max(0f, t - 0.72f) / 0.28f, 2f)));
            for (int x = 0; x < width; x++)
            {
                float dx = Math.Abs((x + 0.5f) / width - 0.5f);
                float alpha = halfWidth <= 0f ? 0f : Mathf.Clamp(1f - dx / halfWidth, 0f, 1f);
                alpha = Mathf.SmoothStep(0f, 1f, alpha) * Mathf.SmoothStep(0f, 0.25f, t);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        return _flame = ImageTexture.CreateFromImage(image);
    }

    private static Texture2D Puff() => _puff ??= new GradientTexture2D
    {
        Width = 64,
        Height = 64,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(0.5f, 0f),
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 1f },
            Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) },
        },
    };
}
