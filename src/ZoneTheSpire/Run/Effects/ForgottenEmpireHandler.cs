using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.ForgottenEmpire;
using ZoneTheSpire.Run.ForgottenEmpire;
using ZoneTheSpire.Run.Powers;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Forgotten Empire (Monster, Elite, "?" fights): every enemy (including enemies added during the fight) is a Forgotten Statue
/// with 80% of its Max HP, Marbled equal to 30% of its original Max HP and Polishing (10 per player). Card rewards that gain Block are Marbled; other playable ones get Marbling. All
/// inside synced hooks.
/// </summary>
internal sealed class ForgottenEmpireHandler : ZoneEffectHandler
{
    public override string EffectId => ForgottenEmpireBiome.StatuesEffectId;

    public override async Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Creature enemy in room.CombatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
        {
            await Petrify(enemy);
        }
    }

    /// <summary>Enemies summoned or spawned mid-fight. The fight's starting enemies are petrified in OnBeforeCombatStart.</summary>
    public override async Task OnCreatureAddedToCombat(Creature creature, ZoneContext context)
    {
        if (CombatManager.Instance.IsInProgress)
        {
            await Petrify(creature);
        }
    }

    public override bool TryModifyCardReward(Player player, List<CardCreationResult> options, CardCreationOptions creationOptions, ZoneContext context)
    {
        if (creationOptions.Source != CardCreationSource.Encounter
            || creationOptions.Flags.HasFlag(CardCreationFlags.NoCardModelModifications))
        {
            return false;
        }

        bool modified = false;
        foreach (CardModel card in options.Select(option => option.Card))
        {
            // CardReward.Populate can re-invoke this hook for pre-set rewards: both TryAdds skip cards that already have either.
            modified |= MarbledModifier.TryAdd(card) || MarblingModifier.TryAdd(card);
        }

        return modified;
    }

    private static async Task Petrify(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || !creature.IsMonster || !creature.IsAlive || creature.CombatState == null
            || creature.HasPower<ForgottenStatuePower>())
        {
            return;
        }

        int originalMaxHp = creature.MaxHp;
        await PowerCmd.Apply<ForgottenStatuePower>(new ThrowingPlayerChoiceContext(), creature, 1m, null, null);
        await CreatureCmd.SetMaxAndCurrentHp(creature, ForgottenEmpireRules.StatueMaxHp(originalMaxHp));
        await MarbledPower.Add(creature, ForgottenEmpireRules.StartingMarbled(originalMaxHp), null);
        await PowerCmd.Apply<PolishingPower>(new ThrowingPlayerChoiceContext(), creature, ForgottenEmpireRules.PolishingAmount(creature.CombatState.Players.Count), null, null);
    }
}

/// <summary>Forgotten Empire shops: every card for sale that gains Block is Marbled (shop stock is identical on every peer).</summary>
internal sealed class ForgottenEmpireShopHandler : ZoneEffectHandler
{
    public override string EffectId => ForgottenEmpireBiome.MarbledShopEffectId;

    public override void OnModifyMerchantCards(Player player, List<CardCreationResult> cards, ZoneContext context)
    {
        foreach (CardCreationResult result in cards)
        {
            MarbledModifier.TryAdd(result.Card);
        }
    }
}

/// <summary>
/// Forgotten Empire rest sites: an extra Sculpt option after the existing ones. Appended (never inserted) so the vanilla option
/// indices synced by OptionIndexChosenMessage stay the same on every peer.
/// </summary>
internal sealed class ForgottenEmpireCampfireHandler : ZoneEffectHandler
{
    public override string EffectId => ForgottenEmpireBiome.SculptEffectId;

    public override bool TryModifyRestSiteOptions(Player player, ICollection<MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption> options, ZoneContext context)
    {
        options.Add(new Campfire.SculptRestSiteOption(player));
        return true;
    }
}
