using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.ForgottenEmpire;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Forgotten Empire event. Each player chooses on their own: bow (obtain Emperor's Will, which takes a Power from their deck
/// and plays it at the start of every combat, and add Normality), take the crown (obtain Calcified Crown), or break the
/// statue's nose (heal half of their Max HP). Every change goes through the game's own commands, so it is saved and replicated
/// normally.
/// </summary>
public sealed class ForgottenEmperor : ZoneEventModel
{
    private const string PortraitFile = "forgotten_emperor.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/forgotten_emperor.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new ForgottenEmpireBiome().Id };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new HealVar(0),
    };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    public override void CalculateVars()
    {
        DynamicVars.Heal.BaseValue = ForgottenEmpireRules.NoseHeal(Owner?.Creature.MaxHp ?? 0);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption bow = Owner!.Deck.Cards.Any(EmperorsWill.CanTake)
            ? Option(Bow, HoverTipFactory.FromRelic<EmperorsWill>().Concat(HoverTipFactory.FromCardWithCardHoverTips<Normality>()), "INITIAL")
            : LockedOption("BOW_LOCKED");

        return new[]
        {
            bow,
            Option(TakeTheCrown, HoverTipFactory.FromRelic<CalcifiedCrown>(), "INITIAL"),
            Option(BreakItsNose),
        };
    }

    private async Task Bow()
    {
        await RelicCmd.Obtain(ModelDb.Relic<EmperorsWill>().ToMutable(), Owner!);
        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Normality>() }, Owner!);
        SetEventFinished(PageDescription("BOWED"));
    }

    private async Task TakeTheCrown()
    {
        await RelicCmd.Obtain(ModelDb.Relic<CalcifiedCrown>().ToMutable(), Owner!);
        SetEventFinished(PageDescription("TOOK_THE_CROWN"));
    }

    private async Task BreakItsNose()
    {
        await CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.IntValue);
        SetEventFinished(PageDescription("BROKE_ITS_NOSE"));
    }
}
