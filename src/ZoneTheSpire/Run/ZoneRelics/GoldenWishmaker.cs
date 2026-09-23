using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Halls of Midas zone relic: a lethal hit can be capped at exactly enough HP loss to leave the owner at 1 HP, provided the
/// owner can pay 5 gold for every prevented point. BeforeDamageReceived arms only real damage (not previews); the final
/// HP-loss patch resolves against synced HP/gold after Block, redirection, other modifiers and Marbled, then the engine's
/// awaited after-modification hook pays through PlayerCmd before HP changes. If the full cost is unaffordable, neither the
/// damage nor the gold is changed.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class GoldenWishmaker : ModArtRelicModel
{
    protected override string TextureName => "golden_wishmaker";

    private bool _handlingOwnerDamage;
    private int _pendingGoldCost;

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "black_star";

    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner.Creature)
        {
            _handlingOwnerDamage = true;
            _pendingGoldCost = 0;
        }

        return Task.CompletedTask;
    }

    internal decimal ModifyFinalHpLoss(Creature target, decimal amount)
    {
        if (!_handlingOwnerDamage || target != Owner.Creature)
        {
            return amount;
        }

        GoldenWishmakerResult result = ZoneRelicEffects.GoldenWishmaker(
            target.CurrentHp,
            (int)decimal.Floor(amount),
            Owner.Gold);
        if (!result.Triggered)
        {
            return amount;
        }

        _pendingGoldCost = result.GoldCost;
        return result.HpLoss;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        int cost = _pendingGoldCost;
        _handlingOwnerDamage = false;
        _pendingGoldCost = 0;
        if (cost <= 0)
        {
            return;
        }

        Flash();
        await PlayerCmd.LoseGold(cost, Owner, GoldLossType.Lost);
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner.Creature)
        {
            _handlingOwnerDamage = false;
            _pendingGoldCost = 0;
        }

        return Task.CompletedTask;
    }
}
