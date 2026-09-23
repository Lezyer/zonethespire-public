using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Run.Powers;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Shadow Corruption (Monster, Elite, "?" fights): every enemy has Shadow Brutality (its attacks also give Doom; enemies added
/// mid-fight too); 5 Light the Way are shuffled into each player's draw pile before the first draw; at the start of each player
/// turn one more card in that player's hand is shaded (just before the hand draw). Everything runs in synced combat hooks; hidden intents and shaded
/// faces are local presentation (ShadowSight, ShadedCards).
/// </summary>
internal sealed class ShadowFightsHandler : ZoneEffectHandler
{
    public override string EffectId => ShadowCorruptionBiome.ShadowFightsEffectId;

    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Creature enemy in room.CombatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
        {
            await GiveBrutality(enemy);
        }

        foreach (Player player in room.CombatState.Players)
        {
            try
            {
                var lights = new List<CardModel>();
                for (int i = 0; i < ShadowRules.LightTheWayCopies; i++)
                {
                    lights.Add(room.CombatState.CreateCard<LightTheWay>(player));
                }

                await CardPileCmd.AddGeneratedCardsToCombat(lights, PileType.Draw, player, CardPilePosition.Random);
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to shuffle Light the Way into player {player.NetId}'s draw pile: {ex}");
            }
        }
    }

    public override async Task OnCreatureAddedToCombat(Creature creature, ZoneContext context)
    {
        if (CombatManager.Instance.IsInProgress)
        {
            await GiveBrutality(creature);
        }
    }

    public override Task OnAfterPlayerTurnStart(Player player, ZoneContext context)
    {
        ShadedCards.ShadeInHand(player);

        // A Light the Way reveal ends with the turn: redraw what's drawn from the intents on this machine (local only).
        if (LocalContext.IsMe(player) && player.Creature.CombatState is { } combat)
        {
            ShadowSight.RefreshLocalHealthBars(combat);
        }

        return Task.CompletedTask;
    }

    private static async Task GiveBrutality(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || !creature.IsMonster || !creature.IsAlive || creature.HasPower<ShadowBrutalityPower>())
        {
            return;
        }

        await PowerCmd.Apply<ShadowBrutalityPower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
    }
}
