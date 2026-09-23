using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Shadow;

/// <summary>
/// Shadow Corruption fight card: 1 cost, Exhaust. Reveals enemy intents for the rest of the turn, but only to the player who
/// played it (see ShadowSight; the "only you" note only shows in multiplayer). It also reveals the Shaded cards in that player's
/// hand (on every peer) and removes 10 of their Doom. Five are shuffled into each player's draw pile at the start of every zone
/// fight. A token card: it never appears in rewards or shops.
/// </summary>
[Pool(typeof(TokenCardPool))]
public sealed class LightTheWay : CustomCardModel
{
    public LightTheWay()
        : base(1, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedByModifiers => false;

    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override Texture2D? CustomPortrait => ModTextures.GetCustom("light_the_way.png");

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<DoomPower>() };

    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("IsMultiplayer", IsMutable && Owner?.RunState.Players.Count > 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ShadowSight.Reveal(Owner);
        ShadedCards.UnshadeHand(Owner);
        await ShadowDoom.Remove(choiceContext, Owner.Creature, ShadowRules.LightTheWayDoomRemoved, this);
    }
}
