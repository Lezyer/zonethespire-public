using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Debug;
using ZoneTheSpire.Run;

namespace ZoneTheSpire.Commands;

/// <summary>
/// Debug command: <c>room_with_zone &lt;shop|rest|treasure&gt; &lt;zone&gt;</c>. Enters a shop, rest site or treasure room with the zone's effects applied
/// until the next room. Networked like <c>fight</c>, so every peer processes it and sets the same override.
/// </summary>
public sealed class RoomWithZoneConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "room_with_zone";

    public override string Args => "<room:string> <zone:string>";

    public override string Description => "Enters a shop, rest site or treasure room with a Zone the Spire zone's effects applied.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length < 2)
        {
            return new CmdResult(success: false, $"Usage: {CmdName} {Args}. Rooms: {DebugZoneRules.RoomIdList}. Zones: {DebugZoneRules.ZoneIdList}.");
        }

        if (!RunManager.Instance.IsInProgress || RunManager.Instance.DebugOnlyGetState() is not IRunState runState)
        {
            return new CmdResult(success: false, "A run is currently not in progress!");
        }

        if (DebugZoneRules.ResolveRoom(args[0]) is not { } room)
        {
            return new CmdResult(success: false, $"Room '{args[0]}' not supported. Rooms: {DebugZoneRules.RoomIdList}.");
        }

        BiomeDefinition? biome = DebugZoneRules.ResolveBiome(args[1]);
        if (biome == null)
        {
            return new CmdResult(success: false, $"Zone '{args[1]}' not found. Zones: {DebugZoneRules.ZoneIdList}.");
        }

        ZoneOverride.Set(runState, biome, DebugZoneRules.NodeKindFor(room));
        RoomType roomType = room switch
        {
            DebugRoomKind.Shop => RoomType.Shop,
            DebugRoomKind.Treasure => RoomType.Treasure,
            _ => RoomType.RestSite,
        };
        Task task = RunManager.Instance.EnterRoomDebug(roomType);
        return new CmdResult(task, success: true, $"Entered {args[0].Trim().ToLowerInvariant()} in {biome.DisplayName}");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            return CompleteArgument(DebugZoneRules.RoomIds, Array.Empty<string>(), args.FirstOrDefault() ?? "");
        }

        if (args.Length == 2)
        {
            return CompleteArgument(BiomeRegistry.Ids, new[] { args[0] }, args[1]);
        }

        return base.GetArgumentCompletions(player, args);
    }
}
