using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.ZoneEvents;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.HallsOfMidas;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Halls of Midas and Blood Rain event: a collector who takes gold or blood. Each player chooses on their own: pay half their
/// gold for 1 Max HP per 10 gold paid (the option shows the exact amounts), lose 12 HP to make 2 chosen cards Gilded (the
/// option shows the Gilded tip), lose 5 Max HP for the Blood Ledger relic, or rob the collector (50%: 150 gold; otherwise lose
/// 10 HP and add Debt). The roll uses the event's own RNG, so every peer gets the same result. Every change goes through the
/// game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class TheTitheCollector : ZoneEventModel
{
    private const string PortraitFile = "the_tithe_collector.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/the_tithe_collector.png";

    private static readonly IReadOnlyList<string> Zones = new[]
    {
        new HallsOfMidasBiome().Id,
        new BloodRainBiome().Id,
    };

    internal static string GildPromptKey => ModelDb.GetId<TheTitheCollector>().Entry + ".gildPrompt";

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new GoldVar(0),
        new MaxHpVar(0),
    };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    /// <summary>Tithe in Gold shows the exact gold it takes and Max HP it gives for this player's current gold.</summary>
    public override void CalculateVars()
    {
        int paid = TitheRules.GoldTithe(Owner?.Gold ?? 0);
        DynamicVars.Gold.BaseValue = paid;
        DynamicVars.MaxHp.BaseValue = TitheRules.MaxHpForGold(paid);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption gold = TitheRules.CanTitheGold(Owner!.Gold)
            ? Option(TitheInGold)
            : LockedOption("TITHE_IN_GOLD_LOCKED");
        var gildedTips = new List<IHoverTip>();
        CardModifier.Get<GildedModifier>().AddTips(gildedTips);
        EventOption blood = TitheRules.CanTitheBlood(Owner.Creature.CurrentHp) && Owner.Deck.Cards.Any(GildedModifier.CanGild)
            ? Option(TitheInBlood, gildedTips, "INITIAL")
            : LockedOption("TITHE_IN_BLOOD_LOCKED");
        EventOption ledger = TitheRules.CanSignLedger(Owner.Creature.MaxHp)
            ? Option(SignTheLedger, HoverTipFactory.FromRelic<BloodLedger>(), "INITIAL")
            : LockedOption("SIGN_THE_LEDGER_LOCKED");
        EventOption rob = TitheRules.CanRob(Owner.Creature.CurrentHp)
            ? Option(RobTheCollector, HoverTipFactory.FromCardWithCardHoverTips<Debt>(), "INITIAL")
            : LockedOption("ROB_THE_COLLECTOR_LOCKED");

        return new[] { gold, blood, ledger, rob };
    }

    private async Task TitheInGold()
    {
        int paid = TitheRules.GoldTithe(Owner!.Gold);
        int maxHp = TitheRules.MaxHpForGold(paid);
        await PlayerCmd.LoseGold(paid, Owner, GoldLossType.Spent);
        if (maxHp > 0)
        {
            await CreatureCmd.GainMaxHp(Owner.Creature, maxHp);
        }

        SetEventFinished(PageDescription("PAID_GOLD"));
    }

    private async Task TitheInBlood()
    {
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            TitheRules.BloodTitheHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

        int count = TitheRules.GildCountFor(Owner.Deck.Cards.Count(GildedModifier.CanGild));
        if (count > 0)
        {
            var prefs = new CardSelectorPrefs(new LocString("events", GildPromptKey), count, count)
            {
                Cancelable = false,
                RequireManualConfirmation = true,
            };
            List<CardModel> cards = (await CardSelectCmd.FromDeckGeneric(Owner, prefs, GildedModifier.CanGild)).ToList();
            foreach (CardModel card in cards)
            {
                GildedModifier.TryAdd(card);
            }
        }

        SetEventFinished(PageDescription("PAID_BLOOD"));
    }

    private async Task SignTheLedger()
    {
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature, TitheRules.LedgerMaxHpLoss, isFromCard: false);
        await RelicCmd.Obtain(ModelDb.Relic<BloodLedger>().ToMutable(), Owner!);
        SetEventFinished(PageDescription("SIGNED"));
    }

    private async Task RobTheCollector()
    {
        bool success = TitheRules.RobSucceeds(Rng.NextInt(100));
        if (success)
        {
            await PlayerCmd.GainGold(TitheRules.RobGold, Owner!);
        }
        else
        {
            await CreatureCmd.Damage(
                new ThrowingPlayerChoiceContext(),
                Owner!.Creature,
                TitheRules.RobHpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                null,
                null);
            await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Debt>() }, Owner!);
        }

        SetEventFinished(PageDescription(success ? "ROBBED" : "CAUGHT"));
    }
}
