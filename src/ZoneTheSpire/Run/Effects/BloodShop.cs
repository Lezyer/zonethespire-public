using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.BloodRain;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Blood Rain shops: prices stay computed in gold (so discounts, sales and card removal scaling all apply as usual), then
/// are shown and paid in HP at 1 HP per 15 gold. Every shop payment ends in PlayerCmd.LoseGold, on the buyer's machine and
/// (through the game's GoldLostMessage / MerchantCardRemovalMessage) on every other peer, so converting it there keeps
/// multiplayer in sync. The Blood Ledger relic uses the same path in any shop, for prices its holder can't afford in gold.
/// </summary>
internal static class BloodShop
{
    private static readonly FieldInfo? EntryPlayerField = AccessTools.Field(typeof(MerchantEntry), "_player");

    public static bool IsActive(Player? player) =>
        player != null
        && player.RunState.CurrentRoom is MerchantRoom
        && ZoneEffectQuery.IsActive(player.RunState, BloodRainBiome.BloodShopEffectId);

    public static Player? PlayerOf(MerchantEntry entry) => EntryPlayerField?.GetValue(entry) as Player;

    public static int HpCostOf(MerchantEntry entry) => BloodRainRules.HpCost(entry.Cost);

    /// <summary>
    /// Blood Ledger: in any shop, a price its holder can't afford in gold is paid in HP instead (if they would survive it).
    /// </summary>
    public static bool LedgerCovers(Player? player, int price) =>
        player != null
        && player.RunState.CurrentRoom is MerchantRoom
        && BloodLedger.IsHeldBy(player)
        && TitheRules.LedgerPaysWithHp(player.Gold, price, player.Creature.CurrentHp);

    /// <summary>Whether this shop price is shown and paid in HP: in Blood Rain shops, or when the Blood Ledger covers it.</summary>
    public static bool PaysInHp(MerchantEntry entry)
    {
        Player? player = PlayerOf(entry);
        return IsActive(player) || LedgerCovers(player, entry.Cost);
    }

    /// <summary>
    /// Replaces MerchantEntry.EnoughGold at every shop call site: HP in Blood Rain shops, gold everywhere else, and HP for what
    /// a Blood Ledger holder can't afford in gold.
    /// </summary>
    public static bool EnoughCurrency(MerchantEntry entry)
    {
        try
        {
            Player? player = PlayerOf(entry);
            if (IsActive(player))
            {
                return BloodRainRules.CanAffordHp(player!.Creature.CurrentHp, HpCostOf(entry));
            }

            if (!entry.EnoughGold && LedgerCovers(player, entry.Cost))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Blood Rain shop affordability check failed; using gold: {ex}");
        }

        return entry.EnoughGold;
    }

    /// <summary>Pays a shop price of <paramref name="goldAmount"/> gold as HP loss (unblockable, like an event's HP cost).</summary>
    public static async Task PayWithHp(Player player, decimal goldAmount)
    {
        int hpCost = BloodRainRules.HpCost((int)goldAmount);
        if (hpCost <= 0)
        {
            return;
        }

        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, hpCost, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
    }
}
