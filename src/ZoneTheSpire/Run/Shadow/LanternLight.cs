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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.Shadow;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Shadow;

/// <summary>
/// Last-Light Lantern fight card: 1 cost, Exhaust (0 upgraded, with the same numbers). Like Light the Way it reveals enemy
/// intents for the rest of the turn (only to the player who played it) and the Shaded cards in that player's hand; it also
/// heals 3, gives 1 Strength and 1 Dexterity and removes 5 Doom. The Lantern adds 5 to its holder's deck upon pickup; from then
/// on they are normal deck cards (upgradable at campfires). In the event card pool with Event rarity, which the game never
/// draws random cards from.
/// </summary>
[Pool(typeof(EventCardPool))]
public sealed class LanternLight : CustomCardModel
{
    public LanternLight()
        : base(ShadowRules.LanternLightCost, CardType.Skill, CardRarity.Event, TargetType.Self)
    {
    }

    public override bool CanBeGeneratedByModifiers => false;

    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new HealVar(ShadowRules.LanternLightHeal),
        new PowerVar<StrengthPower>(ShadowRules.LanternLightStrength),
        new PowerVar<DexterityPower>(ShadowRules.LanternLightDexterity),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromPower<DoomPower>(),
    };

    public override Texture2D? CustomPortrait => ModTextures.GetCustom("lantern_light.png");

    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("IsMultiplayer", IsMutable && Owner?.RunState.Players.Count > 1);
    }

    /// <summary>The upgrade only drops the cost: the card's numbers stay the same.</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(ShadowRules.LanternLightUpgradedCost - ShadowRules.LanternLightCost);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ShadowSight.Reveal(Owner);
        ShadedCards.UnshadeHand(Owner);
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["StrengthPower"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, DynamicVars["DexterityPower"].BaseValue, Owner.Creature, this);
        await ShadowDoom.Remove(choiceContext, Owner.Creature, ShadowRules.LanternLightDoomRemoved, this);
    }
}
