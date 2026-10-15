using System;
using SeaPower;

namespace SeaPowerRadio.Core;

/// <summary>A voice line the game asked for, captured before <c>VoiceMessage</c> queues it.</summary>
internal sealed class StockVoiceRequest
{
    public string Key;            // voice.ini key, e.g. "UnableToComply", "ContactFaded"
    public ObjectBase Speaker;    // null for taskforce-level reports (then only Nation is set)
    public string Nation;
    public int Number;            // -1 = none
    public string Format;
    public bool Sequence;         // PlayVoiceSequence
    public bool Altitude;         // PlayAircraftAltitude
    public bool Appended;         // AddVoiceToSequence (continues the previous transmission)
    public float Time;            // Time.unscaledTime
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
/// Normalized game events. Harmony patches publish, the radio/diagnostics layers subscribe. Handlers run inside game
/// code, so every invoke is guarded: a broken subscriber must never break the game method it was called from.
/// </summary>
internal static class EventBus
{
    public static event Action<StockVoiceRequest> StockVoice;
    public static event Action MissionStarted;
    public static event Action MissionEnded;
    public static event Action<AircraftLaunch> AircraftLaunched;

    public static void Publish(StockVoiceRequest request) => Safe(StockVoice, request, nameof(StockVoice));
    public static void PublishMissionStarted() => Safe(MissionStarted, nameof(MissionStarted));
    public static void PublishMissionEnded() => Safe(MissionEnded, nameof(MissionEnded));
    public static void PublishAircraftLaunched(AircraftLaunch launch) => Safe(AircraftLaunched, launch, nameof(AircraftLaunched));

    private static void Safe<T>(Action<T> handlers, T arg, string name)
    {
        if (handlers == null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
        {
            try { handler(arg); }
            catch (Exception ex) { ModLog.Error($"{name} handler failed: {ex}"); }
        }
    }

    private static void Safe(Action handlers, string name)
    {
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception ex) { ModLog.Error($"{name} handler failed: {ex}"); }
        }
    }
}
