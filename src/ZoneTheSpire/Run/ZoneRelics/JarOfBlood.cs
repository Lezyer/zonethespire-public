using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using ZoneTheSpire.Core.BloodRain;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// The Umbrella Seller event relic: the first time its owner's HP would be reduced to 0, they heal to 75% of their Max HP
/// instead, once. The same death-prevention hooks as vanilla Lizard Tail. An Event relic, so it only comes from the event.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class JarOfBlood : ModArtRelicModel
{
    private bool _wasUsed;

    protected override string TextureName => "jar_of_blood";

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override string IconBaseName => "blood_vial";

    public override bool IsUsedUp => _wasUsed;

    /// <summary>Whether the jar has been drunk. Saved with the run.</summary>
    [SavedProperty]
    public bool WasUsed
    {
        get => _wasUsed;
        set
        {
            AssertMutable();
            _wasUsed = value;
            if (_wasUsed)
            {
                Status = RelicStatus.Disabled;
            }
        }
    }

    public override bool ShouldDieLate(Creature creature) => creature != Owner.Creature || WasUsed;

    public override async Task AfterPreventingDeath(Creature creature)
    {
        Flash();
        WasUsed = true;
        await CreatureCmd.Heal(creature, BloodRainRules.JarHeal(creature.MaxHp));
    }
}
