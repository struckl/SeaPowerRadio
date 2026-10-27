#if !FLATMAP
using System;
using System.Reflection;
using SeaPower.LiveMap;

namespace SeaPowerRadio;

/// <summary>
/// The few game members the mod needs that the game keeps internal, looked up once by reflection. A lookup that
/// fails is logged once and its feature falls back (see the callers).
/// </summary>
internal static class GameApi
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly Func<MapPalette, bool> PaletteDark = Getter<MapPalette, bool>("IsDark");

    /// <summary>Whether a map shows its night colours (in Auto: at the centre of the view); false if the palette is unknown.</summary>
    public static bool IsDark(MapPalette palette) => palette != null && PaletteDark != null && PaletteDark(palette);

    private static Func<T, TResult> Getter<T, TResult>(string property)
    {
        MethodInfo get = typeof(T).GetProperty(property, Any)?.GetGetMethod(nonPublic: true);
        if (get == null)
        {
            ModLog.Warn($"{typeof(T).Name}.{property} not found (game updated?)");
            return null;
        }
        return (Func<T, TResult>)Delegate.CreateDelegate(typeof(Func<T, TResult>), get);
    }
}
#endif
