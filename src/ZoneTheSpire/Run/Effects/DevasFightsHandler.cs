using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Run.Devas;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Deva's Domain (Monster, Elite, "?" fights): every player gets Samsara at the start of the fight, and every enemy (also those
/// added mid-fight) gets Deva's Blessing unless it already comes back from death on its own (see DevasBlessing). Everything
/// runs in synced combat hooks.
/// </summary>
internal sealed class DevasFightsHandler : ZoneEffectHandler
{
    public override string EffectId => DevasDomainBiome.DevasFightsEffectId;

    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Player player in room.CombatState.Players)
        {
            try
            {
                if (player.Creature is { IsDead: false } creature && !creature.HasPower<SamsaraPower>())
                {
                    await PowerCmd.Apply<SamsaraPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to give player {player.NetId} Samsara: {ex}");
            }
        }

        foreach (Creature enemy in room.CombatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
        {
            await DevasBlessing.TryGive(enemy);
        }
    }

    public override async Task OnCreatureAddedToCombat(Creature creature, ZoneContext context)
    {
        if (CombatManager.Instance.IsInProgress)
        {
            await DevasBlessing.TryGive(creature);
        }
    }
}

/// <summary>Deva's Domain campfires: Meditate is added (it uses the campfire action).</summary>
internal sealed class DevasCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => DevasDomainBiome.MeditateEffectId;

    public override bool TryModifyRestSiteOptions(Player player, System.Collections.Generic.ICollection<MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption> options, ZoneContext context)
    {
        options.Add(new Campfire.MeditateRestSiteOption(player));
        return true;
    }
}
