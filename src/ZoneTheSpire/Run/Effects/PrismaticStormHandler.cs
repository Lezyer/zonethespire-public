using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Prismatic;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Prismatic Storm (Monster, Elite, "?" fights): every enemy present when combat starts gets one random buff (picked from
/// the run seed, map location and its combat order, so identical on every peer) and a rainbow prism sheen; every player
/// gets two extra card rewards drawn from all character card pools plus colourless.
/// </summary>
internal sealed class PrismaticStormHandler : ZoneEffectHandler
{
    private const int ExtraCardRewards = 2;

    public override string EffectId => PrismaticStormBiome.StormBuffsEffectId;

    public override Task OnCombatRoomEntered(CombatRoom room, ZoneContext context)
    {
        try
        {
            RoomType rewardRoom = room.RoomType == RoomType.Elite ? RoomType.Elite : RoomType.Monster;
            List<CardPoolModel> pools = ModelDb.AllCharacterCardPools
                .Append(ModelDb.CardPool<ColorlessCardPool>())
                .Distinct()
                .ToList();
            foreach (Player player in room.CombatState.Players)
            {
                for (int i = 0; i < ExtraCardRewards; i++)
                {
                    CardCreationOptions options = CardCreationOptions.ForRoom(player, rewardRoom).WithCardPools(pools);
                    room.AddExtraReward(player, new CardReward(options, 3, player));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to add the Prismatic Storm card rewards: {ex}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Buffs are applied once combat has started, after each enemy's own setup, so a buff it already has is added to. They go
    /// through PowerCmd.Apply, which applies the game's own multiplayer scaling to the powers that opt into it (Curl Up, Regen,
    /// Plating, Slippery: amount x player count x act factor, for any player count).
    /// </summary>
    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        CombatState state = room.CombatState;
        IRunState runState = state.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, room.Id ?? 0);
        List<Creature> enemies = state.Enemies.Where(enemy => enemy.IsAlive).ToList();
        for (int i = 0; i < enemies.Count; i++)
        {
            Creature enemy = enemies[i];
            PrismaticBuff buff = PrismaticRules.PickBuff(runState.Rng.Seed, location, i);
            // Curl Up is scaled for multiplayer by the game, so base it on HP before multiplayer scaling (no double scaling).
            int baseMaxHp = enemy.MonsterMaxHpBeforeModification ?? enemy.MaxHp;
            int amount = PrismaticRules.BuffAmount(buff, runState.CurrentActIndex, baseMaxHp);
            try
            {
                await ApplyBuff(enemy, buff, amount);
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to apply Prismatic Storm {buff} to {enemy.Monster?.Id.Entry}: {ex}");
            }

            PrismMaterial.Apply(enemy);
        }
    }

    private static Task ApplyBuff(Creature enemy, PrismaticBuff buff, int amount)
    {
        var context = new ThrowingPlayerChoiceContext();
        decimal value = amount;
        Task task = buff switch
        {
            PrismaticBuff.CurlUp => PowerCmd.Apply<CurlUpPower>(context, enemy, value, null, null),
            PrismaticBuff.Enrage => PowerCmd.Apply<EnragePower>(context, enemy, value, null, null),
            PrismaticBuff.Ritual => PowerCmd.Apply<RitualPower>(context, enemy, value, null, null),
            PrismaticBuff.Strength => PowerCmd.Apply<StrengthPower>(context, enemy, value, null, null),
            PrismaticBuff.Regen => PowerCmd.Apply<RegenPower>(context, enemy, value, null, null),
            PrismaticBuff.Intangible => PowerCmd.Apply<IntangiblePower>(context, enemy, value, null, null),
            PrismaticBuff.Thorns => PowerCmd.Apply<ThornsPower>(context, enemy, value, null, null),
            PrismaticBuff.Plating => PowerCmd.Apply<PlatingPower>(context, enemy, value, null, null),
            PrismaticBuff.PersonalHive => PowerCmd.Apply<PersonalHivePower>(context, enemy, value, null, null),
            _ => PowerCmd.Apply<SlipperyPower>(context, enemy, value, null, null),
        };
        return task;
    }
}
