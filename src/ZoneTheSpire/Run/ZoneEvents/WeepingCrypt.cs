using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Phantasmal;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Phantasmal;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Phantasmal Tombs event. Each player chooses on their own: trade 30% Max HP for 2 Apparitions, 15 HP for 2 Ghost in a Jar
/// potions (offered as rewards, so full potion slots work as usual), or remove a card and have a random other card
/// Phantasm-Haunted. The haunted card is picked with the event's own RNG (seeded per player from the run seed, like vanilla
/// events), so every peer haunts the same card. Every change goes through the game's own commands.
/// </summary>
public sealed class WeepingCrypt : ZoneEventModel
{
    private const string PortraitFile = "weeping_crypt.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/weeping_crypt.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new PhantasmalTombsBiome().Id };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new MaxHpVar(0),
    };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    public override void CalculateVars()
    {
        DynamicVars.MaxHp.BaseValue = PhantasmalRules.ProcessionMaxHpLoss(Owner?.Creature.MaxHp ?? 0);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption bottle = PhantasmalRules.CanBottleSpirits(Owner!.Creature.CurrentHp)
            ? Option(BottleTheSpirits, "INITIAL", HoverTipFactory.FromPotion<GhostInAJar>())
            : LockedOption("BOTTLE_THE_SPIRITS_LOCKED");
        EventOption layToRest = Owner.Deck.Cards.Any(card => card.IsRemovable)
            ? Option(LayAMemoryToRest)
            : LockedOption("LAY_A_MEMORY_TO_REST_LOCKED");

        return new[]
        {
            Option(JoinTheProcession, HoverTipFactory.FromCardWithCardHoverTips<Apparition>(), "INITIAL"),
            bottle,
            layToRest,
        };
    }

    private async Task JoinTheProcession()
    {
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature, DynamicVars.MaxHp.BaseValue, isFromCard: false);
        var results = new List<CardPileAddResult>();
        for (int i = 0; i < PhantasmalRules.ProcessionApparitions; i++)
        {
            CardModel card = Owner!.RunState.CreateCard<Apparition>(Owner);
            results.Add(await CardPileCmd.Add(card, PileType.Deck));
        }

        CardCmd.PreviewCardPileAdd(results, 2f);
        SetEventFinished(PageDescription("JOINED"));
    }

    private async Task BottleTheSpirits()
    {
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            PhantasmalRules.BottleHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
        var rewards = new List<Reward>();
        for (int i = 0; i < PhantasmalRules.BottledGhosts; i++)
        {
            rewards.Add(new PotionReward(ModelDb.Potion<GhostInAJar>().ToMutable(), Owner!));
        }

        await RewardsCmd.OfferCustom(Owner!, rewards);
        SetEventFinished(PageDescription("BOTTLED"));
    }

    private async Task LayAMemoryToRest()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1);
        List<CardModel> removed = (await CardSelectCmd.FromDeckForRemoval(Owner!, prefs)).ToList();
        await CardPileCmd.RemoveFromDeck(removed);

        List<CardModel> hauntable = Owner!.Deck.Cards.Where(PhantasmHauntedModifier.CanHaunt).ToList();
        if (hauntable.Count > 0)
        {
            CardModel haunted = hauntable[Rng.NextInt(hauntable.Count)];
            PhantasmHauntedModifier.TryAdd(haunted);
            CardCmd.Preview(haunted);
        }

        SetEventFinished(PageDescription("LAID_TO_REST"));
    }
}
