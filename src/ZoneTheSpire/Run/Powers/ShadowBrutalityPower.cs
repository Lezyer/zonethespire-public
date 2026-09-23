using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Shadow Corruption: each of the owner's attacks also gives the player it hits Doom equal to 50% of the attack's full damage
/// (after Strength, Vulnerable and the like, before Block; rounded down, at least 1). It runs when the damage is about to be
/// received, before Block, Marbled, Osty or a Wriggler guard absorb any of it, so absorbed damage still counts. An attack on a
/// player's pet dooms its owner. The enemy is wreathed in a shadow aura (ShadowAura). Its icon is served by BloodDrinkerIconPatch
/// (Strength remains the fallback).
/// </summary>
public sealed class ShadowBrutalityPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<StrengthPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        NestedTooltips.Enabled ? Array.Empty<IHoverTip>() : new[] { HoverTipFactory.FromPower<DoomPower>() };

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ShadowAura.Attach(Owner);
        return Task.CompletedTask;
    }

    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        try
        {
            int doom = ShadowRules.BrutalityDoom(amount, props.IsPoweredAttack());
            if (dealer != Owner || doom <= 0 || (target.Player ?? target.PetOwner) is not Player player || player.Creature is not { IsDead: false } creature)
            {
                return;
            }

            await PowerCmd.Apply<DoomPower>(choiceContext, creature, doom, Owner, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Shadow Brutality failed to apply Doom: {ex}");
        }
    }
}
