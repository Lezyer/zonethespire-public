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
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Shadow Corruption zone relic: on pickup, its owner makes one card in their deck Shadow Corrupted (every value doubled, Doom
/// when played). The pickup selection uses the game's synchronized deck selector, so every multiplayer peer applies the same
/// permanent card modifier. Nothing happens if no card can be corrupted. An Event relic, so it only comes from Shadow
/// Corruption chests. Icons: original seed_of_hatred*.png relic art.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class SeedOfHatred : ModArtRelicModel
{
    protected override string TextureName => "seed_of_hatred";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasUponPickupEffect => true;

    protected override string IconBaseName => "ghost_seed";

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var tips = new List<IHoverTip>();
            CardModifier.Get<ShadowCorruptedModifier>().AddTips(tips);
            return tips;
        }
    }

    public override async Task AfterObtained()
    {
        try
        {
            if (!Owner.Deck.Cards.Any(ShadowCorruption.CanCorrupt))
            {
                return;
            }

            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1, 1)
            {
                RequireManualConfirmation = true,
            };
            CardModel? card = (await CardSelectCmd.FromDeckGeneric(Owner, prefs, ShadowCorruption.CanCorrupt)).FirstOrDefault();
            if (card != null && ShadowCorruptedModifier.TryAdd(card))
            {
                Flash();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Seed of Hatred failed to corrupt a card: {ex}");
        }
    }
}
