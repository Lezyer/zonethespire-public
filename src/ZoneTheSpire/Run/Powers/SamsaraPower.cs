using System.Collections.Generic;
using System.Linq;
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
using System;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Core.Devas;
using ZoneTheSpire.Run.Devas;

namespace ZoneTheSpire.Run.Powers;

/// <summary>
/// Deva's Domain (every player, whole fight): each card the player plays loses 1 Karma after any discount is spent (it can go
/// negative, raising the cost); at the end of the player's turn, cards left unplayed in hand with negative Karma get 1 back
/// (never above 0). The play side runs in <see cref="Karma.BeforeCardPlayed"/>, so it always comes after the discount and the X
/// bonus of the same play. A buff, so Artifact never blocks it and nothing that clears debuffs removes it. Its icon is served by
/// BloodDrinkerIconPatch.
/// </summary>
public sealed class SamsaraPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => ModelDb.Power<LoopPower>().PackedIconPath;

    public override string? CustomBigIconPath => ModelDb.Power<LoopPower>().ResolvedBigIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => NestedTooltips.Enabled ? Array.Empty<IHoverTip>() : new[] { Karma.Tip };

    /// <summary>Whether the player has Samsara in the current fight.</summary>
    public static bool IsOn(Player player) => player.Creature is { } creature && creature.HasPower<SamsaraPower>();

    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner.Player is { } player && participants.Contains(Owner))
        {
            Karma.RecoverUnplayed(player);
        }

        return Task.CompletedTask;
    }
}
