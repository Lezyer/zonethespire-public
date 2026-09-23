using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.ZoneRelics;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Forgotten Empire zone relic: grants its owner 5 Polishing at the start of combat. Polishing already has a player path:
/// the player gains its current amount as Marbled at end of turn, then it decrements at the start of each later player turn.
/// The power application is part of the synchronized combat-start hook on every peer.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class MarblePauldrons : ModArtRelicModel
{
    protected override string TextureName => "marble_pauldrons";

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "oddly_smooth_stone";

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<PolishingPower>(),
        HoverTipFactory.FromPower<MarbledPower>(),
    };

    public override async Task BeforeCombatStart()
    {
        try
        {
            Flash();
            await PowerCmd.Apply<PolishingPower>(
                new ThrowingPlayerChoiceContext(),
                Owner.Creature,
                ZoneRelicEffects.MarblePauldronsPolishing,
                Owner.Creature,
                null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Marble Pauldrons failed to grant Polishing: {ex}");
        }
    }
}
