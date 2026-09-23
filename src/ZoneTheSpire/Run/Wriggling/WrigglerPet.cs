using System.Collections.Generic;
using BaseLib.Abstracts;

namespace ZoneTheSpire.Run.Wriggling;

/// <summary>The Wriggler companion summoned by Wriggling: a pet with the Wriggler's visuals and a visible health bar.</summary>
public sealed class WrigglerPet : CustomPetModel, ILocalizationProvider
{
    public WrigglerPet()
        : base(visibleHp: true)
    {
    }

    public override int MinInitialHp => 1;

    public override int MaxInitialHp => 1;

    public override string? CustomVisualPath => "res://scenes/creature_visuals/wriggler.tscn";

    /// <summary>English text: Localization/eng/*.json (loaded by the game, see ModLocalization).</summary>
    public List<(string, string)>? Localization => null;
}
