using System;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Ferrosand: Magnetized enemies levitate. The creature's body (NCreatureVisuals.Body, not the Visuals node the game moves for
/// shakes and deaths) is lifted a little and bobs slowly, and small electric sparks crackle in the gap below it. When the enemy
/// has used up its Magnetized hits for the turn it settles to the ground and the sparks die out; it rises again when they refill.
/// Hitboxes, intents and health bars stay where they are. Local rendering only; never affects gameplay.
/// </summary>
internal static class LevitationVisual
{
    private const string SparksName = "ZoneTheSpireMagneticSparks";
    private const float Lift = 26f;
    private const float Bob = 7f;
    private const double BobSeconds = 1.1;
    private const double SettleSeconds = 0.35;
    private const double RiseSeconds = 0.5;
    private const int MaxLayoutRetries = 3;

    private sealed class State
    {
        public Vector2 Rest;
        public Tween? Tween;
        public bool Grounded;
    }

    private static readonly ConditionalWeakTable<Node2D, State> States = new();
    private static Texture2D? _spark;
    private static Gradient? _fade;

    /// <summary>Starts levitating (first call) or rises again after <see cref="Ground"/>.</summary>
    public static void Attach(Creature creature, int attempt = 0) => Apply(creature, grounded: false, attempt);

    /// <summary>Settles a levitating creature to the ground and stops its sparks.</summary>
    public static void Ground(Creature creature) => Apply(creature, grounded: true, 0);

    private static void Apply(Creature creature, bool grounded, int attempt)
    {
        try
        {
            if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node || !GodotObject.IsInstanceValid(node))
            {
                return;
            }

            if (!node.IsInsideTree())
            {
                if (attempt < MaxLayoutRetries)
                {
                    Callable.From(() => Apply(creature, grounded, attempt + 1)).CallDeferred();
                }

                return;
            }

            Node2D body = node.Visuals.Body;
            if (!States.TryGetValue(body, out State? state))
            {
                if (grounded)
                {
                    return;
                }

                state = new State { Rest = body.Position, Grounded = true };
                States.Add(body, state);
                AddSparks(node);
            }

            if (state.Grounded == grounded)
            {
                return;
            }

            state.Grounded = grounded;
            state.Tween?.Kill();
            if (node.GetNodeOrNull<CpuParticles2D>(SparksName) is { } sparks)
            {
                sparks.Emitting = !grounded;
            }

            Tween tween = body.CreateTween();
            state.Tween = tween;
            if (grounded)
            {
                tween.TweenProperty(body, "position", state.Rest, SettleSeconds).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
                return;
            }

            Vector2 high = state.Rest + new Vector2(0f, -Lift - Bob);
            Vector2 low = state.Rest + new Vector2(0f, -Lift + Bob);
            tween.TweenProperty(body, "position", low, RiseSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenCallback(Callable.From(() => StartBobbing(body, state, high, low)));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to update a Magnetized enemy's levitation: {ex}");
        }
    }

    private static void StartBobbing(Node2D body, State state, Vector2 high, Vector2 low)
    {
        if (!GodotObject.IsInstanceValid(body) || state.Grounded)
        {
            return;
        }

        Tween bob = body.CreateTween().SetLoops();
        state.Tween = bob;
        bob.TweenProperty(body, "position", high, BobSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        bob.TweenProperty(body, "position", low, BobSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private static void AddSparks(NCreature node)
    {
        if (node.GetNodeOrNull(SparksName) != null)
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
        Vector2 topLeft = (bounds.GlobalPosition - node.GlobalPosition) / scale;
        var sparks = new CpuParticles2D
        {
            Name = SparksName,
            Amount = 26,
            Lifetime = 0.35,
            Texture = Spark(),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(size.X * 0.3f, 6f),
            Direction = new Vector2(0f, -1f),
            Spread = 180f,
            Gravity = Vector2.Zero,
            InitialVelocityMin = 30f,
            InitialVelocityMax = 110f,
            DampingMin = 60f,
            DampingMax = 120f,
            ScaleAmountMin = 0.25f,
            ScaleAmountMax = 0.7f,
            ColorRamp = Fade(),
            Emitting = false,
        };
        sparks.Position = new Vector2(topLeft.X + size.X / 2f, topLeft.Y + size.Y - Lift * 0.4f);
        node.AddChild(sparks);
    }

    private static Texture2D Spark() => _spark ??= new GradientTexture2D
    {
        Width = 24,
        Height = 24,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(0.5f, 0f),
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.35f, 1f },
            Colors = new[] { Colors.White, new Color(0.7f, 0.9f, 1f, 0.8f), new Color(0.5f, 0.8f, 1f, 0f) },
        },
    };

    private static Gradient Fade() => _fade ??= new Gradient
    {
        Offsets = new[] { 0f, 0.3f, 1f },
        Colors = new[] { new Color(0.85f, 0.95f, 1f, 1f), new Color(0.55f, 0.8f, 1f, 0.85f), new Color(0.4f, 0.65f, 1f, 0f) },
    };
}
