using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Blinding Hallows creature visuals: a golden halo, glow and rising motes on Zealous enemies, and the Judgement burst (a
/// sequence in HolyVisuals.Judgement.cs) when Hallowed kills. Local rendering only; never affects gameplay.
/// </summary>
internal static partial class HolyVisuals
{
    private const string HaloName = "ZoneTheSpireHolyHalo";
    private const int MaxLayoutRetries = 3;

    private static Texture2D? _ring;
    private static Texture2D? _mote;

    public static void AttachHalo(Creature creature, int attempt = 0)
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
                    Callable.From(() => AttachHalo(creature, attempt + 1)).CallDeferred();
                }

                return;
            }

            if (node.GetNodeOrNull(HaloName) != null)
            {
                return;
            }

            (Vector2 centre, Vector2 size) = Layout(node);
            var root = new Node2D { Name = HaloName, Position = centre };

            var glow = new Sprite2D
            {
                Texture = Mote(),
                Scale = new Vector2(size.X, size.Y) / 64f * 1.1f,
                Modulate = new Color(1f, 0.85f, 0.45f, 0.18f),
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            };
            root.AddChild(glow);

            var halo = new Sprite2D
            {
                Texture = Ring(),
                Position = new Vector2(0f, -size.Y * 0.62f),
                Scale = new Vector2(size.X / 128f * 0.55f, size.X / 128f * 0.18f),
                Modulate = new Color(1f, 0.9f, 0.55f, 0.9f),
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            };
            root.AddChild(halo);
            Tween bob = halo.CreateTween().SetLoops();
            bob.TweenProperty(halo, "position:y", halo.Position.Y - 6f, 1.4).SetTrans(Tween.TransitionType.Sine);
            bob.TweenProperty(halo, "position:y", halo.Position.Y, 1.4).SetTrans(Tween.TransitionType.Sine);

            root.AddChild(new CpuParticles2D
            {
                Amount = 14,
                Lifetime = 2.2,
                Preprocess = 2.2,
                Texture = Mote(),
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                EmissionRectExtents = new Vector2(size.X * 0.45f, size.Y * 0.4f),
                Direction = new Vector2(0f, -1f),
                Spread = 20f,
                Gravity = new Vector2(0f, -30f),
                InitialVelocityMin = 10f,
                InitialVelocityMax = 30f,
                ScaleAmountMin = 0.06f,
                ScaleAmountMax = 0.14f,
                ColorRamp = Fade(new Color(1f, 0.9f, 0.55f), 0.85f),
                Emitting = true,
            });

            node.AddChild(root);
            node.MoveChild(root, 0);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the holy halo: {ex}");
        }
    }

    private static (Vector2 Centre, Vector2 Size) Layout(NCreature node)
    {
        Control bounds = node.Visuals.Bounds;
        Vector2 size = bounds.Size;
        if (size.X <= 1f || size.Y <= 1f)
        {
            size = new Vector2(200f, 250f);
        }

        float scale = Math.Max(0.0001f, node.GetGlobalTransform().Scale.Y);
        return ((bounds.GlobalPosition - node.GlobalPosition) / scale + size / 2f, size);
    }

    private static Gradient Fade(Color color, float alpha) => new()
    {
        Offsets = new[] { 0f, 0.15f, 0.7f, 1f },
        Colors = new[] { new Color(color, 0f), new Color(color, alpha), new Color(color, alpha * 0.6f), new Color(color, 0f) },
    };

    private static Texture2D Mote() => _mote ??= new GradientTexture2D
    {
        Width = 64,
        Height = 64,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(0.5f, 0f),
        Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) } },
    };

    /// <summary>A soft white ring (drawn once into an image), squashed into an ellipse for the halo.</summary>
    private static Texture2D Ring()
    {
        if (_ring != null)
        {
            return _ring;
        }

        const int size = 128;
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).Length() / (size / 2f);
                float alpha = Mathf.Clamp(1f - Math.Abs(d - 0.8f) / 0.12f, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }

        return _ring = ImageTexture.CreateFromImage(image);
    }
}
