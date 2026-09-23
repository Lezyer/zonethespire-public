using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using ZoneTheSpire.Core.Scrapyard;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.Scrapyard;

/// <summary>
/// What the Scrapyard Automaton curse becomes after 3 combats: a 0-cost Power that gives 3 Strength, 3 Artifact and 15
/// Plating (4, 4 and 20 upgraded). In the event card pool with Event rarity, which the game never draws random cards from.
/// </summary>
[Pool(typeof(EventCardPool))]
public sealed class AutomatonForm : CustomCardModel
{
    private const string PortraitFile = "automaton_form.png";

    public AutomatonForm()
        : base(0, CardType.Power, CardRarity.Event, TargetType.Self)
    {
    }

    public override bool CanBeGeneratedByModifiers => false;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<StrengthPower>(ScrapyardRules.AutomatonFormStrength),
        new PowerVar<ArtifactPower>(ScrapyardRules.AutomatonFormArtifact),
        new PowerVar<PlatingPower>(ScrapyardRules.AutomatonFormPlating),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<ArtifactPower>(),
        HoverTipFactory.FromPower<PlatingPower>(),
    };

    public override Texture2D? CustomPortrait => ModTextures.GetCustom(PortraitFile);

    protected override void OnUpgrade()
    {
        DynamicVars["StrengthPower"].UpgradeValueBy(ScrapyardRules.AutomatonFormUpgradedStrength - ScrapyardRules.AutomatonFormStrength);
        DynamicVars["ArtifactPower"].UpgradeValueBy(ScrapyardRules.AutomatonFormUpgradedArtifact - ScrapyardRules.AutomatonFormArtifact);
        DynamicVars["PlatingPower"].UpgradeValueBy(ScrapyardRules.AutomatonFormUpgradedPlating - ScrapyardRules.AutomatonFormPlating);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["StrengthPower"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<ArtifactPower>(choiceContext, Owner.Creature, DynamicVars["ArtifactPower"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<PlatingPower>(choiceContext, Owner.Creature, DynamicVars["PlatingPower"].BaseValue, Owner.Creature, this);
    }
}
