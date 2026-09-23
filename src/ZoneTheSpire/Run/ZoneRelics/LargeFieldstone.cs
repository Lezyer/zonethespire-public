using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Run.Ferrosand;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Ferrosand zone relic: on pickup, its owner may make up to 3 eligible cards Magnetic. While held, that player's Magnetic
/// cards can pull 6 cards per turn instead of 3. The pickup selection uses the game's synchronized deck selector, so every
/// multiplayer peer applies the same permanent card modifiers. An Event relic, so it only comes from Ferrosand chests.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class LargeFieldstone : ModArtRelicModel
{
    protected override string TextureName => "large_fieldstone";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasUponPickupEffect => true;

    protected override string IconBaseName => "oddly_smooth_stone";

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var tips = new List<IHoverTip>();
            CardModifier.Get<MagneticModifier>().AddTips(tips);
            return tips;
        }
    }

    public override async Task AfterObtained()
    {
        try
        {
            int eligible = Owner.Deck.Cards.Count(MagneticModifier.CanMagnetize);
            int count = FerrosandRules.LargeFieldstoneSelectCount(eligible);
            if (count == 0)
            {
                return;
            }

            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 0, count)
            {
                Cancelable = true,
                RequireManualConfirmation = true,
            };
            IReadOnlyList<CardModel> selected = (await CardSelectCmd.FromDeckGeneric(Owner, prefs, MagneticModifier.CanMagnetize)).ToList();
            foreach (CardModel card in selected)
            {
                MagneticModifier.TryAdd(card);
            }

            if (selected.Count > 0)
            {
                Flash();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Large Fieldstone failed to magnetize cards: {ex}");
        }
    }
}
