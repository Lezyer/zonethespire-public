using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace ZoneTheSpire.Rendering;

/// <summary>
/// Loose PNG textures shipped in &lt;mod folder&gt;/textures (replace a file to change the art). Loaded once and cached;
/// a missing or broken file logs one warning and falls back to the vanilla texture. Local presentation only.
/// </summary>
internal static class ModTextures
{
    private static readonly Dictionary<string, Texture2D?> Loaded = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);

    public static string TexturesDirectory =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(ModTextures).Assembly.Location) ?? string.Empty, "textures");

    public static Texture2D? Get(string fileName, string vanillaResPath) => GetCustom(fileName) ?? LoadVanilla(vanillaResPath);

    /// <summary>The loose PNG only (no vanilla fallback), or null when it is missing or broken. Cached like <see cref="Get"/>.</summary>
    /// <remarks>
    /// The game's asset cache disposes the resources it loaded for a room when it leaves it, and a texture registered with
    /// <see cref="RegisterAsResource"/> is the very object it gets back from ResourceLoader. A cached texture that was disposed
    /// that way is reloaded from disk instead of being reused (reusing it throws ObjectDisposedException, e.g. entering the same
    /// event a second time in one game session).
    /// </remarks>
    public static Texture2D? GetCustom(string fileName)
    {
        if (!Loaded.TryGetValue(fileName, out Texture2D? texture) || (texture != null && !GodotObject.IsInstanceValid(texture)))
        {
            texture = LoadCustom(fileName);
            Loaded[fileName] = texture;
        }

        return texture;
    }

    /// <summary>
    /// Registers the loose PNG in Godot's resource cache under <paramref name="resPath"/>, so the game's own loading code
    /// (ResourceLoader.Load and Exists with cache reuse, and the game's asset cache) finds it at that path. Returns the path, or
    /// null when the PNG is missing or broken. If the game disposed the registered texture (it does when it unloads a room's assets),
    /// a fresh copy is loaded and registered again.
    /// </summary>
    public static string? RegisterAsResource(string fileName, string resPath)
    {
        try
        {
            if (GetCustom(fileName) is not { } texture)
            {
                return null;
            }

            if (texture.ResourcePath != resPath)
            {
                texture.TakeOverPath(resPath);
            }

            return resPath;
        }
        catch (Exception ex)
        {
            // Never let a picture break room entry: callers fall back to the game's default art.
            WarnOnce("register:" + fileName, $"Failed to register texture '{fileName}' as '{resPath}': {ex.Message}");
            Loaded.Remove(fileName);
            return null;
        }
    }

    private static Texture2D? LoadCustom(string fileName)
    {
        string path = System.IO.Path.Combine(TexturesDirectory, fileName);
        try
        {
            if (!File.Exists(path))
            {
                WarnOnce(fileName, $"Texture '{path}' not found; using the vanilla texture.");
                return null;
            }

            Image image = Image.LoadFromFile(path);
            if (image == null || image.IsEmpty())
            {
                WarnOnce(fileName, $"Texture '{path}' could not be decoded; using the vanilla texture.");
                return null;
            }

            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            WarnOnce(fileName, $"Failed to load texture '{path}': {ex.Message}; using the vanilla texture.");
            return null;
        }
    }

    private static Texture2D? LoadVanilla(string resPath)
    {
        try
        {
            return PreloadManager.Cache.GetTexture2D(resPath);
        }
        catch (Exception ex)
        {
            WarnOnce(resPath, $"Failed to load vanilla texture '{resPath}': {ex.Message}");
            return null;
        }
    }

    private static void WarnOnce(string key, string message)
    {
        if (Warned.Add(key))
        {
            Log.Warn(message);
        }
    }
}
