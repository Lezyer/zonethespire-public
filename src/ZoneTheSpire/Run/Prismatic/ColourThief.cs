using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using ZoneTheSpire.Core.Prismatic;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Prismatic;

/// <summary>
/// The Colour Thief event card: like vanilla Discovery, but the 3 choices come from other characters and the chosen card costs 0
/// this turn. 0 cost, Exhaust; upgrading removes Exhaust. Picks use the combat card generation RNG, like Discovery, so every peer
/// generates the same choices. In the event card pool, which the game never draws random cards from.
/// </summary>
[Pool(typeof(EventCardPool))]
public sealed class ColourThief : CustomCardModel
{
    private const string PortraitFile = "colour_thief.png";

    public ColourThief()
        : base(0, CardType.Skill, CardRarity.Event, TargetType.Self)
    {
    }

    public override bool CanBeGeneratedByModifiers => false;

    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override Texture2D? CustomPortrait => ModTextures.GetCustom(PortraitFile);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> choices = CardFactory.GetDistinctForCombat(
            Owner,
            OtherCharacterCards.Unlocked(Owner),
            PrismaticRules.ColourThiefChoices,
            Owner.RunState.Rng.CombatCardGeneration).ToList();
        if (choices.Count == 0)
        {
            return;
        }

        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: true);
        if (chosen == null)
        {
            return;
        }

        chosen.EnergyCost.SetThisTurnOrUntilPlayed(0);
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
