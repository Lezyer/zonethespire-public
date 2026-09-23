using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using ZoneTheSpire.Core.Biomes;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Scrapyard (Monster, Elite, "?" fights): every player gets an extra, skippable card removal reward (trashing a card in
/// the scrapyard), and 1–2 floating scrap bots join the fight once it has started.
/// </summary>
internal sealed class ScrapyardFightsHandler : ZoneEffectHandler
{
    public override string EffectId => ScrapyardBiome.ScrapBotsEffectId;

    public override Task OnCombatRoomEntered(CombatRoom room, ZoneContext context)
    {
        try
        {
            foreach (Player player in room.CombatState.Players)
            {
                room.AddExtraReward(player, new CardRemovalReward(player));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the Scrapyard card removal reward: {ex}");
        }

        return Task.CompletedTask;
    }

    /// <summary>Bots are added once combat is in progress (CreatureCmd.Add refuses earlier, e.g. on room entry).</summary>
    public override Task OnBeforeCombatStart(CombatRoom room, ZoneContext context) => ScrapyardBots.SpawnFor(room);
}
