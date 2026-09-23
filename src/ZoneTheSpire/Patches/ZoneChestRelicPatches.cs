using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Zone chests: after the game rolls the chest's relics (one per player, on every peer at room entry), a zone relic may be
/// added as one more choice. It is appended last, so the normal relics keep their indices; votes, rock-paper-scissors and
/// awarding are the game's own. Runs after other mods' postfixes (e.g. Heart of the Spire adds its Sapphire Key to the same
/// list), so the zone relic is always last and taking it out for layout never shifts another mod's index.
/// </summary>
[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
internal static class ZoneChestRelicOfferPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(List<RelicModel>? ____currentRelics, IPlayerCollection ____playerCollection)
    {
        try
        {
            if (____currentRelics == null || ____playerCollection.Players.FirstOrDefault()?.RunState is not { } runState)
            {
                return;
            }

            ZoneChestRelics.TryAddOffer(runState, ____currentRelics);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add a zone chest relic: {ex}");
        }
    }
}

/// <summary>
/// Shows the zone relic in its own holder to the right of the normal relics. The game lays out holders by relic count (one
/// relic uses the centred singleplayer holder, more use the multiplayer spots), so the zone relic is taken out of the list
/// while the game lays out the normal relics, then put back and given a new holder with its own index. Local presentation
/// only: the relic list is the same before and after.
/// </summary>
[HarmonyPatch(typeof(NTreasureRoomRelicCollection), nameof(NTreasureRoomRelicCollection.InitializeRelics))]
internal static class ZoneChestRelicHolderPatch
{
    private const string HolderScenePath = "res://scenes/ui/treasure_relic_holder.tscn";
    private const float HolderSize = 136f;

    /// <summary>Holder centre, relative to the relic collection's centre: right of the singleplayer relic.</summary>
    private static readonly Vector2 SingleplayerSpot = new(250f, -81f);

    /// <summary>Holder centre, relative to the relic collection's centre: right of the rightmost multiplayer spot (x 320).</summary>
    private static readonly Vector2 MultiplayerSpot = new(500f, 0f);

    private static readonly AccessTools.FieldRef<TreasureRoomRelicSynchronizer, List<RelicModel>?> CurrentRelics =
        SafeRef.Field<TreasureRoomRelicSynchronizer, List<RelicModel>?>("_currentRelics");

    private static readonly AccessTools.FieldRef<NTreasureRoomRelicCollection, List<NTreasureRoomRelicHolder>> HoldersInUse =
        SafeRef.Field<NTreasureRoomRelicCollection, List<NTreasureRoomRelicHolder>>("_holdersInUse");

    private static readonly AccessTools.FieldRef<NTreasureRoomRelicCollection, IRunState> RunState =
        SafeRef.Field<NTreasureRoomRelicCollection, IRunState>("_runState");

    private static readonly MethodInfo? PickRelic = AccessTools.Method(typeof(NTreasureRoomRelicCollection), "PickRelic");

    private static void Prefix(out RelicModel? __state)
    {
        __state = null;
        try
        {
            List<RelicModel>? relics = CurrentRelics(RunManager.Instance.TreasureRoomRelicSynchronizer);
            if (relics is { Count: >= 2 } && ZoneChestRelics.IsZoneRelic(relics[^1]))
            {
                __state = relics[^1];
                relics.RemoveAt(relics.Count - 1);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to set aside the zone chest relic: {ex}");
        }
    }

    private static void Finalizer(NTreasureRoomRelicCollection __instance, RelicModel? __state)
    {
        if (__state == null)
        {
            return;
        }

        try
        {
            // The prefix set the relic aside: it goes back first, whatever happens to its holder.
            List<RelicModel>? relics = CurrentRelics(RunManager.Instance.TreasureRoomRelicSynchronizer);
            if (relics == null)
            {
                return;
            }

            relics.Add(__state);
            AddHolder(__instance, __state, relics.Count - 1, singleplayerLayout: relics.Count == 2);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to show the zone chest relic: {ex}");
        }
    }

    private static void AddHolder(NTreasureRoomRelicCollection collection, RelicModel relic, int index, bool singleplayerLayout)
    {
        Control container = collection.GetNode<Control>("Container");
        var holder = ResourceLoader.Load<PackedScene>(HolderScenePath).Instantiate<NTreasureRoomRelicHolder>();
        holder.Name = "ZoneRelicHolder";
        Vector2 centre = singleplayerLayout ? SingleplayerSpot : MultiplayerSpot;
        holder.AnchorLeft = holder.AnchorRight = holder.AnchorTop = holder.AnchorBottom = 0.5f;
        holder.OffsetLeft = centre.X - HolderSize / 2f;
        holder.OffsetRight = centre.X + HolderSize / 2f;
        holder.OffsetTop = centre.Y - HolderSize / 2f;
        holder.OffsetBottom = centre.Y + HolderSize / 2f;
        // Added right away (not deferred): Initialize needs the holder's child nodes, which _Ready looks up.
        container.AddChild(holder);

        holder.Visible = true;
        holder.Index = index;
        holder.Initialize(relic, RunState(collection));
        holder.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NTreasureRoomRelicHolder>(picked => PickRelic?.Invoke(collection, new object[] { picked })));
        holder.VoteContainer.RefreshPlayerVotes();
        holder.SetFocusMode(Control.FocusModeEnum.All);
        // With 2+ normal relics the game keeps all 4 multiplayer holders in use, hidden ones included (their relic is never
        // set). Awarding finds each relic's holder by scanning in order and reads every holder's relic on the way, so the zone
        // holder must come right after the holders that show a relic, before the hidden ones.
        List<NTreasureRoomRelicHolder> holders = HoldersInUse(collection);
        holders.Insert(Math.Min(index, holders.Count), holder);
    }
}
