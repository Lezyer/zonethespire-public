using System;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using ZoneTheSpire.Run;

namespace ZoneTheSpire;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public const string ModId = "ZoneTheSpire";
    private const string HarmonyId = "reyzel.zonethespire";

    public static void Initialize()
    {
        var harmony = new Harmony(HarmonyId);
        var patchTypes = typeof(ModEntry).Assembly.GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length > 0)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        int applied = 0;
        int skipped = 0;
        foreach (var type in patchTypes)
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
                applied++;
            }
            catch (Exception ex)
            {
                skipped++;
                Log.Warn($"Skipping patch class {type.FullName}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Log.Info($"Initialized: {applied} patch classes applied, {skipped} skipped.");
        Run.Localization.ModLocalization.Initialize();
        RestSiteLoc.Inject();
        Run.Devas.Karma.RegisterCardText();
        Run.Effects.ZoneEffectRegistry.Validate();
    }
}
