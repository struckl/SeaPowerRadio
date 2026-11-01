using System.IO;

namespace SeaPowerRadio.Voices;

/// <summary>
/// Where the base mod lives. Its <c>voices/</c> folder is the NATO (English) voice pack and, as every pack, holds
/// <c>voicepack.ini</c>, <c>personas.txt</c> (<c>id|language|gender|accent</c>), the catalog files and one folder of
/// clips per persona. All packs – this one and those of other mod folders – are found by <see cref="VoicePacks"/>.
/// </summary>
internal static class VoiceLibrary
{
    public static string ModDirectory { get; } =
        Path.GetDirectoryName(typeof(VoiceLibrary).Assembly.Location) ?? ".";

    /// <summary>The base mod's own pack (also home of nations.txt).</summary>
    public static string VoicesDirectory => Path.Combine(ModDirectory, "voices");
}
