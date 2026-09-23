using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Fermentory;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Fermentory;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// The Fermentory event: a huge copper vat, fermenting something old and hungry. Each player chooses on their own (see
/// FermentingVatRules for the numbers):
/// <list type="bullet">
/// <item>Drink Deep: gain 10 Max HP; a random Curse (the game's own curse pool and synced RNG, like Cursed Run) joins the deck.</item>
/// <item>Feed the Vat: every potion is poured away; one random upgradable card per potion (seeded per node and player) is
/// upgraded, shown before and after. Locked without a potion or an upgradable card.</item>
/// </list>
/// Every change goes through the game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class TheFermentingVat : ZoneEventModel
{
    private const string PortraitFile = "fermenting_vat.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/fermenting_vat.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new FermentoryBiome().Id };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption drink = Option(DrinkDeep, System.Array.Empty<IHoverTip>(), "INITIAL");
        int potions = Owner!.Potions.Count();
        int upgradable = UpgradableCards().Count;
        EventOption feed = FermentingVatRules.CanFeed(potions, upgradable)
            ? FeedOption(FermentingVatRules.FeedUpgrades(potions, upgradable))
            : LockedOption(potions == 0 ? "FEED_THE_VAT_NO_POTIONS" : "FEED_THE_VAT_NO_CARDS");
        return new[] { drink, feed };
    }

    /// <summary>The Feed the Vat option, naming how many cards it really upgrades.</summary>
    private EventOption FeedOption(int count)
    {
        // Option loc keys are stored exactly as EventOptionLoc declares them (upper snake case), under the event's model id.
        string prefix = $"{ModelDb.GetId<TheFermentingVat>().Entry}.pages.INITIAL.options.FEED_THE_VAT";
        var description = new LocString("events", prefix + ".description");
        description.Add("Count", count);
        return Option(FeedTheVat, new LocString("events", prefix + ".title"), description, System.Array.Empty<IHoverTip>());
    }

    private List<CardModel> UpgradableCards() => Owner!.Deck.Cards.Where(card => card.IsUpgradable).ToList();

    private async Task DrinkDeep()
    {
        await CreatureCmd.GainMaxHp(Owner!.Creature, FermentingVatRules.DrinkMaxHp);

        IRunState runState = Owner.RunState;
        List<CardModel> curses = ModelDb.CardPool<CurseCardPool>()
            .GetUnlockedCards(Owner.UnlockState, runState.CardMultiplayerConstraint)
            .Where(card => card.CanBeGeneratedByModifiers)
            .ToList();
        string curseName = "none";
        if (curses.Count > 0)
        {
            CardModel curse = runState.CreateCard(runState.Rng.Niche.NextItem(curses)!, Owner);
            curseName = curse.Id.Entry;
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(curse, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
        }

        SetEventFinished(PageDescription("DRANK"));
    }

    private async Task FeedTheVat()
    {
        List<PotionModel> potions = Owner!.Potions.ToList();
        List<CardModel> upgradable = UpgradableCards();
        int count = FermentingVatRules.FeedUpgrades(potions.Count, upgradable.Count);
        foreach (PotionModel potion in potions)
        {
            await PotionCmd.Discard(potion);
        }

        IRunState runState = Owner.RunState;
        string location = SpecialSlots.CurrentLocation(runState);
        var changes = new List<CardChangePreview.Change>();
        foreach (int index in FermentingVatRules.PickUpgrades(runState.Rng.Seed, location, Owner.NetId, upgradable.Count, count))
        {
            CardModel card = upgradable[index];
            CardModel before = CardChangePreview.Snapshot(card);
            CardCmd.Upgrade(card, CardPreviewStyle.None);
            changes.Add(new CardChangePreview.Change(before, CardChangePreview.Snapshot(card)));
        }

        if (LocalContext.IsMe(Owner) && changes.Count > 0)
        {
            // Local display only: never awaited, so the synced event doesn't wait on a click.
            _ = CardChangePreview.Show(global::ZoneTheSpire.Run.Localization.ModLocalization.GameText("events", $"{ModelDb.GetId<TheFermentingVat>().Entry}.pages.INITIAL.options.FEED_THE_VAT.title"), changes);
        }

        SetEventFinished(PageDescription("FED"));
    }
}
