using System.IO;

namespace SeaPowerRadio.Voices;

/// <summary>
/// Where the base mod lives. Its <c>voices/</c> folder is the NATO (English) voice pack and, as every pack, holds
/// <c>voicepack.ini</c>, <c>personas.txt</c> (<c>id|language|gender|accent</c>), the catalog files and one folder of
/// clips per persona. All packs - this one and those of other mod folders - are found by <see cref="VoicePacks"/>.
/// </summary>
internal static class VoiceLibrary
{
    public static string ModDirectory { get; } =
        Path.GetDirectoryName(typeof(VoiceLibrary).Assembly.Location) ?? ".";

    /// <summary>A pack inside Radio's own folder (manual installs and Radio before 2.0; since 2.0 English is its own item).</summary>
    public static string VoicesDirectory => Path.Combine(ModDirectory, "voices");

    /// <summary>
    /// Radio's own data, the same for every language (2.0): <c>nations.txt</c> (country groups of the options page) and
    /// <c>stockmap.txt</c> (which of the game's voice keys the mod speaks, for packs without their own).
    /// </summary>
    public static string DataDirectory => Path.Combine(ModDirectory, "data");

    /// <summary>A data file: Radio's data folder first, then its voices folder (layout before 2.0).</summary>
    public static string DataFile(string name)
    {
        string file = Path.Combine(DataDirectory, name);
        return File.Exists(file) ? file : Path.Combine(VoicesDirectory, name);
    }
}
