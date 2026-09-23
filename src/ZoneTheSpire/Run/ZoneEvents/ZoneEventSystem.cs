using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.ZoneEvents;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Resolves zone events from synchronized run state. The game's VisitedEventIds is the authoritative cross-act,
/// save/reload and multiplayer-rejoin no-repeat set; no parallel persistence format is needed.
/// </summary>
internal static class ZoneEventSystem
{
    /// <summary>The saved chance before this node's roll, or null when this is not an eligible event room in a zone.</summary>
    public static int? CurrentEligibleChance(RunState runState)
    {
        ZoneContext? context = ZoneContext.Current(runState);
        ZoneTheSpireModifier? modifier = ZoneService.FindModifier(runState);
        return context != null
            && modifier != null
            && ZoneEventRules.ResolvedEventCanRoll(context.NodeKind)
            && runState.CurrentMapCoord != null
                ? modifier.ZoneEventChancePercent
                : null;
    }

    /// <summary>
    /// Records a vanilla/non-zone result from the chance that existed before the room was resolved. Passing the original
    /// value makes the patch's exception fallback idempotent even if selection failed after partially mutating state.
    /// </summary>
    public static void RecordNonZoneEvent(RunState runState, int originalChancePercent)
    {
        ZoneTheSpireModifier? modifier = ZoneService.FindModifier(runState);
        if (modifier != null)
        {
            modifier.ZoneEventChancePercent = ZoneEventRules.NextOfferChance(originalChancePercent, wasZoneEvent: false);
        }
    }

    public static EventModel? TryPull(RunState runState)
    {
        ZoneContext? context = ZoneContext.Current(runState);
        ZoneTheSpireModifier? modifier = ZoneService.FindModifier(runState);
        if (context == null
            || modifier == null
            || !ZoneEventRules.ResolvedEventCanRoll(context.NodeKind)
            || runState.CurrentMapCoord is not { } coord)
        {
            return null;
        }

        int chance = modifier.ZoneEventChancePercent;
        string location = ZoneEventRules.LocationKey(runState.CurrentActIndex, coord.row, coord.col);

        List<ZoneEventModel> models = ModelDb.All
            .OfType<ZoneEventModel>()
            .Where(model => model.ZoneIds.Contains(context.Biome.Id, StringComparer.Ordinal))
            .Where(model => model.IsAllowed(runState))
            .OrderBy(model => model.Id.Entry, StringComparer.Ordinal)
            .ToList();
        var visitedKeys = models
            .Where(model => runState.VisitedEventIds.Contains(model.Id))
            .Select(model => model.Id.Entry)
            .ToHashSet(StringComparer.Ordinal);
        var candidates = models.Select(model => new ZoneEventCandidate(model.Id.Entry, model.ZoneIds)).ToList();
        string? key = ZoneEventRules.PickEvent(
            runState.Rng.Seed,
            location,
            context.Biome.Id,
            candidates,
            visitedKeys,
            offerChancePercent: chance);
        if (key == null)
        {
            modifier.ZoneEventChancePercent = ZoneEventRules.NextOfferChance(chance, wasZoneEvent: false);
            return null;
        }

        ZoneEventModel selected = models.Single(model => string.Equals(model.Id.Entry, key, StringComparison.Ordinal));
        EventModel finalEvent = Hook.ModifyNextEvent(runState, selected);

        // Preserve the no-repeat guarantee even if another modifier's event hook redirects to an already-seen zone event.
        if (finalEvent is ZoneEventModel && runState.VisitedEventIds.Contains(finalEvent.Id))
        {
            finalEvent = selected;
        }

        bool wasZoneEvent = finalEvent is ZoneEventModel;
        runState.AddVisitedEvent(finalEvent);
        modifier.ZoneEventChancePercent = ZoneEventRules.NextOfferChance(chance, wasZoneEvent);
        return finalEvent;
    }
}
