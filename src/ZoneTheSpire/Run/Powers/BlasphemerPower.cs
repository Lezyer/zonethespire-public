using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Core.Hallowed;
using ZoneTheSpire.Run.Hallowed;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Blasphemer (every player, Blinding Hallows fights): after each turn-start draw, 2 random hand cards become Blasphemous until
/// they leave the hand or the enemy turn begins; playing one gives 5 Hallowed. A Buff on purpose (like Samsara), so Artifact
/// never blocks it. Its icon is served by BloodDrinkerIconPatch.
/// </summary>
public sealed class BlasphemerPower : CustomPowerModel
{
    private static readonly string Text = HallowedText.BlasphemerDescription;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<DoomPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<DoomPower>().ResolvedBigIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        NestedTooltips.Enabled ? Array.Empty<IHoverTip>() : new[] { HoverTipFactory.FromPower<HallowedPower>() };

    public static bool IsOn(Player player) => player.Creature is { } creature && creature.HasPower<BlasphemerPower>();

    /// <summary>The game fires this right after the turn-start hand draw.</summary>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.Player == player)
        {
            BlasphemousMark.MarkAfterHandDraw(player);
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Enemy && Owner.Player is { } player)
        {
            BlasphemousMark.Clear(player);
        }

        return Task.CompletedTask;
    }
}
