using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Debug;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Run;
using ZoneTheSpire.Run.ZoneEvents;

namespace ZoneTheSpire.Commands;

/// <summary>
/// Debug command: <c>event_with_zone &lt;zone&gt;</c>. Enters an event room whose event is one of the zone's own events, as if
/// the zone event roll had hit (no chance roll, and the run's zone event chance is left alone). Prefers events not seen this
/// run, and marks the chosen one as seen like a real roll, so repeating the command cycles through the zone's events; once
/// all were seen, any of them can come up again. The zone's effects (screen effect, tooltips) apply to the room as a "?"
/// node. Networked like <c>event</c>, and the pick is seeded, so every peer enters the same event.
/// </summary>
public sealed class EventWithZoneConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "event_with_zone";

    public override string Args => "<zone:string>";

    public override string Description => "Enters an event room with one of a Zone the Spire zone's own events (a guaranteed zone event roll).";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length < 1)
        {
            return new CmdResult(success: false, $"Usage: {CmdName} {Args}. Zones: {DebugZoneRules.ZoneIdList}.");
        }

        if (!RunManager.Instance.IsInProgress || RunManager.Instance.DebugOnlyGetState() is not RunState runState)
        {
            return new CmdResult(success: false, "A run is currently not in progress!");
        }

        BiomeDefinition? biome = DebugZoneRules.ResolveBiome(args[0]);
        if (biome == null)
        {
            return new CmdResult(success: false, $"Zone '{args[0]}' not found. Zones: {DebugZoneRules.ZoneIdList}.");
        }

        List<ZoneEventModel> models = ModelDb.All
            .OfType<ZoneEventModel>()
            .Where(model => model.ZoneIds.Contains(biome.Id, StringComparer.Ordinal))
            .OrderBy(model => model.Id.Entry, StringComparer.Ordinal)
            .ToList();
        var visitedKeys = models
            .Where(model => runState.VisitedEventIds.Contains(model.Id))
            .Select(model => model.Id.Entry)
            .ToHashSet(StringComparer.Ordinal);
        var candidates = models.Select(model => new ZoneEventCandidate(model.Id.Entry, model.ZoneIds)).ToList();
        string attemptKey = "debug:" + runState.CurrentActIndex.ToString(CultureInfo.InvariantCulture)
            + ":" + runState.VisitedEventIds.Count.ToString(CultureInfo.InvariantCulture);
        string? key = DebugZoneRules.PickForcedEvent(runState.Rng.Seed, attemptKey, biome.Id, candidates, visitedKeys);
        if (key == null)
        {
            return new CmdResult(success: false, $"{biome.DisplayName} has no zone events.");
        }

        ZoneEventModel selected = models.Single(model => string.Equals(model.Id.Entry, key, StringComparison.Ordinal));
        runState.AddVisitedEvent(selected);
        ZoneOverride.Set(runState, biome, NodeKind.Unknown);
        Task task = RunManager.Instance.EnterRoomDebug(RoomType.Event, MapPointType.Unknown, selected);
        return new CmdResult(task, success: true, $"Entered {selected.Id.Entry} in {biome.DisplayName}");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            return CompleteArgument(BiomeRegistry.Ids, Array.Empty<string>(), args.FirstOrDefault() ?? "");
        }

        return base.GetArgumentCompletions(player, args);
    }
}
