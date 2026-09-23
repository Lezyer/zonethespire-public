using System.Collections.Generic;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.ZoneEvents;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// The Troubled Pilgrim event relic: once in each fight, when its holder would die, they don't. They become untouchable behind a
/// full purple infinite health bar and keep playing for 2 more of their own turns (see WorldlyAttachmentPower). Finish the fight
/// in that time and they live, healing 10% of their Max HP; when the turns run out they die there and then, which in co-op is an
/// ordinary death (their allies can still finish the fight and bring them back at 1 HP). It is the last word on dying: every
/// relic and potion that saves its owner, like Lizard Tail or Fairy in a Bottle, gets its chance first. An Event relic, so it
/// only comes from the event.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class WorldlyAttachment : ModArtRelicModel
{
    protected override string TextureName => "worldly_attachment";

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "signet_ring";

    internal static bool IsHeldBy(Player? player) => player?.GetRelic<WorldlyAttachment>() != null;
}
