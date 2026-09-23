using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using ZoneTheSpire.Core.Biomes;
using ZoneTheSpire.Core.ZoneRelics;

namespace ZoneTheSpire.Run.Effects;

internal static class ZoneEffectRegistry
{
    /// <summary>Every effect handler. Add new handlers here.</summary>
    private static readonly ZoneEffectHandler[] AllHandlers =
    {
        new MirroredFoesHandler(),
        new DuplicateServiceHandler(),
        new MirroredCampfireHandler(),
        new ScrapyardFightsHandler(),
        new ScrapServiceHandler(),
        new ScrapyardCampfireHandler(),
        new PrismaticStormHandler(),
        new TransformServiceHandler(),
        new PrismaticCampfireHandler(),
        new InfestationHandler(),
        new InfestationShopHandler(),
        new InfestationCampfireHandler(),
        new ZoneChestRelicHandler(ZoneRelicCatalog.ChestRelicEffect.Id),
        new BloodRainHandler(),
        new BloodRainCampfireHandler(),
        new BloodShopHandler(),
        new HallsOfMidasHandler(),
        new HallsOfMidasShopHandler(),
        new HallsOfMidasCampfireHandler(),
        new PhantasmalTombsHandler(),
        new RaidedShopHandler(),
        new PhantasmalCampfireHandler(),
        new FerrosandHandler(),
        new FerrosandShopHandler(),
        new FerrosandCampfireHandler(),
        new ForgottenEmpireHandler(),
        new ForgottenEmpireShopHandler(),
        new ForgottenEmpireCampfireHandler(),
        new ShadowFightsHandler(),
        new ShadowCampfireHandler(),
        new ShadowShopHandler(),
        new DevasFightsHandler(),
        new DevasCampfireHandler(),
        new DevasShopHandler(),
        new HoarfrostFightsHandler(),
        new HoarfrostShopHandler(),
        new HoarfrostCampfireHandler(),
        new HallowedFightsHandler(),
        new HallowedShopHandler(),
        new HallowedRepentHandler(),
        new HallowedBlasphemeHandler(),
        new FermentoryFightsHandler(),
        new FermentoryShopHandler(),
        new FermentoryDistilHandler(),
    };

    private static readonly Dictionary<string, ZoneEffectHandler> HandlersById = BuildIndex();
    private static readonly HashSet<string> WarnedMissing = new(StringComparer.Ordinal);

    public static IReadOnlyList<ZoneEffectHandler> HandlersFor(ZoneContext context)
    {
        var handlers = new List<ZoneEffectHandler>();
        foreach (var effect in context.Effects)
        {
            if (HandlersById.TryGetValue(effect.Id, out ZoneEffectHandler? handler))
            {
                handlers.Add(handler);
            }
            else if (WarnedMissing.Add(effect.Id))
            {
                Log.Warn($"No handler registered for zone effect '{effect.Id}'; it is tooltip-only.");
            }
        }

        return handlers;
    }

    /// <summary>Resolves {Token} placeholders in effect tooltip lines (except {Zone}, which ZoneTooltip handles).</summary>
    public static string? ResolveTooltipToken(string token) => token switch
    {
        "Glam" => $"[gold]{ModelDb.Enchantment<Glam>().Title.GetFormattedText()}[/gold]",
        _ => null,
    };

    /// <summary>
    /// Startup self-check (ModEntry): warns about every biome effect with no registered handler (it would only show in tooltips)
    /// and every handler whose effect id no biome declares, so a missed registration shows in the log before anyone plays the zone.
    /// </summary>
    public static void Validate()
    {
        try
        {
            // The chest relic effect is added to every zone with relics (ZoneEffects.EffectsFor), not declared by a biome.
            var declared = new HashSet<string>(StringComparer.Ordinal) { ZoneRelicCatalog.ChestRelicEffect.Id };
            int missing = 0;
            foreach (BiomeDefinition biome in BiomeRegistry.All)
            {
                foreach (BiomeEffect effect in biome.Effects)
                {
                    declared.Add(effect.Id);
                    if (!HandlersById.ContainsKey(effect.Id))
                    {
                        missing++;
                        WarnedMissing.Add(effect.Id);
                        Log.Warn($"Zone effect '{effect.Id}' ({biome.DisplayName}) has no registered handler; add one to ZoneEffectRegistry.AllHandlers.");
                    }
                }
            }

            int orphans = 0;
            foreach (string id in HandlersById.Keys.Where(id => !declared.Contains(id)))
            {
                orphans++;
                Log.Warn($"Zone effect handler for '{id}' matches no biome effect; it never runs.");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to check the zone effect handlers: {ex}");
        }
    }

    private static Dictionary<string, ZoneEffectHandler> BuildIndex()
    {
        var index = new Dictionary<string, ZoneEffectHandler>(StringComparer.Ordinal);
        foreach (ZoneEffectHandler handler in AllHandlers)
        {
            if (index.ContainsKey(handler.EffectId))
            {
                Log.Warn($"Two zone effect handlers claim '{handler.EffectId}'; {handler.GetType().Name} replaces {index[handler.EffectId].GetType().Name}.");
            }

            index[handler.EffectId] = handler;
        }

        return index;
    }
}
