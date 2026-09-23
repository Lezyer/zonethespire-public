using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Wriggling;

namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// Infestation (Monster, Elite, "?" fights): every player starts the fight with 2 Infection in hand (added after the
/// first-turn draw, so the normal draw is unchanged), and when an enemy that isn't a minion dies, a Wriggler crawls out in
/// its place. The Phrog Parasite still releases only its own Wrigglers, but each of those releases one more.
/// </summary>
internal sealed class InfestationHandler : ZoneEffectHandler
{
    private const int StartingInfections = 2;

    public override string EffectId => InfestationBiome.WrigglersEffectId;

    /// <summary>Flies buzz around every enemy (visual only; Wrigglers excluded).</summary>
    public override Task OnBeforeCombatStart(CombatRoom room, ZoneContext context)
    {
        foreach (Creature enemy in room.CombatState.Enemies.Where(enemy => enemy.IsAlive).ToList())
        {
            FlySwarmVisual.Attach(enemy);
        }

        return Task.CompletedTask;
    }

    public override Task OnCreatureAddedToCombat(Creature creature, ZoneContext context)
    {
        if (CombatManager.Instance.IsInProgress)
        {
            FlySwarmVisual.Attach(creature);
        }

        return Task.CompletedTask;
    }

    public override async Task OnAfterPlayerTurnStart(Player player, ZoneContext context)
    {
        if (player.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        await CardPileCmd.AddToCombatAndPreview<Infection>(player.Creature, PileType.Hand, StartingInfections, null);
    }

    /// <summary>
    /// Every playable card in an Infestation fight's card rewards gets Wriggling 4 x its cost (0-cost and X-cost count as 1).
    /// Rewards are generated identically on every peer, so the modifiers match everywhere.
    /// </summary>
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
            // CardReward.Populate can re-invoke this hook for pre-set rewards: TryAdd never adds Wriggling twice.
            modified |= WrigglingModifier.TryAdd(card, WrigglingRules.RewardAmount(card.EnergyCost.GetResolved(), card.EnergyCost.CostsX));
        }

        return modified;
    }

    public override Task OnBeforeDeath(Creature creature, ZoneContext context)
    {
        InfestationWrigglers.RememberDeathPosition(creature);
        return Task.CompletedTask;
    }

    public override async Task OnAfterDeath(Creature creature, bool wasRemovalPrevented, float deathAnimLength, ZoneContext context)
    {
        if (wasRemovalPrevented || !InfestationWrigglers.ShouldSpawnFor(creature))
        {
            return;
        }

        await InfestationWrigglers.SpawnFor(creature);
    }
}
