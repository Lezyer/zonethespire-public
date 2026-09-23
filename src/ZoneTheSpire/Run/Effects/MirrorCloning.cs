using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

internal static class MirrorCloning
{
    private const float HalfScreenWidth = 960f;
    private const float HalfScreenHeight = 540f;
    private const float TopMargin = 110f;
    private const float ScreenEdgeMargin = 20f;
    private const float FallbackHeight = 250f;
    private const double SlideSeconds = 0.3;
    private const int MaxLayoutRetries = 3;

    private sealed record Slide(Tween Tween, Vector2 Target);

    /// <summary>Creatures currently gliding to a new spot, so back-to-back splits lay out against where they are heading.</summary>
    private static readonly ConditionalWeakTable<NCreature, Slide> Slides = new();

    /// <summary>Combat rooms whose enemy row was re-laid: enemies summoned into scene slots there afterwards are placed too.</summary>
    private static readonly ConditionalWeakTable<NCombatRoom, object> RelaidRooms = new();

    private static readonly object Relaid = new();

    /// <summary>
    /// Spawns a fresh copy of the original's monster (no copied buffs/debuffs) with <paramref name="clonePercent"/>% of the
    /// original's max HP and the Reflection marker. The clone carries the original's slot label so slot-dependent monsters
    /// can initialize their moves, but it does not consume another free slot: that label was already occupied by the original.
    /// This preserves free slots for vanilla summoners such as Living Fog and Two-Tailed Rat. The clone is then placed beside
    /// the original instead of remaining at the slot's scene position.
    /// Deterministic: runs inside synced combat hooks on every peer.
    /// </summary>
    public static async Task SpawnClone(Creature original, int clonePercent)
    {
        ICombatState? combatState = original.CombatState;
        MonsterModel? monster = original.Monster;
        if (combatState == null || monster == null)
        {
            Log.Warn("Cannot clone a creature without a combat state or monster model.");
            return;
        }

        // Phantasmal Gardener and potentially other hand-placed monsters branch their initial move on SlotName. Passing null
        // lets CreatureCmd.Add insert a partial clone, then throws while rolling its first move before HP, Reflection and
        // layout are applied. Reusing the original's label is deterministic and does not occupy a second encounter slot.
        Creature clone = combatState.CreateCreature(monster.CanonicalInstance.ToMutable(), original.Side, original.SlotName);
        CloneIdentity.Mark(clone);
        CloneRegistry.Add(clone);
        try
        {
            await CreatureCmd.Add(clone);
        }
        finally
        {
            CloneRegistry.Remove(clone);
        }

        await CreatureCmd.SetMaxAndCurrentHp(clone, MirrorRules.CloneMaxHp(original.MaxHp, clonePercent));
        await PowerCmd.Apply<ReflectionPower>(new ThrowingPlayerChoiceContext(), clone, 1m, original, null);
        if (clone.Monster != null)
        {
            // The move rolled while the clone was being added may be a summon.
            CloneMoveGuard.ReplaceSummon(clone.Monster);

            // A new clone is stunned on its first turn: its intent shows the stun, then it performs the move it rolled.
            try
            {
                string? nextMoveId = clone.Monster.NextMove?.Id;
                await CreatureCmd.Stun(clone, string.IsNullOrEmpty(nextMoveId) ? null : nextMoveId);
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to stun a new mirrored clone: {ex.Message}");
            }
        }

        Arrange(original, clone, combatState);
    }

