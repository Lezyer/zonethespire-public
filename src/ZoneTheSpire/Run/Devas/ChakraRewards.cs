using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Core.Mirror;

namespace ZoneTheSpire.Run.Devas;

/// <summary>
/// Inside Deva's Domain, one Attack or Skill of every card reward gets Chakra 1-3, picked from the run seed, node, player and the
/// reward's cards (the same on every peer). Rewards that already hold a Chakra card are left alone (CardReward.Populate can
/// re-invoke the hook), as are rewards that forbid card modifications.
/// </summary>
internal static class ChakraRewards
{
    public static bool TryAddToReward(Player player, List<CardCreationResult> options, CardCreationOptions creationOptions)
    {
        try
        {
            IRunState runState = player.RunState;
            if (ZoneContext.Current(runState)?.Biome is not DevasDomainBiome
                || creationOptions.Flags.HasFlag(CardCreationFlags.NoCardModelModifications)
                || options.Any(option => ChakraModifier.AmountOf(option.Card) > 0))
            {
                return false;
            }

            List<CardModel> eligible = options.Select(option => option.Card).Where(ChakraModifier.CanHave).ToList();
            MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
            string optionsKey = string.Join(",", options.Select(option => option.Card.Id.Entry));
            (int index, int chakra) = KarmaRules.PickRewardChakra(runState.Rng.Seed, location, player.NetId, optionsKey, eligible.Count);
            if (index < 0 || !ChakraModifier.TryAdd(eligible[index], chakra))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add Chakra to a Deva's Domain card reward: {ex}");
            return false;
        }
    }
}
