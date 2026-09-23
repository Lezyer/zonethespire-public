using System.Collections.Generic;
using ZoneTheSpire.Core.Localization;
using ZoneTheSpire.Core.Mirror;
using ZoneTheSpire.Core.Model;

namespace ZoneTheSpire.Core.Biomes;

public sealed class MirrorlandsBiome : BiomeDefinition
{
    public const string MirroredFoesEffectId = "mirrorlands.mirrored_foes";
    public const string DuplicateServiceEffectId = "mirrorlands.duplicate_service";
    public const string MirroredCampfireEffectId = "mirrorlands.mirrored_campfire";

    private static readonly IReadOnlyList<BiomeEffect> MirrorlandsEffects = new[]
    {
        new BiomeEffect(MirroredFoesEffectId,
            new[] { NodeKind.Monster, NodeKind.Elite, NodeKind.Unknown }),
        new BiomeEffect(DuplicateServiceEffectId,
            new[] { NodeKind.Shop }),
        new BiomeEffect(MirroredCampfireEffectId,
            new[] { NodeKind.RestSite }),
    };

    /// <summary>Rebuilt on every access, so it follows the game's language.</summary>
    private static IReadOnlyList<ZoneKeyword> ZoneKeywords => new[]
    {
        new ZoneKeyword("mirror.mirrored", MirrorText.MirroredDescription),
        new ZoneKeyword("mirror.reflection", MirrorText.ReflectionDescription),
    };

    public override string Id => "mirrorlands";

    public override string DisplayName => ModText.Get("zone.mirrorlands.name");

    public override string ColorHex => "8FD3F4";

    public override IReadOnlyList<ZoneKeyword> Keywords => ZoneKeywords;

    public override IReadOnlyList<BiomeEffect> Effects => MirrorlandsEffects;
}
