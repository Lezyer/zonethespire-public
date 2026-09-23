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
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Infestation;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.ForgottenEmpire;
using ZoneTheSpire.Run.Wriggling;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Infestation/Forgotten Empire event. Each player's choice is resolved by the normal synchronized event and card-selection
/// commands; all lasting changes live on the run-state card/creature models and are therefore saved and replicated normally.
/// </summary>
public sealed class MarbleWorm : ZoneEventModel
{
    internal static string SelectionPromptKey => ModelDb.GetId<MarbleWorm>().Entry + ".selectionPrompt";

    private const int MaxHpLoss = 5;
    private const int HealAmount = 15;
    private const string PortraitFile = "marble_worm.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/marble_worm.png";

    private static readonly IReadOnlyList<string> Zones = new[]
    {
        new InfestationBiome().Id,
        new ForgottenEmpireBiome().Id,
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new MaxHpVar(MaxHpLoss),
        new HealVar(HealAmount),
    };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool canEat = Owner!.Deck.Cards.Any(MarbleCards.GainsBlock);
        EventOption eat = canEat
            ? Option(EatTheWorm)
            : LockedOption("EAT_THE_WORM_LOCKED");

        return new[]
        {
            eat,
            Option(CrushIt, HoverTipFactory.FromCardWithCardHoverTips<Guilty>(), "INITIAL"),
        };
    }

    private async Task EatTheWorm()
    {
        var prefs = new CardSelectorPrefs(
            new LocString("events", SelectionPromptKey),
            1)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };

        CardModel? card = (await CardSelectCmd.FromDeckGeneric(Owner!, prefs, MarbleCards.GainsBlock)).FirstOrDefault();
        if (card != null)
        {
            MarbledModifier.TryAdd(card);
            int wriggling = WrigglingRules.RewardAmount(card.EnergyCost.GetResolved(), card.EnergyCost.CostsX);
            WrigglingModifier.AddOrIncrease(card, wriggling);
        }

        await CreatureCmd.LoseMaxHp(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            DynamicVars.MaxHp.BaseValue,
            isFromCard: false);
        SetEventFinished(PageDescription("EATEN"));
    }

    private async Task CrushIt()
    {
        await CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.IntValue);
        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Guilty>() }, Owner!);
        SetEventFinished(PageDescription("CRUSHED"));
    }
}
