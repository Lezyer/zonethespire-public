using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Model;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Adds a zone relic to a zone chest's relic choices. Runs inside TreasureRoomRelicSynchronizer.BeginRelicPicking, which every
/// peer runs on entering the chest room, and only reads synced state (run seed, map location, zones, the saved appeared list),
/// so every peer appends the same relic at the same index and records it the same way. Votes are indices, so the extra relic
/// goes through the game's normal voting, rock-paper-scissors and awarding.
/// </summary>
internal static class ZoneChestRelics
{
    private static readonly IReadOnlyDictionary<string, Func<RelicModel>> Models = new Dictionary<string, Func<RelicModel>>(StringComparer.Ordinal)
    {
        [ZoneRelicCatalog.VerminSymbiontKey] = () => ModelDb.Relic<VerminSymbiont>(),
        [ZoneRelicCatalog.CrimsonToothKey] = () => ModelDb.Relic<CrimsonTooth>(),
        [ZoneRelicCatalog.LargeFieldstoneKey] = () => ModelDb.Relic<LargeFieldstone>(),
        [ZoneRelicCatalog.MarblePauldronsKey] = () => ModelDb.Relic<MarblePauldrons>(),
        [ZoneRelicCatalog.ReflectiveShardKey] = () => ModelDb.Relic<ReflectiveShard>(),
        [ZoneRelicCatalog.GoldenWishmakerKey] = () => ModelDb.Relic<GoldenWishmaker>(),
        [ZoneRelicCatalog.FlutteringPhantasmKey] = () => ModelDb.Relic<FlutteringPhantasm>(),
        [ZoneRelicCatalog.SalvageMachineKey] = () => ModelDb.Relic<SalvageMachine>(),
        [ZoneRelicCatalog.PrismCloudKey] = () => ModelDb.Relic<PrismCloud>(),
        [ZoneRelicCatalog.SeedOfHatredKey] = () => ModelDb.Relic<SeedOfHatred>(),
        [ZoneRelicCatalog.ScrollOfChantsKey] = () => ModelDb.Relic<ScrollOfChants>(),
        [ZoneRelicCatalog.FrostheartKey] = () => ModelDb.Relic<Frostheart>(),
        [ZoneRelicCatalog.SacrosanctFlailKey] = () => ModelDb.Relic<SacrosanctFlail>(),
        [ZoneRelicCatalog.BrewExtractorKey] = () => ModelDb.Relic<BrewExtractor>(),
    };

    /// <summary>Whether <paramref name="relic"/> is a zone relic (they never come from the normal chest roll).</summary>
    public static bool IsZoneRelic(RelicModel? relic) =>
        relic != null && Models.Values.Any(model => model().Id.Equals(relic.Id));

    /// <summary>Appends this chest's zone relic to <paramref name="relics"/> when one is rolled. Returns the relic added, if any.</summary>
    public static RelicModel? TryAddOffer(IRunState runState, List<RelicModel> relics)
    {
        // Empty chests (e.g. Silver Crucible) stay empty.
        if (relics.Count == 0 || relics.Any(IsZoneRelic))
        {
            return null;
        }

        ZoneContext? context = ZoneContext.Current(runState);
        ZoneTheSpireModifier? modifier = ZoneService.FindModifier(runState);
        if (context == null || modifier == null || !ZoneRelicRules.ResolvedTreasureCanOffer(context.NodeKind))
        {
            return null;
        }

        // This method is reached through the actual TreasureRoomRelicSynchronizer. A map "?" that became a chest normally
        // resolves to NodeKind.Treasure here (see ZoneContext.Current); ResolvedTreasureCanOffer also accepts Unknown as a fallback.

        IReadOnlyList<string> candidates = ZoneRelicCatalog.KeysFor(context.Biome.Id).Where(Models.ContainsKey).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, runState.CurrentMapCoord?.row, runState.CurrentMapCoord?.col, 0);
        bool forced = ZoneOverride.ContextFor(runState) != null && context.NodeKind == NodeKind.Treasure;
        string? key = ZoneRelicRules.PickOffer(
            runState.Rng.Seed,
            location,
            candidates,
            ZoneRelicRules.ParseAppeared(modifier.ZoneRelicsAppeared),
            forceOffer: forced);
        if (key == null)
        {
            return null;
        }

        RelicModel relic = Models[key]();
        relics.Add(relic);
        modifier.ZoneRelicsAppeared = ZoneRelicRules.MarkAppeared(modifier.ZoneRelicsAppeared, key, location);
        return relic;
    }
}
