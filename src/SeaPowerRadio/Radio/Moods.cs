using System;
using System.Collections.Generic;
using SeaPowerRadio.Roster;
using SeaPowerRadio.Voices;
using UnityEngine;

namespace SeaPowerRadio.Radio;

/// <summary>
/// How a call is spoken: neutral (the base library), stressed, happy or sad. The mood clips come from a mood pack
/// (<c>moodpack.ini</c>, see <see cref="VoicePacks"/>) as <c>&lt;persona&gt;/&lt;mood&gt;/&lt;key&gt;.mp3</c>. A clip the mood lacks is
/// spoken neutral, so a mood that covers only some phrases (happy, sad) mixes with the neutral callsign.
/// <list type="bullet">
/// <item>The call's own event decides first: a kill, a hit, a rescue, mission success → happy; a failure, survivors in the water,
/// out of weapons, a lost contact → sad.</item>
/// <item>Otherwise warnings and weapons calls are stressed, and so is everything a unit says for a while after its own warning
/// (missile inbound, we are hit, flooding ...): the danger carries over into its next calls.</item>
/// <item>A confirmation shortly after a success sounds pleased.</item>
/// </list>
/// </summary>
internal static class Moods
{
    public const string Stressed = "stressed", Happy = "happy", Sad = "sad";

    private const float StressAfterWarning = 60f;   // seconds a unit stays stressed after its own warning call
    private const float StressAfterCombat = 20f;    // ... after its own weapons call
    private const float HappyAfterSuccess = 40f;    // ... pleased after a kill, a rescue, mission success

    private static readonly HashSet<string> HappyEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SplashOne", "SplashTrack", "TargetDestroyed", "Shack", "HitTrack", "MissionComplete", "ObjectiveComplete",
        "SarPickup", "SarPickupCount", "SarPickupMany", "SarDelivered", "HeloRecovered", "AllAircraftRecovered", "ContactRegained",
    };

    private static readonly HashSet<string> SadEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "MissionFailed", "ObjectiveFailed", "SarFull", "SarSurvivors", "Winchester", "BatteryLow", "NeedToCharge",
        "DeckDamaged", "ContactFaded", "ContactsFaded",
    };

    private sealed class State
    {
        public float StressedUntil = float.MinValue;
        public float HappyUntil = float.MinValue;
    }

    private static readonly Dictionary<RosterElement, State> States = new Dictionary<RosterElement, State>();

    public static bool IsMood(string name) =>
        string.Equals(name, Stressed, StringComparison.OrdinalIgnoreCase) || string.Equals(name, Happy, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, Sad, StringComparison.OrdinalIgnoreCase);

    public static void Reset() => States.Clear();

    /// <summary>
    /// The clip keys to load for <paramref name="t"/>: each key that exists in the call's mood as "mood/key", the rest as they
    /// are. Same length and order as <c>t.Clips</c>. Also updates the speaker's state for its next calls.
    /// </summary>
    public static IList<string> Apply(Transmission t, out string mood)
    {
        mood = null;
        VoicePack pack = VoicePacks.OfVoice(t.Voice);
        if (pack == null || pack.Moods.Count == 0 || Settings.Moods == null || !Settings.Moods.Value)
            return t.Clips;

        mood = Pick(t, pack);
        if (mood == null)
            return t.Clips;

        var keys = new List<string>(t.Clips.Count);
        int moody = 0;
        foreach (string key in t.Clips)
        {
            if (key != null && ClipLibrary.Exists(t.Voice, mood + "/" + key))
            {
                keys.Add(mood + "/" + key);
                moody++;
            }
            else
                keys.Add(key);
        }
        if (moody == 0)
            mood = null;
        return keys;
    }

    private static string Pick(Transmission t, VoicePack pack)
    {
        float now = Time.unscaledTime;
        State s = null;
        if (t.Speaker != null && !States.TryGetValue(t.Speaker, out s))
            States[t.Speaker] = s = new State();

        CallCategory category = CallCategories.Of(t.Phrase, t.Role);
        string phrase = t.Phrase ?? "";

        string mood;
        if (HappyEvents.Contains(phrase))
        {
            mood = Happy;
            if (s != null) s.HappyUntil = now + HappyAfterSuccess;
        }
        else if (SadEvents.Contains(phrase))
        {
            mood = Sad;
        }
        else if (category == CallCategory.Warnings || category == CallCategory.Combat)
        {
            mood = Stressed;
            if (s != null)
                s.StressedUntil = Math.Max(s.StressedUntil, now + (category == CallCategory.Combat ? StressAfterCombat : StressAfterWarning));
        }
        else if (s != null && now < s.StressedUntil)
            mood = Stressed;
        else if (s != null && now < s.HappyUntil && t.Role == CallRole.Reply)
            mood = Happy;
        else
            mood = null;

        return mood != null && pack.Moods.Contains(mood) ? mood : null;
    }
}
