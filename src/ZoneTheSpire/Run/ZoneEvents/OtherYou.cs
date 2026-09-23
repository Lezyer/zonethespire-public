using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.Effects;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Mirrorlands event. Each player chooses on their own: copy a card of their choice for 6 Max HP (the same exact copy as the
/// Duplicate shop service), give one card Glam in exchange for Shame, or smash the mirror for 3 random relics and Bad Luck.
/// Every change goes through the game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class OtherYou : ZoneEventModel
{
    private const string PortraitFile = "other_you.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/other_you.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new MirrorlandsBiome().Id };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption shake = MirrorRules.CanShakeHands(Owner!.Creature.MaxHp)
            ? Option(ShakeHands)
            : LockedOption("SHAKE_HANDS_LOCKED");

        Glam glam = ModelDb.Enchantment<Glam>();
        EventOption turnAway = Owner.Deck.Cards.Any(glam.CanEnchant)
            ? Option(TurnAway, HoverTipFactory.FromEnchantment<Glam>().Concat(HoverTipFactory.FromCardWithCardHoverTips<Shame>()), "INITIAL")
            : LockedOption("TURN_AWAY_LOCKED");

        return new[]
        {
            shake,
            turnAway,
            Option(SmashIt, HoverTipFactory.FromCardWithCardHoverTips<BadLuck>(), "INITIAL"),
        };
    }

    private async Task ShakeHands()
    {
        var prefs = new CardSelectorPrefs(ZoneTheSpireModifier.ModLoc("duplicate_prompt"), 1)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
        };

        CardModel? card = (await CardSelectCmd.FromDeckGeneric(Owner!, prefs)).FirstOrDefault();
        if (card != null)
        {
            await MirrorDuplication.AddCopyToDeck(card, Owner!);
        }

        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature, MirrorRules.ShakeHandsMaxHpLoss, isFromCard: false);
        SetEventFinished(PageDescription("SHOOK_HANDS"));
    }

    private async Task TurnAway()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, MirrorRules.TurnAwayGlamCount);
        CardModel? card = (await CardSelectCmd.FromDeckForEnchantment(Owner!, ModelDb.Enchantment<Glam>(), 1, prefs)).FirstOrDefault();
        if (card != null)
        {
            CardCmd.Enchant<Glam>(card, 1m);
            NCardEnchantVfx? vfx = NCardEnchantVfx.Create(card);
            if (vfx != null)
            {
                NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
            }
        }

        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Shame>() }, Owner!);
        SetEventFinished(PageDescription("TURNED_AWAY"));
    }

    private async Task SmashIt()
    {
        for (int i = 0; i < MirrorRules.SmashItRelics; i++)
        {
            RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner!).ToMutable();
            await RelicCmd.Obtain(relic, Owner!);
        }

        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<BadLuck>() }, Owner!);
        SetEventFinished(PageDescription("SMASHED"));
    }
}
