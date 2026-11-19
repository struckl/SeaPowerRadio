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
/// - The call's own event decides first: a kill, a hit, a rescue, mission success -> happy; a failure, survivors in the water,
/// out of weapons, a lost contact -> sad.
/// - Otherwise warnings and weapons calls are stressed, and so is everything a unit says for a while after its own warning
/// (missile inbound, we are hit, flooding ...): the danger carries over into its next calls.
/// - A confirmation shortly after a success sounds pleased.
/// </summary>
internal static class Moods
{
    public const string Stressed = "stressed", Happy = "happy", Sad = "sad";

    private const float StressAfterWarning = 60f;   // seconds a unit stays stressed after its own warning call
    private const float StressAfterCombat = 20f;    // ... after its own weapons call
    private const float HappyAfterSuccess = 40f;    // ... pleased after a kill, a rescue, mission success

    private static readonly HashSet<string> HappyEvents = CallCategories.PhraseSet(ClipRules.Happy);
    private static readonly HashSet<string> SadEvents = CallCategories.PhraseSet(ClipRules.Sad);

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
        // A call without a speaker carries no mood over: its state is thrown away.
        State s = new State();
        if (t.Speaker != null && !States.TryGetValue(t.Speaker, out s))
            States[t.Speaker] = s = new State();
        string mood = EventMood(t, s, now) ?? CarriedMood(t, s, now);
        return mood != null && pack.Moods.Contains(mood) ? mood : null;
    }

    /// <summary>The mood of the call's own event; marks how long it carries over into the speaker's next calls.</summary>
    private static string EventMood(Transmission t, State s, float now)
    {
        string phrase = t.Phrase ?? "";
        if (HappyEvents.Contains(phrase))
        {
            s.HappyUntil = now + HappyAfterSuccess;
            return Happy;
        }
        if (SadEvents.Contains(phrase))
            return Sad;
        CallCategory category = CallCategories.Of(t.Phrase, t.Role);
        if (category != CallCategory.Warnings && category != CallCategory.Combat)
            return null;
        s.StressedUntil = Math.Max(s.StressedUntil, now + (category == CallCategory.Combat ? StressAfterCombat : StressAfterWarning));
        return Stressed;
    }

    private static string CarriedMood(Transmission t, State s, float now) =>
        now < s.StressedUntil ? Stressed
        : now < s.HappyUntil && t.Role == CallRole.Reply ? Happy
        : null;
}
