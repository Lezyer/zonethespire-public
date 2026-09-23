using System;

namespace ZoneTheSpire.Run;

/// <summary>
/// Clears the mod's static state that belongs to one run, so nothing reaches the next run: called when a run is created or
/// loaded (RunState.CreateForNewRun / FromSerializable) and when the game cleans a run up (RunManager.CleanUp: win, loss,
/// abandon, quit to menu, disconnect). Each part is cleared on its own, so one failure doesn't keep the others.
/// </summary>
internal static class RunLifecycle
{
    public static void Reset()
    {
        Clear("Troubled Dreams free action", Campfire.TroubledDreamsRestSiteOption.Reset);
        Clear("potion row scroll", Rendering.SpecialSlotVisuals.Reset);
        Clear("clone registry", Effects.CloneRegistry.Reset);
        Clear("zone cache", ZoneService.Reset);
        Clear("debug zone override", ZoneOverride.Reset);
        Clear("zone outlines", Rendering.ZoneOverlay.Reset);
        Clear("Shadowed Path cache", Shadow.ShadowPath.Reset);
    }

    private static void Clear(string what, Action reset)
    {
        try
        {
            reset();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to clear the {what} between runs: {ex.Message}");
        }
    }
}
