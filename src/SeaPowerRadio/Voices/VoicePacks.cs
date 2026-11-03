using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeaPowerRadio.Radio;
using UnityEngine;

namespace SeaPowerRadio.Voices;

/// <summary>
/// Finds the installed voice packs and says which one speaks for a country. Discovery scans every folder the game
/// loads mods from – <c>StreamingAssets/&lt;folder&gt;</c> and the Steam Workshop items of the game – plus the base
/// mod's own folder, for a <c>voicepack.ini</c> at the top level or in <c>voices/</c>. It reads the folders
/// itself instead of asking the game's FileManager (whose API differs between the two game versions).
/// </summary>
internal static class VoicePacks
{
    public const string Auto = "Auto";
    public const string GameVoice = "Game voice";
    public const string NatoId = "nato-en";            // the base mod's own pack: its seed stays as in 1.1.0

    private static readonly List<VoicePack> All = new List<VoicePack>();
    private static readonly HashSet<string> Warned = new HashSet<string>();

    public static IReadOnlyList<VoicePack> Installed => All;

    /// <summary>The stock key table (NATO pack's, else the first pack's): which keys the mod knows, for lines of units no pack speaks for.</summary>
    public static Catalog StockCatalog => (ById(NatoId) ?? All.FirstOrDefault())?.Catalog;

    public static VoicePack ById(string id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The pack a voice id ("packId/personaId") belongs to.</summary>
    public static VoicePack OfVoice(string voice)
    {
        if (voice == null) return null;
        int slash = voice.IndexOf('/');
        return ById(slash < 0 ? voice : voice.Substring(0, slash));
    }

    public static string PersonaOf(string voice) => voice.Substring(voice.IndexOf('/') + 1);

    public static void Discover(Version baseVersion)
    {
        All.Clear();
        var folders = new List<string> { VoiceLibrary.ModDirectory };
        folders.AddRange(ModFolders());

        var found = new Dictionary<string, VoicePack>(StringComparer.OrdinalIgnoreCase);
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in folders)
        {
            foreach (string dir in new[] { folder, Path.Combine(folder, "voices") })
            {
                string full;
                try { full = Path.GetFullPath(dir); } catch (Exception) { continue; }
                if (!File.Exists(Path.Combine(full, "voicepack.ini")) || !seenDirs.Add(full))
                    continue;
                try
                {
                    VoicePack pack = VoicePack.ReadIni(full, baseVersion);
                    if (pack == null) continue;
                    if (found.TryGetValue(pack.Id, out VoicePack other))
                    {
                        bool newer = pack.Version > other.Version;
                        ModLog.Info($"Voice pack '{pack.Id}' found twice ({other.Directory} {other.Version}, {pack.Directory} {pack.Version}): keeping {(newer ? pack.Directory : other.Directory)}.");
                        if (!newer) continue;
                    }
                    found[pack.Id] = pack;
                }
                catch (Exception ex)
                {
                    ModLog.Warn($"Voice pack in {full} could not be read: {ex.Message}");
                }
            }
        }

        // The base's own pack loads first: its stock map is the default for packs without a stockmap.txt.
        Catalog baseCatalog = null;
        string baseDir = Path.GetFullPath(VoiceLibrary.VoicesDirectory);
        foreach (VoicePack pack in found.Values.OrderBy(p => p.Id == NatoId ? 0 : 1).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!pack.LoadContent(baseCatalog))
                    continue;
                if (baseCatalog == null && string.Equals(Path.GetFullPath(pack.Directory), baseDir, StringComparison.OrdinalIgnoreCase))
                    baseCatalog = pack.Catalog;
                All.Add(pack);
            }
            catch (Exception ex)
            {
                ModLog.Warn($"Voice pack '{pack.Id}' could not be loaded: {ex.Message}");
            }
        }
        ModLog.Info($"Voice packs installed: {(All.Count == 0 ? "none" : string.Join(", ", All.Select(p => $"{p.Id} {p.Version}")))}");
    }

    public static IEnumerable<string> ModFolders()
    {
        var roots = new List<string>();
        // Each root on its own: one that cannot be worked out must not drop the others.
        TryRoot(roots, () => Application.streamingAssetsPath);
        // <game>/Sea Power_Data -> steamapps/workshop/content/<app id>/<item id>
        TryRoot(roots, () => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "workshop", "content", "1286220")));
        TryRoot(roots, () =>
        {
            string parent = Directory.GetParent(VoiceLibrary.ModDirectory)?.FullName;
            return parent != null && parent.IndexOf("workshop", StringComparison.OrdinalIgnoreCase) >= 0 ? parent : null;   // the base itself sits in a Workshop item folder
        });
        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root)) continue;
            string[] subs;
            try { subs = Directory.GetDirectories(root); } catch (Exception) { continue; }
            foreach (string sub in subs)
            {
                string name = Path.GetFileName(sub);
                if (name == "original" || name == "user" || name == "EntityScenes") continue;
                yield return sub;
            }
        }
    }

    private static void TryRoot(List<string> roots, Func<string> root)
    {
        try
        {
            string path = root();
            if (!string.IsNullOrEmpty(path))
                roots.Add(path);
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Could not work out a mod folder root: {ex.Message}");
        }
    }

    /// <summary>
    /// The pack that speaks for a country under the player's setting, or null for the game's own voice.
    /// <c>Auto</c> = the pack that lists the country; an unknown value (a pack that was uninstalled) acts as Auto.
    /// </summary>
    public static VoicePack Resolve(string nation)
    {
        string choice = Settings.NationChoice(nation);
        if (string.Equals(choice, GameVoice, StringComparison.OrdinalIgnoreCase))
            return null;
        if (!string.Equals(choice, Auto, StringComparison.OrdinalIgnoreCase))
        {
            VoicePack chosen = ById(choice);
            if (chosen != null)
                return chosen;
            if (Warned.Add(nation + "|" + choice))
                ModLog.Warn($"{nation}: voice pack '{choice}' is not installed, using Auto.");
        }
        return AutoPack(nation);
    }

    public static VoicePack AutoPack(string nation) => All.FirstOrDefault(p => p.ListsNation(nation));
}
