using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Run.Campfire;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Run;

/// <summary>
/// Hidden run modifier added to every run. It anchors zones to the run: the game saves its [SavedProperty] values
/// with the run and sends them to clients in loaded/rejoined multiplayer sessions. Derives from plain ModifierModel
/// (not BaseLib's CustomModifierModel) so it never appears in custom/daily modifier pickers. It also dispatches game
/// hooks to the zone effect handlers of the node the party is at.
/// </summary>
public sealed class ZoneTheSpireModifier : ModifierModel, ILocalizationProvider
{
    private string _zonesJson = string.Empty;

    /// <summary>All acts' zones as JSON (see Core/Persistence). Saved with the run.</summary>
    [SavedProperty]
    public string ZonesJson
    {
        get => _zonesJson;
        set
        {
            _zonesJson = value ?? string.Empty;
            ZonesJsonVersion++;
        }
    }

    private string _zoneRelicsAppeared = string.Empty;

    /// <summary>
    /// Zone relics that already appeared in a chest, as "key@location" pairs (see ZoneRelicRules). Saved with the run and
    /// updated identically on every peer when a chest is generated.
    /// </summary>
    [SavedProperty]
    public string ZoneRelicsAppeared
    {
        get => _zoneRelicsAppeared;
        set => _zoneRelicsAppeared = value ?? string.Empty;
    }

    private int _zoneEventChancePercent = ZoneEventRules.DefaultOfferChancePercent;

    /// <summary>
    /// Run-wide chance for the next event room inside a zone to become a zone event. It deliberately lives beside the
    /// saved zone data instead of on an act, so misses, resets, save/reload and multiplayer rejoin all persist across acts.
    /// </summary>
    [SavedProperty]
    public int ZoneEventChancePercent
    {
        get => _zoneEventChancePercent;
        set => _zoneEventChancePercent = ZoneEventRules.NormalizeOfferChance(value);
    }

    private string _shadowRevealed = string.Empty;

    /// <summary>
    /// Shadow Corruption nodes revealed on the map, as "act:row:col" keys (see ShadowRules). Saved with the run and updated
    /// identically on every peer when the party enters a room; a revealed node is never hidden again.
    /// </summary>
    [SavedProperty]
    public string ShadowRevealed
    {
        get => _shadowRevealed;
        set => _shadowRevealed = value ?? string.Empty;
    }

    private string _shadowStay = string.Empty;

    /// <summary>
    /// Shadow Corruption nodes the party has entered since it last came out of the zone, as "act:row:col" keys (the Dawn payout
    /// counts them). A set, so re-entering the same room after a reload never counts a node twice. Saved with the run and
    /// updated identically on every peer.
    /// </summary>
    [SavedProperty]
    public string ShadowStay
    {
        get => _shadowStay;
        set => _shadowStay = value ?? string.Empty;
    }

    private string _specialPotionSlots = string.Empty;

    /// <summary>
    /// The Fermentory's special potion slots of every player (FermentoryRules.Encode). Saved with the run and changed identically
    /// on every peer, only from synced hooks (see Fermentory.SpecialSlots).
    /// </summary>
    [SavedProperty]
    public string SpecialPotionSlots
    {
        get => _specialPotionSlots;
        set => _specialPotionSlots = value ?? string.Empty;
    }

    /// <summary>Incremented on every ZonesJson assignment; lets ZoneService cache parsed zones. Not saved.</summary>
    internal int ZonesJsonVersion { get; private set; }

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;

    /// <summary>Localized plain zone name (hover tip titles).</summary>
    internal static LocString ZoneTitle(BiomeDefinition biome) => ModTextLoc($"zone.{biome.Id}.name");

    /// <summary>Localized title of a zone glossary keyword (map node hover tips).</summary>
    internal static LocString KeywordTitle(ZoneKeyword keyword) => ModTextLoc(keyword.Key);

    /// <summary>A text of the mod's own table (Localization/<language>/zone_the_spire.json) as a game LocString.</summary>
    internal static LocString ModTextLoc(string key) => new(global::ZoneTheSpire.Run.Localization.ModLocalization.ModTable, key);

    /// <summary>A Zone the Spire string from this modifier's loc entries.</summary>
    internal static LocString ModLoc(string key) =>
        new("modifiers", $"{ModelDb.GetId<ZoneTheSpireModifier>().Entry}.{key}");

