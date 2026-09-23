using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Hallowed;

/// <summary>
/// The Sacred Tribunal's Devote card: 2 cost attack dealing damage equal to the target's Hallowed (like vanilla Body Slam with
/// Block: the Hallowed is the base damage, so Strength, Vulnerable and the like still apply; the preview shows the real number
/// against the hovered enemy). The Hallowed stays on the target. Upgraded, it gains Retain. In the event card pool with Event
/// rarity, which the game never draws random cards from.
/// </summary>
[Pool(typeof(EventCardPool))]
public sealed class InquisitorsWrath : CustomCardModel
{
    public InquisitorsWrath()
        : base(TribunalRules.InquisitorsWrathCost, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy)
    {
    }

    public override bool CanBeGeneratedByModifiers => false;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(0m),
        new ExtraDamageVar(1m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(static (_, target) => HallowedOf(target)),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        NestedTooltips.Enabled ? Array.Empty<IHoverTip>() : new[] { HoverTipFactory.FromPower<HallowedPower>() };

    public override Texture2D? CustomPortrait => ModTextures.GetCustom("inquisitors_wrath.png");

    /// <summary>A target's Hallowed (0 without it, or with no target yet).</summary>
    private static decimal HallowedOf(Creature? target) => target?.GetPower<HallowedPower>()?.Amount ?? 0;

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this, cardPlay).Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
    }
}
