using BaseLib.Abstracts;
using ZoneTheSpire.Rendering;

namespace ZoneTheSpire.Run.ZoneRelics;

/// <summary>
/// A mod relic whose icons are loose, editable PNGs in the mod's textures folder: &lt;TextureName&gt;.png (small icon,
/// 85x85), &lt;TextureName&gt;_outline.png (its outline) and &lt;TextureName&gt;_big.png (large icon). Each PNG is registered in
/// Godot's resource cache under its own res:// path and the relic's icon paths point there, so every place the game loads a
/// relic icon (relic bar, chests, rewards, inspect screen) gets it. Patching RelicModel's Icon/BigIcon getters doesn't work:
/// they are one-liners the JIT inlines into their callers. A missing PNG keeps the vanilla icon named by IconBaseName.
/// </summary>
public abstract class ModArtRelicModel : CustomRelicModel
{
    private const string ResourceFolder = "res://ZoneTheSpire/relics/";

    protected abstract string TextureName { get; }

    public override string PackedIconPath => ModPath(string.Empty) ?? base.PackedIconPath;

    protected override string PackedIconOutlinePath => ModPath("_outline") ?? base.PackedIconOutlinePath;

    protected override string BigIconPath => ModPath("_big") ?? base.BigIconPath;

    private string? ModPath(string suffix)
    {
        string fileName = TextureName + suffix + ".png";
        return ModTextures.RegisterAsResource(fileName, ResourceFolder + fileName);
    }
}
