using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Scrapyard;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Scrapyard fights: 1–2 extra act 3 bots (not minions) spawned floating above the enemies, with act-scaled HP. Bot count
/// and types come from the run seed and map location; runs inside the synced combat-start hook on every peer.
/// </summary>
internal static class ScrapyardBots
{
    private const float HalfScreenWidth = 960f;
    private const float HalfScreenHeight = 540f;
    private const float TopMargin = 110f;
    private const int MaxLayoutRetries = 3;

    private static readonly ConditionalWeakTable<Creature, object> Bots = new();
    private static readonly object Marker = new();

    /// <summary>True for bots spawned by the Scrapyard (they float, so clone layout ignores them).</summary>
    public static bool IsScrapyardBot(Creature? creature) => creature != null && Bots.TryGetValue(creature, out _);

    public static async Task SpawnFor(CombatRoom room)
    {
        CombatState state = room.CombatState;
        IRunState runState = state.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, room.Id ?? 0);
        IReadOnlyList<ScrapBot> picks = ScrapyardRules.PickBots(runState.Rng.Seed, location);
        int hpPercent = ScrapyardRules.BotHpPercent(runState.CurrentActIndex);

        var spawned = new List<Creature>();
        foreach (ScrapBot bot in picks)
        {
            MonsterModel model = (bot switch
            {
                ScrapBot.Noisebot => (MonsterModel)ModelDb.Monster<Noisebot>(),
                ScrapBot.Guardbot => ModelDb.Monster<Guardbot>(),
                ScrapBot.Stabbot => ModelDb.Monster<Stabbot>(),
                _ => ModelDb.Monster<Zapbot>(),
            }).ToMutable();
            Creature creature = state.CreateCreature(model, CombatSide.Enemy, null);
            Bots.AddOrUpdate(creature, Marker);
            await CreatureCmd.Add(creature);
            await CreatureCmd.SetMaxAndCurrentHp(creature, ScrapyardRules.ScaledHp(creature.MaxHp, hpPercent));
            spawned.Add(creature);
        }

        ArrangeFloating(spawned, state);
    }

    /// <summary>
    /// Visual only: cancel each bot's Fabricator "fall" offset so it floats, then place the bots one by one where they
    /// overlap neither the enemies (including bodies of dead enemies still on the field) nor each other
    /// (<see cref="FloatingPlacement"/>). Also used for Infestation Wrigglers above Decimillipede segments.
    /// </summary>
    internal static void ArrangeFloating(IReadOnlyList<Creature> bots, ICombatState state, string label = "Scrapyard bots", int attempt = 0)
    {
        try
        {
            NCombatRoom? room = NCombatRoom.Instance;
            if (room == null || bots.Count == 0)
            {
                return;
            }

            if (bots.Any(bot => room.GetCreatureNode(bot) is { } pending && !pending.IsInsideTree()))
            {
                // CreatureCmd.Add uses AddChildSafely, which defers adding the node while the container is busy (e.g. several
                // enemies dying in one action). Lay out once it is in the tree, so enemies are found and sizes are real.
                if (attempt < MaxLayoutRetries)
                {
                    Callable.From(() => ArrangeFloating(bots, state, label, attempt + 1)).CallDeferred();
                }
                else
                {
                    Log.Warn($"{label}: node still not in the scene after {attempt} deferred layouts; left where it is.");
                }

                return;
            }

            var botNodes = new List<NCreature>();
            foreach (Creature bot in bots)
            {
                if (room.GetCreatureNode(bot) is not { } node)
                {
                    continue;
                }

                if (node.GetSpecialNode<Node2D>("Visuals/FallControl") is { } fallControl)
                {
                    fallControl.Position = Vector2.Zero;
                }

                botNodes.Add(node);
            }

            if (botNodes.Count == 0)
            {
                return;
            }

            float scaling = Math.Max(0.01f, state.Encounter?.GetCameraScaling() ?? 1f);
            var stage = new StageBounds(CloneLayout.MinX, HalfScreenWidth / scaling, -HalfScreenHeight / scaling + TopMargin);
            var botSet = new HashSet<Creature>(bots);
            Node parent = botNodes[0].GetParent();
            List<CreatureBox> occupied = room.CreatureNodes
                .Where(node => GodotObject.IsInstanceValid(node)
                               && node.GetParent() == parent
                               && node.Entity.Side == CombatSide.Enemy
                               && node.Entity.PetOwner == null
                               && !botSet.Contains(node.Entity))
                .Select(node => MirrorCloning.BoxOf(node, node.Position))
                .ToList();
            List<CloneShape> shapes = botNodes
                .Select(node => MirrorCloning.BoxOf(node, node.Position))
                .Select(box => new CloneShape(box.Width, box.TopOffset, box.Height))
                .ToList();

            IReadOnlyList<(float X, float Y)> spots = FloatingPlacement.Place(occupied, shapes, stage);
            for (int i = 0; i < botNodes.Count; i++)
            {
                (float x, float y) = CloneLayout.ClampOnStage(spots[i].X, spots[i].Y, shapes[i], stage);
                botNodes[i].Position = new Vector2(x, y);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to position {label}: {ex}");
        }
    }
}
