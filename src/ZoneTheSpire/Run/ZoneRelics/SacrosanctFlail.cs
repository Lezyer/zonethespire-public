using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Core.ZoneRelics;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Hallowed;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Blinding Hallows zone relic. Upon pickup, 3 random attacks in its owner's deck gain Hallowing and Blasphemous (preferring
/// attacks with neither; seeded per player, so every peer picks the same cards). Whenever its owner gains Hallowed, it applies
/// twice as much to a random enemy (vanilla's synced CombatTargets RNG), so Blasphemous and Zealous feed it. A gain Artifact
/// blocks is no gain. An Event relic, so it only comes from Blinding Hallows chests.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class SacrosanctFlail : ModArtRelicModel
{
    protected override string TextureName => "sacrosanct_flail";

    protected override string IconBaseName => "war_hammer";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (NestedTooltips.Enabled)
            {
                return Array.Empty<IHoverTip>();
            }

            var tips = new List<IHoverTip>();
            CardModifier.Get<HallowingModifier>().AddTips(tips);
            CardModifier.Get<BlasphemousModifier>().AddTips(tips);
            return tips;
        }
    }

    public override Task AfterObtained()
    {
        try
        {
            List<CardModel> attacks = Owner.Deck.Cards.Where(HallowingModifier.CanHallow).ToList();
            var fresh = attacks.Select(card => !HallowingModifier.Has(card) && !BlasphemousModifier.Has(card)).ToList();
            var changes = new List<CardChangePreview.Change>();
            foreach (int index in ZoneRelicEffects.PickSacrosanctFlailAttacks(Owner.RunState.Rng.Seed, Owner.NetId, fresh))
            {
                CardModel card = attacks[index];
                CardModel before = CardChangePreview.Snapshot(card);
                if (HallowingModifier.TryAdd(card) | BlasphemousModifier.TryAdd(card))
                {
                    changes.Add(new CardChangePreview.Change(before, CardChangePreview.Snapshot(card)));
                }
            }

            if (changes.Count > 0)
            {
                Flash();
                if (LocalContext.IsMe(Owner))
                {
                    // Local display only: never awaited, so the synced pickup doesn't wait on a click.
                    _ = CardChangePreview.Show(global::ZoneTheSpire.Run.Localization.ModLocalization.GameText("relics", Id.Entry + ".title"), changes);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Sacrosanct Flail failed to consecrate attacks: {ex}");
        }

        return Task.CompletedTask;
    }

    /// <summary>Its owner gained Hallowed: twice as much goes to a random enemy.</summary>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        try
        {
            int hallowed = ZoneRelicEffects.SacrosanctFlailHallowed((int)amount);
            if (power is not HallowedPower || power.Owner != Owner.Creature || !CombatManager.Instance.IsInProgress
                || hallowed <= 0 || Owner.Creature.CombatState is not { } combatState)
            {
                return;
            }

            List<Creature> targets = combatState.HittableEnemies.Where(enemy => enemy.IsAlive).ToList();
            if (targets.Count == 0)
            {
                return;
            }

            Creature target = Owner.RunState.Rng.CombatTargets.NextItem(targets)!;
            Flash();
            await PowerCmd.Apply<HallowedPower>(choiceContext, target, hallowed, Owner.Creature, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Sacrosanct Flail failed to pass judgement: {ex}");
        }
    }
}
