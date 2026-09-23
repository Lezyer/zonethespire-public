using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Hoarfrost;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Hoarfrost;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Hoarfrost event: you are stuck to the waist in packed snow. Each player chooses on their own: ditch their gear (lose the
/// relic the option names and half their gold, then bind Biting Cold to 3 chosen cards at Frostbind's rate), struggle through
/// (lose 15 HP, bind it to 1 chosen card at the card reward rate), or cut themselves out (add Injury, and nothing else). The
/// relic and the gold are both shown before the choice is made. The relic is picked from the run seed, node and player, so it
/// never changes while the screen is open and every peer loses the same one. Every change goes through the game's own commands,
/// so it is saved and replicated normally.
/// </summary>
public sealed class Frostfallen : ZoneEventModel
{
    private const string PortraitFile = "frostfallen.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/frostfallen.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new HoarfrostBiome().Id };

    /// <summary>The relic this event takes, worked out once so the option text and the choice can never disagree.</summary>
    private RelicModel? _doomed;

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    internal static string DitchPromptKey => ModelDb.GetId<Frostfallen>().Entry + ".ditchPrompt";

    internal static string StrugglePromptKey => ModelDb.GetId<Frostfallen>().Entry + ".strugglePrompt";

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption ditch = DoomedRelic() is { } doomed ? DitchOption(doomed) : LockedOption("DITCH_YOUR_GEAR_LOCKED");
        EventOption struggle = FrostfallenRules.CanStruggle(Owner!.Creature.CurrentHp)
            ? Option(StruggleThrough, ColdTips(), "INITIAL")
            : LockedOption("STRUGGLE_THROUGH_LOCKED");
        EventOption cut = Option(CutYourselfOut, HoverTipFactory.FromCardWithCardHoverTips<Injury>(), "INITIAL");

        return new[] { ditch, struggle, cut };
    }

    /// <summary>The Ditch Your Gear option, naming the relic and the gold it will really cost.</summary>
    private EventOption DitchOption(RelicModel doomed)
    {
        int gold = FrostfallenRules.GoldLoss(Owner!.Gold);
        // Option loc keys are stored exactly as EventOptionLoc declares them (upper snake case), under the event's model id.
        string key = gold > 0 ? "DITCH_YOUR_GEAR" : "DITCH_YOUR_GEAR_FREE";
        var description = new LocString("events", $"{ModelDb.GetId<Frostfallen>().Entry}.pages.INITIAL.options.{key}.description");
        description.Add("Relic", doomed.Title.GetFormattedText());
        description.Add("Gold", gold);
        var title = new LocString("events", $"{ModelDb.GetId<Frostfallen>().Entry}.pages.INITIAL.options.{key}.title");
        IHoverTip[] tips = ColdTips().Concat(HoverTipFactory.FromRelic(doomed)).ToArray();
        return Option(DitchYourGear, title, description, tips);
    }

    private IHoverTip[] ColdTips()
    {
        var tips = new List<IHoverTip>();
        CardModifier.Get<BitingColdModifier>().AddTips(tips);
        return tips.ToArray();
    }

    /// <summary>
    /// The relic the ice takes: one of those the game counts as tradable, so it is never a starter relic nor one that already
    /// spent its effect when you picked it up. Worked out once per event.
    /// </summary>
    private RelicModel? DoomedRelic()
    {
        if (_doomed != null)
        {
            return _doomed;
        }

        List<RelicModel> losable = Owner!.Relics.Where(relic => relic.IsTradable).ToList();
        IRunState runState = Owner.RunState;
        MapCoord? coord = runState.CurrentMapCoord;
        string location = MirrorDuplicateRules.LocationKey(runState.CurrentActIndex, coord?.row, coord?.col, 0);
        int index = FrostfallenRules.PickDoomedRelicIndex(runState.Rng.Seed, location, Owner.NetId, losable.Count);
        _doomed = index < 0 ? null : losable[index];
        return _doomed;
    }

    private async Task DitchYourGear()
    {
        if (DoomedRelic() is { } doomed)
        {
            await RelicCmd.Remove(doomed);
        }

        int gold = FrostfallenRules.GoldLoss(Owner!.Gold);
        if (gold > 0)
        {
            await PlayerCmd.LoseGold(gold, Owner, GoldLossType.Spent);
        }

        await BindFrost(FrostfallenRules.DitchCards, FrostfallenRules.DitchColdPerCost, new LocString("events", DitchPromptKey));
        SetEventFinished(PageDescription("DITCHED"));
    }

    private async Task StruggleThrough()
    {
        await CreatureCmd.Damage(
            new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            FrostfallenRules.StruggleHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

        await BindFrost(FrostfallenRules.StruggleCards, FrostfallenRules.StruggleColdPerCost, new LocString("events", StrugglePromptKey));
        SetEventFinished(PageDescription("STRUGGLED"));
    }

    private async Task CutYourselfOut()
    {
        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Injury>() }, Owner!);
        SetEventFinished(PageDescription("CUT"));
    }

    /// <summary>Binds Biting Cold to cards of the player's choice, at <paramref name="perCost"/> per energy each costs.</summary>
    private async Task BindFrost(int wanted, int perCost, LocString prompt)
    {
        int count = FrostfallenRules.BindCountFor(Owner!.Deck.Cards.Count(BitingColdModifier.CanIce), wanted);
        if (count <= 0)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(prompt, count, count)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };
        List<CardModel> chosen = (await CardSelectCmd.FromDeckGeneric(Owner, prefs, BitingColdModifier.CanIce)).ToList();
        foreach (CardModel card in chosen)
        {
            BitingColdModifier.AddOrIncrease(card, FrostfallenRules.BoundCold(perCost, card.EnergyCost.GetResolved(), card.EnergyCost.CostsX));
        }
    }
}
