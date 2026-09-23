using System;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Infestation: flies buzz around every enemy, the same flies as on Wriggling cards (same size, loops, speed, jitter and
/// flickering wings) at the same density: one fly per 220px cell over the enemy's bounds plus a margin, at least one. Each fly
/// loops around its own cell and never leaves it, so none is cut off. Drawn by one full-rect ColorRect shader just above the
/// creature's visuals (under its health bar and intent), ignoring the mouse; it fades out when the enemy dies. Wrigglers are
/// left alone. Local rendering only; never affects gameplay.
/// </summary>
internal static class FlySwarmVisual
{
    private const string NodeName = "ZoneTheSpireFlySwarm";
    private const float Cell = 220f;
    private const float Margin = 0.2f;
    private const int MaxLayoutRetries = 3;

    private const string ShaderCode = @"shader_type canvas_item;

uniform vec2 rect_size = vec2(440.0, 440.0);
uniform vec2 cells = vec2(2.0, 2.0);
uniform float cell = 220.0;

vec3 hash32(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return fract((p3.xxy + p3.yzz) * p3.zyx);
}

void fragment() {
    vec2 pixel = UV * rect_size;
    vec2 origin = (rect_size - cells * cell) * 0.5;
    vec2 id = floor((pixel - origin) / cell);
    if (id.x < 0.0 || id.y < 0.0 || id.x >= cells.x || id.y >= cells.y) {
        discard;
    }

    // One fly per cell, looping a wobbly figure-eight around a spot near the cell's middle with a fast jitter (the Wriggling
    // card's flies; the loop stays well inside the cell).
    vec3 h = hash32(id + vec2(3.7, 9.1));
    float fi = h.z * 10.0;
    vec2 home = origin + (id + 0.5) * cell + (h.xy - 0.5) * 12.0;
    float t = TIME * (0.9 + 0.5 * h.z) + h.x * 40.0;
    float radius = 28.0 + 26.0 * h.z;
    vec2 loop_offset = vec2(sin(t * 1.3) + 0.6 * sin(t * 2.9 + 1.0), cos(t * 1.1) + 0.5 * sin(t * 3.7 + 2.0)) * radius;
    vec2 buzz = vec2(sin(TIME * 23.0 + fi * 4.0), cos(TIME * 29.0 + fi * 7.0)) * 1.5;
    vec2 d = pixel - (home + loop_offset + buzz);
    if (dot(d, d) > 144.0) {
        discard;
    }

    vec2 velocity = vec2(1.3 * cos(t * 1.3) + 1.74 * cos(t * 2.9 + 1.0), -1.1 * sin(t * 1.1) + 1.85 * cos(t * 3.7 + 2.0));
    vec2 dir = normalize(velocity + vec2(0.0001));
    vec2 local = vec2(dot(d, dir), dot(d, vec2(-dir.y, dir.x)));
    float body = 1.0 - smoothstep(2.4, 3.3, length(local * vec2(0.7, 1.15)));
    float head = 1.0 - smoothstep(1.3, 2.0, length(local - vec2(3.2, 0.0)));
    float flap = 0.6 + 0.4 * sin(TIME * 70.0 + fi * 3.0);
    float wing_left = 1.0 - smoothstep(2.0, 3.0, length((local - vec2(-1.2, 2.7 * flap)) * vec2(1.0, 1.7)));
    float wing_right = 1.0 - smoothstep(2.0, 3.0, length((local - vec2(-1.2, -2.7 * flap)) * vec2(1.0, 1.7)));
    float body_alpha = max(body, head);
    float wing_alpha = max(wing_left, wing_right);

    vec3 color = mix(vec3(0.78, 0.8, 0.74), vec3(0.16, 0.1, 0.05), body_alpha);
    float alpha = max(wing_alpha * 0.35, body_alpha * 0.92);
    COLOR = vec4(color, alpha);
}
";

    private static Shader? _shader;

    public static void Attach(Creature creature, int attempt = 0)
    {
        try
        {
            if (creature.Side != CombatSide.Enemy || !creature.IsMonster || creature.Monster is Wriggler)
            {
                return;
            }

            if (NCombatRoom.Instance?.GetCreatureNode(creature) is not NCreature node || !GodotObject.IsInstanceValid(node))
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

            float scale = Math.Max(0.0001f, node.GetGlobalTransform().Scale.Y);
            Vector2 topLeft = (bounds.GlobalPosition - node.GlobalPosition) / scale;
            Vector2 area = size * (1f + 2f * Margin);
            area = new Vector2(Math.Max(area.X, Cell), Math.Max(area.Y, Cell));
            var cells = new Vector2(Math.Max(1f, MathF.Floor(area.X / Cell)), Math.Max(1f, MathF.Floor(area.Y / Cell)));

            var material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode } };
            material.SetShaderParameter("rect_size", area);
            material.SetShaderParameter("cells", cells);
            material.SetShaderParameter("cell", Cell);
            var swarm = new ColorRect
            {
                Name = NodeName,
                Color = Colors.White,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = material,
            };
            node.AddChild(swarm);
            swarm.Size = area;
            swarm.Position = topLeft + size / 2f - area / 2f;
            if (node.Visuals.GetParent() == node)
            {
                node.MoveChild(swarm, node.Visuals.GetIndex() + 1);
            }

            creature.Died += _ => FadeOut(swarm);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add Infestation flies: {ex}");
        }
    }

    private static void FadeOut(ColorRect swarm)
    {
        if (!GodotObject.IsInstanceValid(swarm) || !swarm.IsInsideTree())
        {
            return;
        }

        Tween tween = swarm.CreateTween();
        tween.TweenProperty(swarm, "modulate:a", 0f, 0.6);
        tween.TweenCallback(Callable.From(swarm.QueueFree));
    }
}
