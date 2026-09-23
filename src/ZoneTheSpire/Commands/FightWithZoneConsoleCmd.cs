using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Debug;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Run;

namespace ZoneTheSpire.Commands;

/// <summary>
/// Debug command: <c>fight_with_zone &lt;encounter&gt; &lt;zone&gt;</c>. Same as the game's <c>fight</c>, but the fight and its
/// rewards use the zone's effects. Networked like <c>fight</c>, so every peer processes it and sets the same override.
/// </summary>
public sealed class FightWithZoneConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "fight_with_zone";

    public override string Args => "<id:string> <zone:string>";

    public override string Description => "Jumps a player to a specific encounter with a Zone the Spire zone's effects applied.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length < 2)
        {
            return new CmdResult(success: false, $"Usage: {CmdName} {Args}. Zones: {DebugZoneRules.ZoneIdList}.");
        }

        if (!RunManager.Instance.IsInProgress || RunManager.Instance.DebugOnlyGetState() is not IRunState runState)
        {
            return new CmdResult(success: false, "A run is currently not in progress!");
        }

        BiomeDefinition? biome = DebugZoneRules.ResolveBiome(args[1]);
        if (biome == null)
        {
            return new CmdResult(success: false, $"Zone '{args[1]}' not found. Zones: {DebugZoneRules.ZoneIdList}.");
        }

        ModelId modelId = new(ModelId.SlugifyCategory<EncounterModel>(), args[0].ToUpperInvariant());
        EncounterModel encounter;
        try
        {
            encounter = ModelDb.GetById<EncounterModel>(modelId).ToMutable();
        }
        catch (ModelNotFoundException)
        {
            return new CmdResult(success: false, $"Encounter '{modelId.Entry}' not found");
        }

        NodeKind kind = DebugZoneRules.NodeKindFor(encounter.RoomType switch
        {
            RoomType.Elite => DebugEncounterKind.Elite,
            RoomType.Boss => DebugEncounterKind.Boss,
            _ => DebugEncounterKind.Monster,
        });
        ZoneOverride.Set(runState, biome, kind);

        encounter.DebugRandomizeRng();
        Task task = RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Unassigned, encounter);
        return new CmdResult(task, success: true, $"Jumped to encounter: '{encounter.Id.Entry}' in {biome.DisplayName} (as {kind} node)");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            return CompleteArgument(ModelDb.AllEncounters.Select(e => e.Id.Entry).ToList(), Array.Empty<string>(), args.FirstOrDefault() ?? "");
        }

        if (args.Length == 2)
        {
            return CompleteArgument(BiomeRegistry.Ids, new[] { args[0] }, args[1]);
        }

        return base.GetArgumentCompletions(player, args);
    }
}
