using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Blood Rain zone relic: its owner heals for 5% of the unblocked damage they (or their pets) deal to enemies. Uses the HP the
/// enemy actually lost, so overkill damage never heals. Fractions carry over between hits for the rest of the combat, so many
/// small hits heal as much as one big one. Runs inside the synced damage hook, so every peer heals identically. An Event
/// relic, so it only comes from Blood Rain chests.
/// Icons: textures/crimson_tooth.png, crimson_tooth_outline.png and crimson_tooth_big.png (editable; placeholder copies
/// of the vanilla icon named by IconBaseName, which is also the fallback).
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class CrimsonTooth : ModArtRelicModel
{
    protected override string TextureName => "crimson_tooth";

    /// <summary>Unblocked damage dealt to enemies this combat (resets each combat; not saved).</summary>
    private int _damageThisCombat;

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "razor_tooth";

    public override Task BeforeCombatStart()
    {
        _damageThisCombat = 0;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _damageThisCombat = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        try
        {
            Creature? owner = Owner.Creature;
            if (dealer == null || owner == null || owner.IsDead || !result.Receiver.IsEnemy
                || (dealer != owner && dealer.PetOwner != Owner))
            {
                return;
            }

            int heal = ZoneRelicEffects.CrimsonToothHeal(_damageThisCombat, result.UnblockedDamage);
            _damageThisCombat += Math.Max(0, result.UnblockedDamage);
            if (heal > 0)
            {
                Flash();
                await CreatureCmd.Heal(owner, heal);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Crimson Tooth failed to heal: {ex}");
        }
    }
}
