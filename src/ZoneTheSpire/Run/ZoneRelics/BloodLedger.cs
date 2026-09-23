using System.Collections.Generic;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.BloodRain;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// The Tithe Collector event relic: in any shop, whatever its holder can't afford in gold they can pay for in HP instead, at
/// Blood Rain's rate of 1 HP per 15 gold (never a price that would kill them). Prices they can afford are paid in gold as
/// usual. Implemented with the Blood Rain shop's affordability, payment and price-display patches (BloodShop), which run on
/// every peer. An Event relic, so it only comes from the event.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class BloodLedger : ModArtRelicModel
{
    protected override string TextureName => "blood_ledger";

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "dusty_tome";

    internal static bool IsHeldBy(Player? player) => player?.GetRelic<BloodLedger>() != null;
}
