using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.ValueProps;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Ferrosand;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Ferrosand;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Ferrosand event. Each player chooses on their own: magnetize up to 3 cards for 10 HP, upgrade up to 2 chosen Magnetic
/// cards for 4 Max HP, or take 40 gold and a random potion (offered as a reward, so full potion slots work as usual). Every
/// change goes through the game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class SingingLodestone : ZoneEventModel
{
    internal static string AttunePromptKey => ModelDb.GetId<SingingLodestone>().Entry + ".attunePrompt";
    internal static string ChargePromptKey => ModelDb.GetId<SingingLodestone>().Entry + ".chargePrompt";

    private const string PortraitFile = "singing_lodestone.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/singing_lodestone.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new FerrosandBiome().Id };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new GoldVar(FerrosandRules.SiftGold),
    };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    /// <summary>A Magnetic card that can still be upgraded (Charge the Iron's choices).</summary>
    internal static bool CanCharge(CardModel card) => card.IsUpgradable && card.TryGetModifier<MagneticModifier>(out _);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        int magnetizable = Owner!.Deck.Cards.Count(MagneticModifier.CanMagnetize);
        EventOption attune = FerrosandRules.CanAttune(Owner.Creature.CurrentHp, magnetizable)
            ? Option(Attune)
            : LockedOption("ATTUNE_LOCKED");
        EventOption charge = Owner.Deck.Cards.Any(CanCharge)
            ? Option(ChargeTheIron)
            : LockedOption("CHARGE_THE_IRON_LOCKED");

        return new[]
        {
            attune,
            charge,
            Option(SiftTheSand),
        };
    }

    private async Task Attune()
    {
        var prefs = new CardSelectorPrefs(new LocString("events", AttunePromptKey), 1, FerrosandRules.AttuneMaxCards)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };

        List<CardModel> cards = (await CardSelectCmd.FromDeckGeneric(Owner!, prefs, MagneticModifier.CanMagnetize)).ToList();
        foreach (CardModel card in cards)
        {
            MagneticModifier.TryAdd(card);
        }

        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            FerrosandRules.AttuneHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
        SetEventFinished(PageDescription("ATTUNED"));
    }

    private async Task ChargeTheIron()
    {
        int count = FerrosandRules.ChargeSelectCount(Owner!.Deck.Cards.Count(CanCharge));
        var prefs = new CardSelectorPrefs(new LocString("events", ChargePromptKey), 1, count)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };

        List<CardModel> cards = (await CardSelectCmd.FromDeckGeneric(Owner!, prefs, CanCharge)).ToList();
        foreach (CardModel card in cards)
        {
            CardCmd.Upgrade(card);
        }

        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature, FerrosandRules.ChargeMaxHpLoss, isFromCard: false);
        SetEventFinished(PageDescription("CHARGED"));
    }

    private async Task SiftTheSand()
    {
        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner!);
        await RewardsCmd.OfferCustom(Owner!, new List<Reward> { new PotionReward(Owner!) });
        SetEventFinished(PageDescription("SIFTED"));
    }
}
