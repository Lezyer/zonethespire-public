using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Swaps a creature body's material (Spine "normal" material or the plain body material) and restores the original later.
/// Local rendering only; never affects gameplay.
/// </summary>
internal static class CreatureShaderMaterial
{
    private const string PrevMaterialMetaKey = "zonethespire_prev_material";
    private const int MaxLayoutRetries = 3;

    /// <summary>
    /// Like <see cref="Apply"/>, but for creatures whose node may not be in the scene yet (added mid-fight): retries on the
    /// next frames. <paramref name="create"/> is only called once the node exists.
    /// </summary>
    public static void ApplyWhenReady(Creature creature, Func<Material> create, int attempt = 0)
    {
        try
        {
            NCreature? node = NCombatRoom.Instance?.GetCreatureNode(creature);
            if (node == null || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
            {
                if (attempt < MaxLayoutRetries)
                {
                    Callable.From(() => ApplyWhenReady(creature, create, attempt + 1)).CallDeferred();
                }

                return;
            }

            Apply(creature, create());
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply a creature shader: {ex}");
        }
    }

    public static void Apply(Creature creature, Material material)
    {
        try
        {
            NCreatureVisuals? visuals = VisualsOf(creature);
            if (visuals == null)
            {
                return;
            }

            // Remember whatever material was in place before the first swap, so Remove can restore it. Later swaps
            // (e.g. Mirrored -> Reflection) must not overwrite the real original with one of ours.
            if (!visuals.Body.HasMeta(PrevMaterialMetaKey))
            {
                Material? previous = visuals.SpineBody != null ? visuals.SpineBody.GetNormalMaterial() : visuals.Body.Material;
                visuals.Body.SetMeta(PrevMaterialMetaKey, previous!);
            }

            if (visuals.SpineBody != null)
            {
                visuals.SpineBody.SetNormalMaterial(material);
            }
            else
            {
                visuals.Body.Material = material;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply a creature shader: {ex}");
        }
    }

    public static void Remove(Creature creature)
    {
        try
        {
            NCreatureVisuals? visuals = VisualsOf(creature);
            if (visuals == null)
            {
                return;
            }

            Material? previous = visuals.Body.HasMeta(PrevMaterialMetaKey)
                ? visuals.Body.GetMeta(PrevMaterialMetaKey).As<Material>()
                : null;
            visuals.Body.RemoveMeta(PrevMaterialMetaKey);

            if (visuals.SpineBody != null)
            {
                visuals.SpineBody.SetNormalMaterial(previous!);
            }
            else
            {
                visuals.Body.Material = previous;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to remove a creature shader: {ex}");
        }
    }

    private static NCreatureVisuals? VisualsOf(Creature creature)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(creature);
        return node != null && GodotObject.IsInstanceValid(node) ? node.Visuals : null;
    }
}
