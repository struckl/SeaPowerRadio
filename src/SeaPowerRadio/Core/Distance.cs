using UnityEngine;
using SeaPower;

namespace SeaPowerRadio.Core;

/// <summary>Distances and bearings between game positions, in the units the reports use.</summary>
internal static class Distance
{
    // GeoPosition.GetDistance returns game units of 67.200066 m each (the factor the game's own code converts with).
    public const float KmPerMapUnit = 67.200066f / 1000f;
    public const float KmPerNm = 1.852f;
    private const float NmPerMapUnit = KmPerMapUnit / KmPerNm;

    public static float Km(GeoPosition a, GeoPosition b) => (float)a.GetDistance(b) * KmPerMapUnit;

    public static float Nm(GeoPosition a, GeoPosition b) => (float)a.GetDistance(b) * NmPerMapUnit;

    /// <summary>True bearing 0-359 from one position to another.</summary>
    public static int Bearing(GeoPosition from, GeoPosition to)
    {
        float heading = from.HeadingToPosition(to);
        return Mathf.RoundToInt(((heading % 360f) + 360f) % 360f) % 360;
    }
}
