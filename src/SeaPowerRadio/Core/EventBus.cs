using System;
using SeaPower;

namespace SeaPowerRadio.Core;

/// <summary>A voice line the game asked for, captured before <c>VoiceMessage</c> queues it.</summary>
internal sealed class StockVoiceRequest
{
    public string Key;            // voice.ini key, e.g. "UnableToComply", "ContactFaded"
    public ObjectBase Speaker;    // null for taskforce-level reports
    public int Number;            // -1 = none
    public bool Appended;         // AddVoiceToSequence (continues the previous transmission)
    public int Frame;
}

/// <summary>A flight deck created an aircraft. Indices point into <c>Deck._vehiclesOnBoard</c> / its squadrons.</summary>
internal sealed class AircraftLaunch
{
    public FlightDeck Deck;
    public int VehicleIndex;
    public int SquadronIndex;
    public ObjectBase Aircraft;
}

/// <summary>
/// Game events the shared hooks do not carry (mission start and end are Rig.Mission). A Harmony patch publishes, the roster
/// subscribes. Handlers run inside game code, so every invoke is guarded: a broken subscriber must never break the game method it was called from.
/// </summary>
internal static class EventBus
{
    public static event Action<AircraftLaunch> AircraftLaunched;

    public static void PublishAircraftLaunched(AircraftLaunch launch) => Safe(AircraftLaunched, launch, nameof(AircraftLaunched));

    private static void Safe<T>(Action<T> handlers, T arg, string name)
    {
        if (handlers == null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
        {
            try { handler(arg); }
            catch (Exception ex) { Rig.Log.Error($"{name} handler failed: {ex}"); }
        }
    }
}
