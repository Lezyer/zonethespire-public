using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.BloodRain;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// The Umbrella Seller event relic: its owner gains 1 Buffer (the vanilla power: prevents the next HP loss) at the start of
/// each combat. Part of the synchronized combat-start hook on every peer. An Event relic, so it only comes from the event.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class BlackUmbrella : ModArtRelicModel
{
    protected override string TextureName => "black_umbrella";

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "lords_parasol";

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<BufferPower>() };

    public override async Task BeforeCombatStart()
    {
        try
        {
            Flash();
            await PowerCmd.Apply<BufferPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, BloodRainRules.UmbrellaBuffer, Owner.Creature, null);
        }
        catch (Exception ex)
        {
            Log.Warn($"Black Umbrella failed to grant Buffer: {ex}");
        }
    }
}
