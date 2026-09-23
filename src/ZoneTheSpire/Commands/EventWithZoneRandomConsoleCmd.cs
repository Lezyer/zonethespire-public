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
/// Debug command: <c>event_with_zone_random</c>. Enters an event room with any zone event, each with equal chance, even one
/// already seen this run, in one of that event's zones (as a "?" node, like <c>event_with_zone</c>). Networked, and the pick is
/// seeded (with a per-session call count, so repeats differ), so every peer enters the same event.
/// </summary>
public sealed class EventWithZoneRandomConsoleCmd : AbstractConsoleCmd
{
    private static int _calls;

    public override string CmdName => "event_with_zone_random";

    public override string Args => "";

    public override string Description => "Enters an event room with a random Zone the Spire zone event (equal chance, repeats allowed).";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!RunManager.Instance.IsInProgress || RunManager.Instance.DebugOnlyGetState() is not RunState runState)
        {
            return new CmdResult(success: false, "A run is currently not in progress!");
        }

        List<ZoneEventModel> models = ModelDb.All
            .OfType<ZoneEventModel>()
            .OrderBy(model => model.Id.Entry, StringComparer.Ordinal)
            .ToList();
        var candidates = models.Select(model => new ZoneEventCandidate(model.Id.Entry, model.ZoneIds)).ToList();
        string attemptKey = runState.CurrentActIndex.ToString(CultureInfo.InvariantCulture)
            + ":" + runState.VisitedEventIds.Count.ToString(CultureInfo.InvariantCulture)
            + ":" + (_calls++).ToString(CultureInfo.InvariantCulture);
        var pick = DebugZoneRules.PickAnyEvent(runState.Rng.Seed, attemptKey, candidates);
        BiomeDefinition? biome = pick == null ? null : DebugZoneRules.ResolveBiome(pick.Value.BiomeId);
        if (pick == null || biome == null)
        {
            return new CmdResult(success: false, "No zone events found.");
        }

        ZoneEventModel selected = models.Single(model => string.Equals(model.Id.Entry, pick.Value.Key, StringComparison.Ordinal));
        if (!runState.VisitedEventIds.Contains(selected.Id))
        {
            runState.AddVisitedEvent(selected);
        }

        ZoneOverride.Set(runState, biome, NodeKind.Unknown);
        Task task = RunManager.Instance.EnterRoomDebug(RoomType.Event, MapPointType.Unknown, selected);
        return new CmdResult(task, success: true, $"Entered {selected.Id.Entry} in {biome.DisplayName}");
    }
}
