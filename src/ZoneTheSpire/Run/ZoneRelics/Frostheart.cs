using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.ZoneRelics;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Hoarfrost zone relic: at the start of each of its owner's turns the cold spreads out from it, giving every living enemy 5
/// Biting Cold and its owner 3. Everything iced takes that much more damage from each attack and loses 1 of it per hit, so the
/// enemies melt faster and so does its owner. Both sides stack turn on turn, which the enemies work off by being hit and the
/// owner works off by being hit. An Event relic, so it only comes from Hoarfrost chests.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class Frostheart : ModArtRelicModel
{
    protected override string TextureName => "frostheart";

    protected override string IconBaseName => "frozen_egg";

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<BitingColdPower>() };

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        try
        {
            if (side != CombatSide.Player || !participants.Contains(Owner.Creature) || !CombatManager.Instance.IsInProgress)
            {
                return;
            }

            foreach (Creature enemy in combatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
            {
                await PowerCmd.Apply<BitingColdPower>(new ThrowingPlayerChoiceContext(), enemy, ZoneRelicEffects.FrostheartEnemyCold, Owner.Creature, null);
            }

            if (Owner.Creature is { IsDead: false } owner)
            {
                await PowerCmd.Apply<BitingColdPower>(new ThrowingPlayerChoiceContext(), owner, ZoneRelicEffects.FrostheartSelfCold, owner, null);
            }

            Flash();
        }
        catch (Exception ex)
        {
            Log.Warn($"Frostheart failed to spread the cold: {ex}");
        }
    }
}
