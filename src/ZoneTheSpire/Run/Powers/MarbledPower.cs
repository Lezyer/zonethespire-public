using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.ForgottenEmpire;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Forgotten Empire: a layer of marble (statues start with 30% of their original Max HP; Marbled cards add their Block) that
/// takes damage right after Block, before Osty or a Wriggler guard and HP (MarbledHpLossPatch absorbs it at the end of the
/// synced "before Osty" HP loss hooks, so absorbed damage never counts as unblocked damage or reaches a redirect). It lasts the
/// whole combat and never grows on its own; Forgotten Statues gain more each turn from Polishing. The power itself is hidden; it
/// is shown as a white shield on the right of the health bar (MarbleHealthBar), and enemies with any Marbled look like marble
/// statues until it breaks.
/// </summary>
public sealed class MarbledPower : CustomPowerModel
{
    private bool _statueLook;

    /// <summary>Set by the hit that broke a Forgotten Statue's Marbled; consumed by AfterDamageReceived.</summary>
    private bool _statueBroken;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override string? CustomPackedIconPath => ModelDb.Power<RollingBoulderPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<RollingBoulderPower>().ResolvedBigIconPath;

    /// <summary>Gives the creature more Marbled (stacking with any it has). Runs inside synced commands only.</summary>
    public static async Task Add(Creature creature, int amount, CardModel? cardSource)
    {
        if (amount <= 0 || creature.IsDead)
        {
            return;
        }

        await PowerCmd.Apply<MarbledPower>(new ThrowingPlayerChoiceContext(), creature, amount, null, cardSource, silent: true);
        creature.GetPower<MarbledPower>()?.OnMarbledChanged(absorbed: 0, grew: true);
    }

    /// <summary>Takes as much of this HP loss as the Marbled left allows and returns the HP loss that gets through.</summary>
    internal decimal Absorb(decimal hpLoss)
    {
        int absorbed = ForgottenEmpireRules.AbsorbedDamage(Amount, (int)decimal.Floor(hpLoss));
        if (absorbed <= 0)
        {
            return hpLoss;
        }

        SetAmount(Amount - absorbed, silent: true);
        // A statue that loses its Marbled also loses its Polishing (removed with the statue in AfterDamageReceived).
        if (Amount <= 0 && Owner.IsEnemy && Owner.HasPower<ForgottenStatuePower>())
        {
            // The HP loss hook can't run commands; the debuffs follow in AfterDamageReceived for this same hit.
            _statueBroken = true;
        }

        OnMarbledChanged(absorbed, grew: false);
        return hpLoss - absorbed;
    }

    /// <summary>A Forgotten Statue whose Marbled just broke stops being a statue and is left Weak 1 and Vulnerable 1.</summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || !_statueBroken)
        {
            return;
        }

        _statueBroken = false;
        if (Owner.IsDead)
        {
            return;
        }

        await PowerCmd.Remove<ForgottenStatuePower>(Owner);
        await PowerCmd.Remove<PolishingPower>(Owner);
        await PowerCmd.Apply<WeakPower>(choiceContext, Owner, ForgottenEmpireRules.BrokenStatueWeak, null, null);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner, ForgottenEmpireRules.BrokenStatueVulnerable, null, null);
    }

    /// <summary>Local visuals only: health bar shield, statue look, chips and the shatter when it breaks.</summary>
    private void OnMarbledChanged(int absorbed, bool grew)
    {
        MarbleHealthBar.Refresh(Owner, pulse: grew);
        if (absorbed > 0)
        {
            MarbleSounds.Hit();
        }

        if (!Owner.IsEnemy)
        {
            return;
        }

        if (Amount > 0)
        {
            if (!_statueLook)
            {
                _statueLook = true;
                MarbleMaterial.Apply(Owner);
            }

            if (absorbed > 0)
            {
                StatueShatterVisual.Chip(Owner);
            }
        }
        else if (_statueLook)
        {
            _statueLook = false;
            CreatureShaderMaterial.Remove(Owner);
            StatueShatterVisual.Shatter(Owner);
        }
    }
}
