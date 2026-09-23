using System;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Run;

/// <summary>
/// Zone forced by the <c>fight_with_zone</c>, <c>room_with_zone</c>, <c>event_with_zone</c> and <c>event_with_zone_random</c>
/// debug commands. It binds to the next room entered (the debug fight, shop, rest site, treasure room or event), stays active through that room (and its rewards), and clears when another room
/// is entered or the run changes. Never saved. Set on every peer because the commands are networked.
/// </summary>
internal static class ZoneOverride
{
    private sealed class State(IRunState runState, ZoneContext context)
    {
        public IRunState RunState { get; } = runState;

        public ZoneContext Context { get; } = context;

        public AbstractRoom? Room { get; set; }
    }

    private static State? _current;

    public static void Set(IRunState runState, BiomeDefinition biome, NodeKind kind)
    {
        var zone = new Zone($"debug_{biome.Id}", biome.Id, Array.Empty<GridCoord>());
        _current = new State(runState, new ZoneContext(zone, biome, kind));
    }

    /// <summary>Between runs (RunLifecycle).</summary>
    internal static void Reset() => _current = null;

    public static ZoneContext? ContextFor(IRunState runState) =>
        _current is { } state && ReferenceEquals(state.RunState, runState) ? state.Context : null;

    public static void OnRoomEntered(IRunState runState, AbstractRoom room)
    {
        if (_current is not { } state)
        {
            return;
        }

        if (!ReferenceEquals(state.RunState, runState))
        {
            _current = null;
            return;
        }

        if (state.Room == null)
        {
            state.Room = room;
            return;
        }

        if (!ReferenceEquals(state.Room, room))
        {
            _current = null;
        }
    }
}
