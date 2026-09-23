using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Mirror;

namespace ZoneTheSpire.Run.Effects;

/// <summary>Mirrorlands card duplication. Runs inside synced flows on every peer.</summary>
internal static class MirrorDuplication
{
    /// <summary>Adds an exact copy (upgrade and enchantment kept) of <paramref name="original"/> to the owner's deck.</summary>
    public static async Task<CardModel> AddCopyToDeck(CardModel original, Player owner)
    {
        var copy = (CardModel)original.ClonePreservingMutability();
        await AddToDeck(copy, owner);
        return copy;
    }

    /// <summary>
    /// Unregistered exact copies of the up to 3 random cards the chooser will be offered from the source player's deck.
    /// Call it when the rest site options are created: RestSiteSynchronizer.BeginRestSite builds every player's options on
    /// every peer from the same state, whereas decks can change concurrently (and arrive in a different order on each
    /// peer) once players start choosing. The offer comes from the run seed, map location and both player ids.
    /// </summary>
    public static IReadOnlyList<CardModel> SnapshotOffer(Player chooser, Player source)
    {
        try
        {
            IRunState runState = chooser.RunState;
            List<CardModel> deck = source.Deck.Cards.ToList();
            MapCoord? coord = runState.CurrentMapCoord;
            string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, runState.CurrentRoom?.Id ?? 0);
            return MirrorDuplicateRules.PickOffer(runState.Rng.Seed, location, chooser.NetId, source.NetId, deck.Count)
                .Select(index => (CardModel)deck[index].ClonePreservingMutability())
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to prepare the Mirrorlands duplicate choice: {ex}");
            return Array.Empty<CardModel>();
        }
    }

    /// <summary>
    /// The chooser must pick 1 of the snapshot copies (no skipping; synced by FromChooseACardScreen); only the picked copy
    /// is registered and added to the chooser's deck. An empty snapshot offers nothing.
    /// </summary>
    public static async Task OfferSnapshot(Player chooser, IReadOnlyList<CardModel> offered)
    {
        if (offered.Count == 0)
        {
            return;
        }

        CardModel? picked = await CardSelectCmd.FromChooseACardScreen(new BlockingPlayerChoiceContext(), offered, chooser, canSkip: false);
        if (picked != null)
        {
            await AddToDeck(picked, chooser);
        }
    }

    private static async Task AddToDeck(CardModel copy, Player owner)
    {
        // ClonePreservingMutability keeps the original's owner, and CardModel.Owner refuses to replace an existing owner
        // ("already has an owner"). Clear it so AddCard registers the copy for its new owner.
        copy.Owner = null!;
        owner.RunState.AddCard(copy, owner);
        CardPileAddResult result = await CardPileCmd.Add(copy, PileType.Deck);
        CardCmd.PreviewCardPileAdd(result, 1.2f, CardPreviewStyle.HorizontalLayout);
    }
}
