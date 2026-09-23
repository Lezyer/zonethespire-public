using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Blood Rain: a slow, drifting deep crimson mist around a creature (soft particles over its body). Local rendering only;
/// never affects gameplay. The mist is a child of the creature node, so it goes away with it.
/// </summary>
internal static class CrimsonMist
{
    private const string NodeName = "ZoneTheSpireCrimsonMist";
    private const int MaxLayoutRetries = 3;
    private static readonly Color Crimson = new(0.45f, 0.02f, 0.06f);

    private static Texture2D? _puff;
    private static Gradient? _fade;

    public static void Attach(Creature creature, int attempt = 0)
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
                    Callable.From(() => Attach(creature, attempt + 1)).CallDeferred();
                }

                return;
            }

            if (node.GetNodeOrNull(NodeName) != null)
            {
                return;
            }

            Control bounds = node.Visuals.Bounds;
            Vector2 size = bounds.Size;
            if (size.X <= 1f || size.Y <= 1f)
            {
                size = new Vector2(200f, 250f);
            }

            var mist = new CpuParticles2D
            {
                Name = NodeName,
                Amount = 32,
                Lifetime = 2.6,
                Preprocess = 2.6,
                Texture = Puff(),
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                EmissionRectExtents = new Vector2(size.X * 0.42f, size.Y * 0.38f),
                Direction = new Vector2(0f, -1f),
                Spread = 35f,
                Gravity = new Vector2(0f, -10f),
                InitialVelocityMin = 4f,
                InitialVelocityMax = 16f,
                ScaleAmountMin = 0.9f,
                ScaleAmountMax = 2.2f,
                ColorRamp = Fade(),
                Emitting = true,
            };
            mist.Position = (bounds.GlobalPosition - node.GlobalPosition) / Math.Max(0.0001f, node.GetGlobalTransform().Scale.Y) + size / 2f;
            node.AddChild(mist);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the crimson mist: {ex}");
        }
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

    private static Gradient Fade() => _fade ??= new Gradient
    {
        Offsets = new[] { 0f, 0.35f, 1f },
        Colors = new[] { new Color(Crimson, 0f), new Color(Crimson, 0.42f), new Color(Crimson, 0f) },
    };
}
