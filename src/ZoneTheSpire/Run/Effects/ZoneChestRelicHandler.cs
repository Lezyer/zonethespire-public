namespace ZoneTheSpire.Run.Effects;

/// <summary>
/// The shared chest relic effect (the tooltip on treasure nodes of zones with relics). The offer itself is added by ZoneChestRelicOfferPatch for
/// any biome with relics in ZoneRelicCatalog; this handler only marks the effect as implemented.
/// </summary>
internal sealed class ZoneChestRelicHandler(string effectId) : ZoneEffectHandler
{
    public override string EffectId => effectId;
}
