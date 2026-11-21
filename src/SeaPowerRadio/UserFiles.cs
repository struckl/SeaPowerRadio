using System;
using System.IO;
using BepInEx;

namespace SeaPowerRadio;

/// <summary>The player's own files: BepInEx/config/SeaPowerRadio, which Workshop updates never touch.</summary>
internal static class UserFiles
{
    public static string PathOf(params string[] parts) =>
        Path.Combine(Path.Combine(Paths.ConfigPath, "SeaPowerRadio"), Path.Combine(parts));

    /// <summary>Writes <paramref name="template"/> to <paramref name="file"/> if it does not exist yet; logs under <paramref name="tag"/> when it cannot.</summary>
    public static void Ensure(string file, string template, string tag)
    {
        try
        {
            if (File.Exists(file)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, template);
        }
        catch (Exception ex)
        {
            Rig.Log.Warn($"[{tag}] could not create {file}: {ex.Message}");
        }
    }
}
