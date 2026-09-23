using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Mirrorlands (Monster, Elite, "?" fights): one random mirrorable enemy gets Mirrored and splits at half health; post-fight
/// card rewards get Glam.
/// </summary>
internal sealed class MirroredFoesHandler : ZoneEffectHandler
{
    public override string EffectId => MirrorlandsBiome.MirroredFoesEffectId;

    /// <summary>
    /// Picks the Mirrored enemy from the run seed and the room id (identical on every peer, no game RNG consumed). Enemies
    /// added later (summons, clones) are never mirrored. Clone HP is 25% when the fight started with one mirrorable enemy,
    /// otherwise 50%. Decimillipede segments can't be mirrored, so each one gets +15% Max HP and a marker instead.
    /// </summary>
    public override async Task OnCombatRoomEntered(CombatRoom room, ZoneContext context)
    {
        foreach (Creature segment in room.CombatState.Enemies.Where(enemy => enemy.IsAlive && enemy.Monster is DecimillipedeSegment).ToList())
        {
            if (segment.HasPower<MirroredCarapacePower>())
            {
                continue;
            }

            // Runs after DecimillipedeSegment.AfterAddedToRoom set each segment's distinct HP; rounding up keeps them distinct.
            await CreatureCmd.SetMaxAndCurrentHp(segment, MirrorRules.BoostedMaxHp(segment.MaxHp, MirrorRules.SegmentMaxHpBonusPercent));
            await PowerCmd.Apply<MirroredCarapacePower>(new ThrowingPlayerChoiceContext(), segment, 1m, null, null);
        }

        List<Creature> candidates = room.CombatState.Enemies
            .Where(enemy => enemy.IsAlive && IsMirrorable(enemy) && !CloneIdentity.IsClone(enemy))
            .ToList();
        int index = MirrorRules.PickMirroredIndex(room.CombatState.RunState.Rng.Seed, room.Id ?? 0, candidates.Count);
        if (index < 0 || candidates.Any(enemy => enemy.HasPower<MirroredPower>()))
        {
            return;
        }

        // The power's amount is the clone HP percent, so its hover text can show it as {Amount}.
        decimal clonePercent = MirrorRules.CloneHpPercent(candidates.Count);
        await PowerCmd.Apply<MirroredPower>(new ThrowingPlayerChoiceContext(), candidates[index], clonePercent, null, null);
    }

    public override bool TryModifyCardReward(Player player, List<CardCreationResult> options, CardCreationOptions creationOptions, ZoneContext context)
    {
        if (creationOptions.Source != CardCreationSource.Encounter
            || creationOptions.Flags.HasFlag(CardCreationFlags.NoCardModelModifications))
        {
            return false;
        }

        // CardReward.Populate can re-invoke this hook for pre-set rewards: never add a second Glam.
        if (options.Any(option => option.Card.Enchantment is Glam))
        {
            return false;
        }

        try
        {
            // Exactly one card gets Glam, chosen from the options it can enchant (relic-enchanted cards are skipped).
            Glam glam = ModelDb.Enchantment<Glam>();
            List<CardModel> eligible = options.Select(option => option.Card)
                .Where(card => card.Enchantment == null && glam.CanEnchant(card))
                .ToList();
            IRunState runState = player.RunState;
            MegaCrit.Sts2.Core.Map.MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, runState.CurrentRoom?.Id ?? 0);
            int index = MirrorRules.PickGlamIndex(runState.Rng.Seed, location, player.NetId, eligible.Count);
            if (index < 0)
            {
                return false;
            }

            CardCmd.Enchant<Glam>(eligible[index], 1m);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add Glam to a Mirrorlands card reward: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Minions (secondary enemies) and Decimillipede segments are never mirrored: cloning them would change
    /// fight-ending and revive rules that vanilla encounters depend on.
    /// </summary>
    private static bool IsMirrorable(Creature creature) =>
        !creature.IsSecondaryEnemy && creature.Monster is not DecimillipedeSegment;
}
