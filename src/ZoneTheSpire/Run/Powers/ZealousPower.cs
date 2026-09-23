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
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Blinding Hallows: each of the owner's attacks also gives the player it hits Hallowed equal to 15% of the attack's full
/// damage (before Block; rounded down, at least 1), even when blocked. An attack on a pet hallows its owner. Shadow
/// Brutality's pattern. Its icon is served by BloodDrinkerIconPatch (Strength is the fallback).
/// </summary>
public sealed class ZealousPower : CustomPowerModel
{
    private static readonly string Text = HallowedText.ZealousDescription;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<StrengthPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<StrengthPower>().ResolvedBigIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        NestedTooltips.Enabled ? Array.Empty<IHoverTip>() : new[] { HoverTipFactory.FromPower<HallowedPower>() };

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        HolyVisuals.AttachHalo(Owner);
        return Task.CompletedTask;
    }

    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        try
        {
            int hallowed = HallowedRules.ZealousHallowed(amount, props.IsPoweredAttack());
            if (dealer != Owner || hallowed <= 0 || (target.Player ?? target.PetOwner) is not Player player || player.Creature is not { IsDead: false } creature)
            {
                return;
            }

            await PowerCmd.Apply<HallowedPower>(choiceContext, creature, hallowed, Owner, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Zealous failed to apply Hallowed: {ex}");
        }
    }
}
