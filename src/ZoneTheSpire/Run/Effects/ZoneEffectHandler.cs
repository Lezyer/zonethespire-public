using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Game behaviour for one BiomeEffect. Handlers run inside synced game hooks on every multiplayer peer: they must not use
/// RNG streams other than the game's synchronized ones, local settings, or UI state.
/// </summary>
public abstract class ZoneEffectHandler
{
    public abstract string EffectId { get; }

    public virtual Task OnCombatRoomEntered(CombatRoom room, ZoneContext context) => Task.CompletedTask;

    public virtual Task OnCreatureAddedToCombat(Creature creature, ZoneContext context) => Task.CompletedTask;

    /// <summary>
    /// Called when combat has started (CombatManager marks it in progress, then runs BeforeCombatStart). Unlike
    /// <see cref="OnCombatRoomEntered"/>, creatures may be added here: CreatureCmd.Add refuses to run before combat is in progress.
    /// </summary>
    public virtual Task OnBeforeCombatStart(CombatRoom room, ZoneContext context) => Task.CompletedTask;

    /// <summary>Called after a combat ends in a player victory (the room is still the current room).</summary>
    public virtual Task OnCombatVictory(CombatRoom room, ZoneContext context) => Task.CompletedTask;

    /// <summary>Called for each player after their start-of-turn draw (runs on every peer for every player).</summary>
    public virtual Task OnAfterPlayerTurnStart(Player player, ZoneContext context) => Task.CompletedTask;

    /// <summary>Called before any creature dies (also when the death is then prevented). Powers are still attached.</summary>
    public virtual Task OnBeforeDeath(Creature creature, ZoneContext context) => Task.CompletedTask;

    /// <summary>
    /// Called after any creature dies, before it is removed from combat and loses its powers, and before the win check.
    /// Creatures may be added here.
    /// </summary>
    public virtual Task OnAfterDeath(Creature creature, bool wasRemovalPrevented, float deathAnimLength, ZoneContext context) => Task.CompletedTask;

    public virtual bool TryModifyCardReward(Player player, List<CardCreationResult> options, CardCreationOptions creationOptions, ZoneContext context) => false;

    /// <summary>Called whenever a shop stocks or refreshes a card entry (may be called again for the same card).</summary>
    public virtual void OnModifyMerchantCards(Player player, List<CardCreationResult> cards, ZoneContext context)
    {
    }

    /// <summary>Called from the modifier's TryModifyRestSiteOptions for the player's rest site. Return true if modified.</summary>
    public virtual bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options, ZoneContext context) => false;

    /// <summary>Whether the shop's card removal slot may be used (checked when the slot is filled). All handlers must allow it.</summary>
    public virtual bool AllowsMerchantCardRemoval(Player player, ZoneContext context) => true;

    /// <summary>Adjusts a shop entry's price; handlers are applied in order.</summary>
    public virtual decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost, ZoneContext context) => cost;
}
