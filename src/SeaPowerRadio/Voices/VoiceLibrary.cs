using System;
using System.Collections.Generic;
using System.IO;

namespace SeaPowerRadio.Voices;

/// <summary>
/// The rendered voice library next to the plugin DLL: <c>voices/personas.txt</c> lists every persona as
/// <c>id|language|gender|accent</c>; each persona's clips live in <c>voices/&lt;id&gt;/</c> (Phase 2).
/// </summary>
internal static class VoiceLibrary
{
    public static string ModDirectory { get; } =
        Path.GetDirectoryName(typeof(VoiceLibrary).Assembly.Location) ?? ".";

    public static string VoicesDirectory => Path.Combine(ModDirectory, "voices");

    private static List<VoicePersona> personas;

    public static IReadOnlyList<VoicePersona> Personas => personas ??= Load();

    private static List<VoicePersona> Load()
    {
        var list = new List<VoicePersona>();
        string file = Path.Combine(VoicesDirectory, "personas.txt");
        if (!File.Exists(file))
        {
            ModLog.Warn($"No voice library at {file}; elements get callsigns but no voices.");
            return list;
        }
        foreach (string raw in File.ReadAllLines(file))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] f = line.Split('|');
            if (f.Length < 4)
            {
                ModLog.Warn($"personas.txt: bad line '{line}'");
                continue;
            }
            list.Add(new VoicePersona { Id = f[0].Trim(), Language = f[1].Trim(), Gender = f[2].Trim(), Accent = f[3].Trim() });
        }
        ModLog.Info($"Voice library: {list.Count} personas.");
        return list;
    }
}
