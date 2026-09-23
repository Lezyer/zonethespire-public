using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// Scrapyard zone relic. CardPileCmd.RemoveFromDeck invokes BeforeCardRemoved for event, shop and other ordinary removals.
/// The removed card is still in the deck during this hook, so it is explicitly excluded. Transforms bypass the removal hook
/// and therefore do not count. The owner's saved Transformations RNG makes the upgrade choice identical on every peer.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class SalvageMachine : ModArtRelicModel
{
    protected override string TextureName => "salvage_machine";

    protected override string IconBaseName => "war_paint";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override Task BeforeCardRemoved(CardModel card)
    {
        try
        {
            if (card.Owner != Owner)
            {
                return Task.CompletedTask;
            }

            List<CardModel> eligible = Owner.Deck.Cards
                .Where(candidate => candidate != card && candidate.IsUpgradable)
                .ToList();
            if (eligible.Count == 0)
            {
                return Task.CompletedTask;
            }

            CardModel upgrade = Owner.PlayerRng.Transformations.NextItem(eligible)!;
            CardCmd.Upgrade(upgrade, CardPreviewStyle.HorizontalLayout);
            Flash();
        }
        catch (Exception ex)
        {
            Log.Warn($"Salvage Machine failed to upgrade a card: {ex}");
        }

        return Task.CompletedTask;
    }
}
