namespace ZoneTheSpire.Core.Visuals;

/// <summary>Full-screen ambient effects some zones play in their rooms (visual only).</summary>
public enum ZoneScreenEffect
{
    None,
    BloodRain,
    PrismaticLight,
    GoldenRays,
    MirrorShine,
    DustClouds,
    BuzzingFlies,
    GhostlyForms,
    Sandstorm,
    CrumblingRuins,
    CreepingShadow,
    DriftingFrost,
    GoldenMandalas,
    BlindingRadiance,
    FermentingGas,
}

public static class ScreenEffectRules
{
    /// <summary>The screen effect for a biome id, or <see cref="ZoneScreenEffect.None"/> when it has none (or there is no zone).</summary>
    public static ZoneScreenEffect For(string? biomeId) => biomeId switch
    {
        "blood_rain" => ZoneScreenEffect.BloodRain,
        "prismatic_storm" => ZoneScreenEffect.PrismaticLight,
        "halls_of_midas" => ZoneScreenEffect.GoldenRays,
        "mirrorlands" => ZoneScreenEffect.MirrorShine,
        "scrapyard" => ZoneScreenEffect.DustClouds,
        "infestation" => ZoneScreenEffect.BuzzingFlies,
        "phantasmal_tombs" => ZoneScreenEffect.GhostlyForms,
        "ferrosand" => ZoneScreenEffect.Sandstorm,
        "forgotten_empire" => ZoneScreenEffect.CrumblingRuins,
        "shadow_corruption" => ZoneScreenEffect.CreepingShadow,
        "devas_domain" => ZoneScreenEffect.GoldenMandalas,
        "hoarfrost" => ZoneScreenEffect.DriftingFrost,
        "blinding_hallowed" => ZoneScreenEffect.BlindingRadiance,
        "fermentory" => ZoneScreenEffect.FermentingGas,
        _ => ZoneScreenEffect.None,
    };
}
