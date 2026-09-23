using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Shadow;

namespace ZoneTheSpire.Run.Shadow;

/// <summary>
/// Dawn: coming out of Shadow Corruption pays every player for each zone node on that stay (N): 25 gold and 10% healing per
/// node, plus a random relic when N is 3 or more. The stay is a saved set of node keys, updated on every peer when a room is
/// entered. The payout happens straight away on arrival at the first node outside the zone, whatever it is.
/// </summary>
internal static class ShadowDawn
{
    public static async Task OnRoomEntered(IRunState runState, AbstractRoom room)
    {
        try
        {
            if (ZoneService.FindModifier(runState) is not { } modifier || runState.CurrentMapCoord is not { } coord)
            {
                return;
            }

            if (ZoneContext.Current(runState)?.Biome is ShadowCorruptionBiome)
            {
                string key = ShadowRules.NodeKey(runState.CurrentActIndex, coord.row, coord.col);
                modifier.ShadowStay = ShadowRules.AddToNodeSet(modifier.ShadowStay, new[] { key });
                return;
            }

            int nodes = ShadowRules.ParseNodeSet(modifier.ShadowStay).Count;
            if (nodes == 0)
            {
                return;
            }

            modifier.ShadowStay = string.Empty;
            foreach (Player player in runState.Players)
            {
                await PayOut(player, nodes);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Dawn failed: {ex}");
        }
    }

    private static async Task PayOut(Player player, int nodes)
    {
        try
        {
            await PlayerCmd.GainGold(ShadowRules.DawnGold(nodes), player);

            int heal = ShadowRules.DawnHeal(player.Creature.MaxHp, nodes);
            if (heal > 0 && !player.Creature.IsDead)
            {
                await CreatureCmd.Heal(player.Creature, heal);
            }

            if (ShadowRules.DawnGivesRelic(nodes))
            {
                await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(player).ToMutable(), player);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Dawn payout failed for player {player.NetId}: {ex}");
        }
    }
}
