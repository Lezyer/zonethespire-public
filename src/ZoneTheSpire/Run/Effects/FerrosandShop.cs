using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Run.Ferrosand;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Ferrosand shops: right after each player's shop inventory is created (MerchantRoom.Enter, on every peer), 3 of the stocked
/// cards for sale become Magnetic, chosen by the run seed, shop location and player. Cards restocked later stay normal.
/// </summary>
internal static class FerrosandShop
{
    public static void Magnetize(MerchantInventory inventory)
    {
        Player player = inventory.Player;
        IRunState runState = player.RunState;
        if (!ZoneEffectQuery.IsActive(runState, FerrosandBiome.MagneticShopEffectId))
        {
            return;
        }

        List<CardModel> stocked = inventory.CardEntries
            .Select(entry => entry.CreationResult?.Card)
            .OfType<CardModel>()
            .ToList();
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
        foreach (int slot in FerrosandRules.PickShopMagneticSlots(runState.Rng.Seed, location, player.NetId, stocked.Count))
        {
            MagneticModifier.TryAdd(stocked[slot]);
        }
    }
}

/// <summary>Ferrosand shop effect id (the work happens in <see cref="FerrosandShop"/> when the inventory is created).</summary>
internal sealed class FerrosandShopHandler : ZoneEffectHandler
{
    public override string EffectId => FerrosandBiome.MagneticShopEffectId;
}
