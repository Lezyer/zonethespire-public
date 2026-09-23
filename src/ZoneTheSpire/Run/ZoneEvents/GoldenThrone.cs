using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Midas;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.HallsOfMidas;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Halls of Midas event. Each player chooses on their own: pay 100 gold for a random relic (pulled from the front of their
/// synced relic bag, like vanilla shop events), take act-scaled gold with a Debt curse, or upgrade a card and make it Gilded.
/// Every change goes through the game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class GoldenThrone : ZoneEventModel
{
    internal static string SelectionPromptKey => ModelDb.GetId<GoldenThrone>().Entry + ".selectionPrompt";

    private const string PortraitFile = "golden_throne.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/golden_throne.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new HallsOfMidasBiome().Id };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new GoldVar(HallsOfMidasRules.PlunderGold(0)),
    };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    public override void CalculateVars()
    {
        DynamicVars.Gold.BaseValue = HallsOfMidasRules.PlunderGold(Owner?.RunState.CurrentActIndex ?? 0);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption tribute = HallsOfMidasRules.CanPayTribute(Owner!.Gold)
            ? Option(PayTribute)
            : LockedOption("PAY_TRIBUTE_LOCKED");
        EventOption grasp = Owner.Deck.Cards.Any(GildedModifier.CanGild)
            ? Option(GraspTheSceptre)
            : LockedOption("GRASP_THE_SCEPTRE_LOCKED");

        return new[]
        {
            tribute,
            Option(PocketTheCoins, HoverTipFactory.FromCardWithCardHoverTips<Debt>(), "INITIAL"),
            grasp,
        };
    }

    private async Task PayTribute()
    {
        await PlayerCmd.LoseGold(HallsOfMidasRules.TributeCost, Owner!, GoldLossType.Spent);
        RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner!).ToMutable();
        await RelicCmd.Obtain(relic, Owner!);
        SetEventFinished(PageDescription("TRIBUTE"));
    }

    private async Task PocketTheCoins()
    {
        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner!);
        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Debt>() }, Owner!);
        SetEventFinished(PageDescription("POCKETED"));
    }

    private async Task GraspTheSceptre()
    {
        var prefs = new CardSelectorPrefs(new LocString("events", SelectionPromptKey), 1)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };

        CardModel? card = (await CardSelectCmd.FromDeckGeneric(Owner!, prefs, GildedModifier.CanGild)).FirstOrDefault();
        if (card != null)
        {
            if (card.IsUpgradable)
            {
                CardCmd.Upgrade(card);
            }

            GildedModifier.TryAdd(card);
        }

        SetEventFinished(PageDescription("GRASPED"));
    }
}
