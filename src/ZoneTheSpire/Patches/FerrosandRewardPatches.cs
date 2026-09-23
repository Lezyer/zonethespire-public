using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;
using ZoneTheSpire.Run.Ferrosand;

namespace ZoneTheSpire.Patches;

/// <summary>
/// A won combat is saved with its extra rewards, and Reward.FromSerializable throws for reward types it doesn't know. Loading a
/// run saved on a Ferrosand reward screen rebuilds the Magnetize reward here instead.
/// </summary>
[HarmonyPatch(typeof(Reward), nameof(Reward.FromSerializable))]
internal static class MagnetizeRewardLoadPatch
{
    private static bool Prefix(SerializableReward save, Player player, ref Reward __result)
    {
        try
        {
            if (save.RewardType != MagnetizeReward.Type)
            {
                return true;
            }

            __result = new MagnetizeReward(player);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to load a saved Magnetize reward: {ex}");
            return true;
        }
    }
}
