using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Shadow;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// The Lantern Bearer event relic. Upon pickup it adds 5 Lantern Light to its holder's deck (normal deck cards from then on).
/// Its holder carries the Shadow Corruption curse into every fight: they can't see enemy intents (ShadowSight, local to the
/// holder) and one card in their hand is Shaded each turn, on top of the zone's own in Shadow Corruption fights (ShadedCards).
/// The holder is wreathed in a faint shadow aura in combat. All gameplay runs in synced hooks on every peer. An Event relic, so
/// it only comes from the event.
/// </summary>
[Pool(typeof(EventRelicPool))]
public sealed class LastLightLantern : ModArtRelicModel
{
    protected override string TextureName => "last_light_lantern";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasUponPickupEffect => true;

    protected override string IconBaseName => "lantern";

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromCard<LanternLight>() };

    /// <summary>Whether <paramref name="player"/> holds the Lantern.</summary>
    internal static bool IsHeldBy(Player? player) => player?.GetRelic<LastLightLantern>() != null;

    /// <summary>Whether the local player holds the Lantern in this fight (their enemy intents are hidden).</summary>
    internal static bool IsHeldLocally(ICombatState combat) => IsHeldBy(LocalContext.GetMe(combat));

    public override async Task AfterObtained()
    {
        try
        {
            var results = new List<CardPileAddResult>();
            for (int i = 0; i < ShadowRules.LanternLightCopies; i++)
            {
                CardModel card = Owner.RunState.CreateCard<LanternLight>(Owner);
                results.Add(await CardPileCmd.Add(card, PileType.Deck));
            }

            CardCmd.PreviewCardPileAdd(results, 2f);
        }
        catch (Exception ex)
        {
            Log.Warn($"Last-Light Lantern failed to add Lantern Light to player {Owner.NetId}'s deck: {ex}");
        }
    }

    public override Task BeforeCombatStart()
    {
        ShadowAura.Attach(Owner.Creature, ShadowAura.Strength.Mild);
        return Task.CompletedTask;
    }

    /// <summary>Fallback when fewer cards than wanted could be shaded before the hand draw (ShadowHandDrawPatch).</summary>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            ShadedCards.ShadeInHand(player);
            if (LocalContext.IsMe(player) && player.Creature.CombatState is { } combat)
            {
                ShadowSight.RefreshLocalHealthBars(combat);
            }
        }

        return Task.CompletedTask;
    }
}
