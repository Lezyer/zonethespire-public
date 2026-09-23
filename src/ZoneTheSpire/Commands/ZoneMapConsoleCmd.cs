using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Debug;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run;

namespace ZoneTheSpire.Commands;

/// <summary>
/// Debug command: <c>zone_map &lt;zone|random&gt;</c>. Makes every node of the current act's map part of one zone of that
/// biome, or rolls new random zones. Saved with the run like generated zones. Networked, so every peer applies it.
/// </summary>
public sealed class ZoneMapConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "zone_map";

    public override string Args => "<zone:string>";

    public override string Description => "Makes the whole current act map one Zone the Spire zone, or rolls new random zones.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length < 1)
        {
            return new CmdResult(success: false, $"Usage: {CmdName} {Args}. Zones: {DebugZoneRules.ZoneIdList}, {DebugZoneRules.RandomZonesArg}.");
        }

        if (!RunManager.Instance.IsInProgress || RunManager.Instance.DebugOnlyGetState() is not IRunState runState)
        {
            return new CmdResult(success: false, "A run is currently not in progress!");
        }

        BiomeDefinition? biome = null;
        if (!DebugZoneRules.IsRandomZonesArg(args[0]))
        {
            biome = DebugZoneRules.ResolveBiome(args[0]);
            if (biome == null)
            {
                return new CmdResult(success: false, $"Zone '{args[0]}' not found. Zones: {DebugZoneRules.ZoneIdList}, {DebugZoneRules.RandomZonesArg}.");
            }
        }

        IReadOnlyList<Zone>? zones;
        try
        {
            zones = ZoneService.ReplaceCurrentActZones(runState, biome?.Id);
        }
        catch (Exception ex)
        {
            Log.Warn($"zone_map failed: {ex}");
            return new CmdResult(success: false, "Could not replace the zones: no act map is available.");
        }

        if (zones == null)
        {
            return new CmdResult(success: false, "Zone the Spire is not active in this run.");
        }

        if (NMapScreen.Instance is { } screen)
        {
            ZoneOverlay.RefreshDeferred(screen);
        }

        string what = biome != null
            ? $"The whole map is now {biome.DisplayName}"
            : $"Rolled {zones.Count} random zones ({string.Join(", ", zones.Select(zone => zone.BiomeId))})";
        return new CmdResult(success: true, $"{what}. Takes effect from the next room.");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            return CompleteArgument(DebugZoneRules.ZoneMapArgs, Array.Empty<string>(), args.FirstOrDefault() ?? "");
        }

        return base.GetArgumentCompletions(player, args);
    }
}
