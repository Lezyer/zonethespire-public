using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ZoneTheSpire.Run.Hoarfrost;

namespace ZoneTheSpire.Patches;

/// <summary>
/// Hoarfrost: the turn's frozen cards are picked just before the turn-start hand draw (among the hand and the cards about to be
/// drawn), so they are already iced on their way in. Runs in the synced turn-start flow on every peer.
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Draw), typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool))]
internal static class HoarfrostDrawPatch
{
    private static void Prefix(decimal count, Player player, bool fromHandDraw)
    {
        try
        {
            if (fromHandDraw)
            {
                FrozenCards.FreezeBeforeHandDraw(player, (int)decimal.Floor(count));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to freeze cards before the hand draw: {ex.Message}");
        }
    }
}