    public override Task AfterMapGenerated(ActMap map, int actIndex)
    {
        try
        {
            ZoneService.EnsureZones(RunState, map, actIndex);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to prepare zones for act {actIndex + 1}: {ex}");
        }

        return Task.CompletedTask;
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        ZoneOverride.OnRoomEntered(RunState, room);
        MirroredRestSiteHistory.RemapChoices(RunState);
        Shadow.ShadowPath.RevealAroundParty(RunState);
        await Shadow.ShadowDawn.OnRoomEntered(RunState, room);
        Rendering.ZoneScreenEffects.OnRoomEntered(RunState, room);
        if (room is not CombatRoom combatRoom || ActiveHandlers() is not { } active)
        {
            return;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                await handler.OnCombatRoomEntered(combatRoom, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed on combat room entered: {ex}");
            }
        }
    }

    public override async Task BeforeCombatStart()
    {
        await Fermentory.SlotEffects.FillStills(RunState);
        if (RunState.CurrentRoom is not CombatRoom combatRoom || ActiveHandlers() is not { } active)
        {
            return;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                await handler.OnBeforeCombatStart(combatRoom, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed before combat start: {ex}");
            }
        }
    }

    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (ActiveHandlers() is not { } active)
        {
            return;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                await handler.OnCreatureAddedToCombat(creature, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed on creature added: {ex}");
            }
        }
    }

    /// <summary>
    /// Worldly Attachment (any zone): a holder who was still living on comes out of the fight alive. It runs here rather than on
    /// victory because the game takes every power off the players between this hook and that one.
    /// </summary>
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        try
        {
            await Devas.WorldlyAttachmentLife.OnCombatEnd(room);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: combat end", ex);
        }
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (ActiveHandlers() is not { } active)
        {
            return;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                await handler.OnCombatVictory(room, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed after a combat victory: {ex}");
            }
        }
    }

    /// <summary>
    /// Shadow Corruption: a shaded card is revealed as soon as it is played, and a corrupted card's values are doubled before it
    /// resolves (in case they were rebuilt since its last preview). Cheap no-ops for every other card.
    /// </summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        try
        {
            Shadow.ShadedCards.Unshade(cardPlay.Card);
            Shadow.ShadowCorruption.EnsureDoubled(cardPlay.Card);
            Devas.Karma.BeforeCardPlayed(cardPlay);
            Hoarfrost.BitingCold.BeforeCardPlayed(cardPlay);
            Hallowed.Hallowing.BeforeCardPlayed(cardPlay);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: before a card is played", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>Hoarfrost: a card's Biting Cold ices every enemy the card's attacks damaged, so its hits are remembered here.</summary>
    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        try
        {
            Hoarfrost.BitingCold.AfterDamageGiven(target, props, cardSource);
            Hallowed.Hallowing.AfterDamageGiven(target, props, cardSource, result.TotalDamage);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: after damage", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>Blasphemer: a hand-marked Blasphemous card gives its owner Hallowed (never on top of the permanent modifier).</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            Devas.Karma.AfterCardPlayed(cardPlay);
            await Hallowed.BlasphemousMark.OnCardPlayed(choiceContext, cardPlay);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: after a card is played", ex);
        }
    }

    /// <summary>Karma (any zone): a card's cost with its Karma, so the cost in its corner shows it too.</summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        try
        {
            return Devas.Karma.TryModifyCost(card, originalCost, out modifiedCost);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: card cost", ex);
            modifiedCost = originalCost;
            return false;
        }
    }

    /// <summary>Karma (any zone): an X-cost card's X with its Karma bonus.</summary>
    public override int ModifyXValue(CardModel card, int originalValue)
    {
        try
        {
            return Devas.Karma.ModifyX(card, originalValue);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: X value", ex);
            return originalValue;
        }
    }

    /// <summary>Phantasmal Tombs: a Phantasm-Haunted card exhausts with a 30% chance when played.</summary>
    public override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources, CardLocation cardLocation)
    {
        try
        {
            return Phantasmal.PhantasmHauntedModifier.ModifyPlayResultLocation(card, cardLocation);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: played card destination", ex);
            return cardLocation;
        }
    }

    /// <summary>
    /// Worldly Attachment holds off its holder's death. It sits here, on the run modifier, because the game asks every power,
    /// relic and potion first and the modifiers last, so Lizard Tail, Fairy in a Bottle and the like still go before it.
    /// </summary>
    public override bool ShouldDieLate(Creature creature)
    {
        try
        {
            return !Devas.WorldlyAttachmentLife.ShouldHold(creature);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: death check", ex);
            return true;
        }
    }

    /// <summary>
    /// Deva's Blessing is a single extra action: an enemy revived after taking it dies again as soon as it has HP, whatever
    /// revived it. Decimillipede segments are left to their own mechanic.
    /// </summary>
    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        try
        {
            if (delta > 0m)
            {
                await Devas.DevasBlessing.KillIfRevivedAfterBlessing(creature);
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: HP change", ex);
        }
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        try
        {
            if (!creature.HasPower<Powers.WorldlyAttachmentPower>())
            {
                await Devas.WorldlyAttachmentLife.Hold(creature);
            }
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: after a death is prevented", ex);
        }
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (ActiveHandlers() is not { } active)
        {
            return;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                await handler.OnAfterPlayerTurnStart(player, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed after a player's turn start: {ex}");
            }
        }
    }

    public override async Task BeforeDeath(Creature creature)
    {
        if (ActiveHandlers() is not { } active)
        {
            return;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                await handler.OnBeforeDeath(creature, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed before a death: {ex}");
            }
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        // A blessed enemy that has now died has spent its blessing, whatever else brings it back later.
        if (!wasRemovalPrevented)
        {
            Devas.DevasBlessing.MarkSpentIfBlessed(creature);
        }

        if (ActiveHandlers() is not { } active)
        {
            return;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                await handler.OnAfterDeath(creature, wasRemovalPrevented, deathAnimLength, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed after a death: {ex}");
            }
        }
    }

    public override bool TryModifyCardRewardOptionsLate(Player player, List<CardCreationResult> cardRewardOptions, CardCreationOptions creationOptions)
    {
        // Shadow Corruption corrupts one card in every card reward inside the zone, whatever the node kind.
        bool modified = Shadow.ShadowCorruption.TryCorruptReward(player, cardRewardOptions, creationOptions);
        // Deva's Domain gives one Attack or Skill in every card reward inside the zone Chakra.
        modified |= Devas.ChakraRewards.TryAddToReward(player, cardRewardOptions, creationOptions);
        if (ActiveHandlers() is not { } active)
        {
            return modified;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                modified |= handler.TryModifyCardReward(player, cardRewardOptions, creationOptions, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed modifying card rewards: {ex}");
            }
        }

        return modified;
    }

    public override void ModifyMerchantCardCreationResults(Player player, List<CardCreationResult> cards)
    {
        if (ActiveHandlers() is not { } active)
        {
            return;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                handler.OnModifyMerchantCards(player, cards, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed modifying shop cards: {ex}");
            }
        }
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (ActiveHandlers() is not { } active)
        {
            return false;
        }

        bool modified = false;
        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                modified |= handler.TryModifyRestSiteOptions(player, options, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed modifying rest site options: {ex}");
            }
        }

        return modified;
    }

    /// <summary>Troubled Dreams (Shadow Corruption) is a free campfire action: choosing it leaves the other options available.</summary>
    public override bool ShouldDisableRemainingRestSiteOptions(Player player)
    {
        try
        {
            return !Campfire.TroubledDreamsRestSiteOption.ConsumeFreeAction(player);
        }
        catch (Exception ex)
        {
            Log.WarnOnce("Run modifier: campfire free action", ex);
            return true;
        }
    }

    public override bool ShouldAllowMerchantCardRemoval(Player player)
    {
        if (ActiveHandlers() is not { } active)
        {
            return true;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                if (!handler.AllowsMerchantCardRemoval(player, active.Context))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed checking card removal availability: {ex}");
            }
        }

        return true;
    }

    public override decimal ModifyMerchantPrice(Player player, MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry entry, decimal cost)
    {
        if (ActiveHandlers() is not { } active)
        {
            return cost;
        }

        foreach (ZoneEffectHandler handler in active.Handlers)
        {
            try
            {
                cost = handler.ModifyMerchantPrice(player, entry, cost, active.Context);
            }
            catch (Exception ex)
            {
                Log.Warn($"Zone effect '{handler.EffectId}' failed modifying a shop price: {ex}");
            }
        }

        return cost;
    }

    private (ZoneContext Context, IReadOnlyList<ZoneEffectHandler> Handlers)? ActiveHandlers()
    {
        try
        {
            ZoneContext? context = ZoneContext.Current(RunState);
            if (context == null)
            {
                return null;
            }

            return (context, ZoneEffectRegistry.HandlersFor(context));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to resolve zone effect handlers: {ex}");
            return null;
        }
    }
}
