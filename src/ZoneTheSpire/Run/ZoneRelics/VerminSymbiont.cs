using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Run.Wriggling;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Infestation zone relic: at the start of each combat, Wriggling 10 summons a Wriggler (or grows the living one). An Event
/// relic, so it never drops from normal relic pools; it only comes from Infestation chests.
/// Icons: textures/vermin_symbiont.png, vermin_symbiont_outline.png and vermin_symbiont_big.png (editable; placeholder copies
/// of the vanilla icon named by IconBaseName, which is also the fallback).
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class VerminSymbiont : ModArtRelicModel
{
    protected override string TextureName => "vermin_symbiont";

    public const int SummonAmount = 10;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool SpawnsPets => true;

    protected override string IconBaseName => "toxic_egg";

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            return new IHoverTip[]
            {
                new HoverTip(new LocString("relics", Id.Entry + ".wriggling_title"), new LocString("relics", Id.Entry + ".wriggling_description")),
            };
        }
    }

    public override async Task BeforeCombatStart()
    {
        try
        {
            Flash();
            await WrigglerSummon.Summon(new ThrowingPlayerChoiceContext(), Owner, SummonAmount);
        }
        catch (Exception ex)
        {
            Log.Warn($"Vermin Symbiont failed to summon a Wriggler: {ex}");
        }
    }
}