    /// <summary>
    /// Visual only. The clone slides out from the original to the spot <see cref="CloneLayout.PlanClone"/> picks: a free row
    /// gap, the whole row re-laid on the ground (hand-placed enemies in the air keep their height), a floating spot, or as a
    /// last resort the least crowded on-screen spot. The original may be dead
    /// (a reviving enemy's body) and still counts as occupying its spot. With <paramref name="ownFootprint"/> the newcomer
    /// is sized from its own node instead of the original's (it isn't a copy of the same monster).
    /// </summary>
    internal static void Arrange(Creature original, Creature clone, ICombatState combatState, bool ownFootprint = false, string label = "Mirrored clone", int attempt = 0, bool onlyIfOverlapping = false)
    {
        try
        {
            NCombatRoom? room = NCombatRoom.Instance;
            NCreature? originalNode = room?.GetCreatureNode(original);
            NCreature? cloneNode = room?.GetCreatureNode(clone);
            if (room == null || originalNode == null || cloneNode == null)
            {
                return;
            }

            if (!cloneNode.IsInsideTree())
            {
                // CreatureCmd.Add uses AddChildSafely, which defers adding the node while the container is busy. Lay out
                // once it is in the tree, so the row is found and sizes are real.
                if (attempt < MaxLayoutRetries)
                {
                    Callable.From(() => Arrange(original, clone, combatState, ownFootprint, label, attempt + 1, onlyIfOverlapping)).CallDeferred();
                }
                else
                {
                    Log.Warn($"{label}: node still not in the scene after {attempt} deferred layouts; left where it is.");
                }

                return;
            }

            float scaling = Math.Max(0.01f, combatState.Encounter?.GetCameraScaling() ?? 1f);
            var stage = new StageBounds(
                CloneLayout.MinX,
                HalfScreenWidth / scaling,
                -HalfScreenHeight / scaling + TopMargin,
                SpreadLeftEdge(room, cloneNode, original.Side, scaling));
            List<NCreature> row = room.CreatureNodes
                .Where(node => node != cloneNode
                               && GodotObject.IsInstanceValid(node)
                               && node.GetParent() == cloneNode.GetParent()
                               && node.Entity.Side == original.Side
                               && node.Entity.PetOwner == null
                               && (node.Entity.IsAlive || node.Entity == original)
                               && !ScrapyardBots.IsScrapyardBot(node.Entity))
                .OrderBy(node => TargetPosition(node).X)
                .ToList();

            Vector2 originalTarget = TargetPosition(originalNode);
            CreatureBox originalBox = BoxOf(originalNode, originalTarget);
            List<CreatureBox> occupied = row.Select(node => BoxOf(node, TargetPosition(node))).ToList();
            // A clone is a fresh copy of the same monster, so it has the original's footprint.
            CreatureBox shapeBox = ownFootprint ? BoxOf(cloneNode, originalTarget) : originalBox;
            var shape = new CloneShape(shapeBox.Width, shapeBox.TopOffset, shapeBox.Height);
            if (onlyIfOverlapping && !occupied.Any(box => Overlaps(box, shapeBox)))
            {
                return;
            }

            cloneNode.Position = originalTarget;
            ClonePlan plan = CloneLayout.PlanClone(originalBox, shape, occupied, stage);
            if (plan.Kind == ClonePlanKind.Place)
            {
                (float x, float y) = CloneLayout.ClampOnStage(plan.X, plan.Y, shape, stage);
                SlideTo(cloneNode, new Vector2(x, y));
                return;
            }

            RelaidRooms.AddOrUpdate(room, Relaid);
            row.Insert(plan.CloneIndex, cloneNode);
            for (int i = 0; i < row.Count && i < plan.Row.Count; i++)
            {
                SlideTo(row[i], new Vector2(plan.Row[i].CenterX, CloneLayout.GroundY + plan.Row[i].YOffset));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to position {label}: {ex}");
        }
    }

    /// <summary>
    /// An enemy summoned into a scene slot lands on the slot's marker. In a row that was re-laid, another enemy may now stand
    /// there: if the newcomer overlaps anyone, it is placed like a clone (a free gap, the row re-laid, or floating). Clones,
    /// scrap bots and pets have their own layout. Visual only.
    /// </summary>
    internal static void AfterCreatureAdded(Creature creature)
    {
        try
        {
            if (creature.SlotName == null || creature.PetOwner != null || creature.IsPlayer || CloneIdentity.IsClone(creature)
                || ScrapyardBots.IsScrapyardBot(creature) || creature.CombatState is not { } combatState
                || NCombatRoom.Instance is not { } room || !RelaidRooms.TryGetValue(room, out _))
            {
                return;
            }

            Arrange(creature, creature, combatState, ownFootprint: true, label: "Slotted summon", onlyIfOverlapping: true);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to place a slotted summon: {ex}");
        }
    }

    /// <summary>
    /// How far left (in the enemy container) a crowded enemy row may reach: <see cref="CloneLayout.CloneGap"/> right of the
    /// rightmost player or pet, and never past the screen's left edge. Never tighter than the usual enemy area.
    /// </summary>
    private static float SpreadLeftEdge(NCombatRoom room, NCreature enemyNode, CombatSide enemySide, float scaling)
    {
        float left = -HalfScreenWidth / scaling + ScreenEdgeMargin;
        if (enemyNode.GetParent() is not CanvasItem container)
        {
            return Math.Min(CloneLayout.SpreadMinX, CloneLayout.MinX);
        }

        Transform2D toContainer = container.GetGlobalTransform().AffineInverse();
        foreach (NCreature node in room.CreatureNodes)
        {
            if (!GodotObject.IsInstanceValid(node) || node.Entity.Side == enemySide || !node.IsVisibleInTree())
            {
                continue;
            }

            Control bounds = node.Visuals.Bounds;
            Vector2 rightEdge = bounds.GlobalPosition + new Vector2(bounds.Size.X * bounds.GetGlobalTransform().Scale.X, 0f);
            left = Math.Max(left, (toContainer * rightEdge).X + CloneLayout.CloneGap);
        }

        return Math.Min(left, CloneLayout.MinX);
    }

    private static bool Overlaps(CreatureBox a, CreatureBox b) =>
        MathF.Abs(a.CenterX - b.CenterX) < (a.Width + b.Width) / 2f
        && a.Top < b.Top + b.Height && b.Top < a.Top + a.Height;

    /// <summary>A creature's footprint in the enemy container at <paramref name="position"/>: vanilla width, visual height.</summary>
    internal static CreatureBox BoxOf(NCreature node, Vector2 position)
    {
        Control bounds = node.Visuals.Bounds;
        float parentScale = Math.Max(0.0001f, (node.GetParent() as CanvasItem)?.GetGlobalTransform().Scale.Y ?? 1f);
        float height = bounds.Size.Y * bounds.GetGlobalTransform().Scale.Y / parentScale;
        float topOffset = (bounds.GlobalPosition.Y - node.GlobalPosition.Y) / parentScale;
        if (height <= 1f)
        {
            height = FallbackHeight;
            topOffset = -FallbackHeight;
        }

        return new CreatureBox(position.X, position.Y, bounds.Size.X, topOffset, height);
    }

    private static Vector2 TargetPosition(NCreature node) =>
        Slides.TryGetValue(node, out Slide? slide) && GodotObject.IsInstanceValid(slide.Tween) && slide.Tween.IsRunning()
            ? slide.Target
            : node.Position;

    private static void SlideTo(NCreature node, Vector2 target)
    {
        if (Slides.TryGetValue(node, out Slide? previous) && GodotObject.IsInstanceValid(previous.Tween))
        {
            previous.Tween.Kill();
        }

        Tween tween = node.CreateTween();
        tween.TweenProperty(node, "position", target, SlideSeconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        Slides.AddOrUpdate(node, new Slide(tween, target));
    }
}

/// <summary>Enemies summoned into scene slots after a row was re-laid get placed too (see MirrorCloning.AfterCreatureAdded).</summary>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
internal static class SlottedSummonLayoutPatch
{
    private static void Postfix(Creature creature) => MirrorCloning.AfterCreatureAdded(creature);
}
