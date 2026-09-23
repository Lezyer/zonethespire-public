using System;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace ZoneTheSpire.Run.Effects;

internal static class CloneLoot
{
    /// <summary>
    /// A dying gremlin's Surprise normally spawns a Fat Gremlin carrying Heist, which gives stolen gold back as a reward
    /// when killed. A clone can't spawn it, so give the gold back now, exactly as HeistPower.BeforeDeath does.
    /// </summary>
    public static void ReturnStolenGold(Creature clone)
    {
        foreach (ThieveryPower thievery in clone.GetPowerInstances<ThieveryPower>())
        {
            try
            {
                int gold = thievery.DynamicVars.Gold.IntValue;
                Player? player = thievery.Target?.Player;
                if (gold <= 0 || player == null || clone.CombatState == null)
                {
                    continue;
                }

                if (clone.CombatState.RunState.CurrentRoom is CombatRoom room)
                {
                    room.AddExtraReward(player, new GoldReward(gold, player, wasGoldStolenBack: true));
                }

                clone.CombatState.RunState.CurrentMapPointHistoryEntry?.GetEntry(player.NetId).MarkLootReturned(gold);
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to return gold stolen by a mirrored clone: {ex}");
            }
        }
    }
}
