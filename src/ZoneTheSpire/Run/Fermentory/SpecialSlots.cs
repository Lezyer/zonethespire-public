using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Core.Mirror;

namespace ZoneTheSpire.Run.Fermentory;

/// <summary>
/// The Fermentory's special potion slots, kept in the hidden modifier's saved <c>SpecialPotionSlots</c> string. A slot stays
/// special for the whole run, in every room. Changes happen only in synced hooks, so every peer holds the same map.
/// </summary>
internal static class SpecialSlots
{
    private static readonly IReadOnlyDictionary<int, SlotEffect> None = new Dictionary<int, SlotEffect>();

    /// <summary>Raised on each peer after a slot becomes special (for the top-bar visuals).</summary>
    public static event Action<Player>? Changed;

    public static IReadOnlyDictionary<int, SlotEffect> Get(Player player)
    {
        if (ZoneService.FindModifier(player.RunState) is not { } modifier)
        {
            return None;
        }

        return FermentoryRules.Decode(modifier.SpecialPotionSlots).TryGetValue(player.NetId, out var slots) ? slots : None;
    }

    public static SlotEffect? EffectAt(Player player, int slot) =>
        slot >= 0 && Get(player).TryGetValue(slot, out SlotEffect effect) ? effect : null;

    /// <summary>Whether gaining a special slot would change a slot (some slot isn't special yet).</summary>
    public static bool CanGain(Player player) => FermentoryRules.NextSpecialSlot(player.MaxPotionCount, Get(player)) != null;

    /// <summary>
    /// Makes the player's leftmost normal slot special with a random effect, or does nothing when every slot is special.
    /// Seeded by the location, the player and how many special slots they already have.
    /// </summary>
    public static (int Slot, SlotEffect Effect)? MakeNextSpecial(Player player, string reason)
    {
        try
        {
            if (ZoneService.FindModifier(player.RunState) is not { } modifier)
            {
                return null;
            }

            var all = FermentoryRules.Decode(modifier.SpecialPotionSlots);
            IReadOnlyDictionary<int, SlotEffect> mine = all.TryGetValue(player.NetId, out var existing) ? existing : None;
            if (FermentoryRules.NextSpecialSlot(player.MaxPotionCount, mine) is not int slot)
            {
                return null;
            }

            IRunState runState = player.RunState;
            SlotEffect effect = FermentoryRules.RollEffect(runState.Rng.Seed, CurrentLocation(runState), player.NetId, mine.Count, runState.Players.Count > 1);
            var updated = mine.ToDictionary(pair => pair.Key, pair => pair.Value);
            updated[slot] = effect;
            all[player.NetId] = updated;
            modifier.SpecialPotionSlots = FermentoryRules.Encode(all.ToDictionary(pair => pair.Key, pair => pair.Value));
            Changed?.Invoke(player);
            return (slot, effect);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to make a special potion slot: {ex}");
            return null;
        }
    }

    /// <summary>The current map location key (act, node, room), used to seed Fermentory picks.</summary>
    public static string CurrentLocation(IRunState runState)
    {
        MapCoord? coord = runState.CurrentMapCoord;
        return MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, runState.CurrentRoom?.Id ?? 0);
    }
}
