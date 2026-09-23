using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Rendering;
using ZoneTheSpire.Run.ZoneRelics;

namespace ZoneTheSpire.Run.ZoneEvents;

/// <summary>
/// Shadow Corruption event. A cursed creature passes its curse on and hands over a lantern; a voice urges you to smash it.
/// Each player chooses on their own: smash the lantern (add the vanilla Guilty curse, which leaves after 5 fights) or take it
/// (obtain Last-Light Lantern). Every change goes through the game's own commands, so it is saved and replicated normally.
/// </summary>
public sealed class TheLanternBearer : ZoneEventModel
{
    private const string PortraitFile = "the_lantern_bearer.png";
    private const string PortraitResourcePath = "res://ZoneTheSpire/events/the_lantern_bearer.png";

    private static readonly IReadOnlyList<string> Zones = new[] { new ShadowCorruptionBiome().Id };

    public override IReadOnlyList<string> ZoneIds => Zones;

    public override string? CustomInitialPortraitPath =>
        ModTextures.RegisterAsResource(PortraitFile, PortraitResourcePath);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        Option(SmashTheLantern, HoverTipFactory.FromCardWithCardHoverTips<Guilty>(), "INITIAL"),
        Option(TakeTheLantern, HoverTipFactory.FromRelic<LastLightLantern>(), "INITIAL"),
    };

    private async Task SmashTheLantern()
    {
        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Guilty>() }, Owner!);
        SetEventFinished(PageDescription("SMASHED"));
    }

    private async Task TakeTheLantern()
    {
        await RelicCmd.Obtain(ModelDb.Relic<LastLightLantern>().ToMutable(), Owner!);
        SetEventFinished(PageDescription("TAKEN"));
    }
}
