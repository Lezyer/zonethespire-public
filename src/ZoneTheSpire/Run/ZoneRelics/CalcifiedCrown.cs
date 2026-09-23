using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.ForgottenEmpire;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// The Forgotten Emperor event relic. At the start of each of its owner's turns (turn 1 excluded: the game never clears Block
/// then), half of the Block about to be cleared, rounded down, becomes Marbled. Runs in BeforeSideTurnStart, just before the
/// game clears Block, and uses the game's own ShouldClearBlock check, so Block kept by Barricade-style effects is left alone.
/// Part of the synchronized turn flow, so every peer converts the same amount. An Event relic, so it only comes from the event.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class CalcifiedCrown : ModArtRelicModel
{
    protected override string TextureName => "calcified_crown";

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "circlet";

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<MarbledPower>() };

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        try
        {
            Creature creature = Owner.Creature;
            if (side != CombatSide.Player
                || !participants.Contains(creature)
                || creature.IsDead
                || (Owner.PlayerCombatState?.TurnNumber ?? 1) <= 1
                || creature.Block <= 0
                || !Hook.ShouldClearBlock(combatState, creature, out _))
            {
                return;
            }

            int marbled = ForgottenEmpireRules.CrownMarbled(creature.Block);
            if (marbled <= 0)
            {
                return;
            }

            Flash();
            await MarbledPower.Add(creature, marbled, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Calcified Crown failed: {ex}");
        }
    }
}
