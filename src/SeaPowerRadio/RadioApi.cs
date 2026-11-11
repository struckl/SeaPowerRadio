using System;
using SeaPower;
using SeaPowerRadio.Radio;
using SeaPowerRadio.Roster;

namespace SeaPowerRadio;

/// <summary>
/// Other mods speak over the radio net through <c>rats.radio.say</c> (see RatsBus and <c>docs/rats-bus.md</c>): a unit
/// and the phrase keys of the catalog, each with a number or -1. R.A.T.S. Command Tools uses it for why-not, contact,
/// fuel and SAR reports; a mod with its own lines adds them as a line pack (<c>linepack.ini</c>, see VoicePacks).
/// <list type="bullet">
/// <item>The unit is resolved in the roster: its own voice, mood and callsign. Null = the task force's command voice.</item>
/// <item>The messages raised for that unit in this same frame are pinned to the call until it was spoken (AlertLink), so
/// a mod raises its message first and calls this right after.</item>
/// <item>False (nothing queued) when no mission is running, the unit has no voice (game voice country), or the voice has no
/// clip for the phrase; the caller's message then stands alone.</item>
/// </list>
/// Called from other assemblies: it never throws.
/// </summary>
internal static class RadioApi
{
    public static RadioPlayer Player;

    public static bool Say(object unit, string[] phrases, int[] numbers)
    {
        try
        {
            if (Player == null || phrases == null || phrases.Length == 0 || !Settings.Enabled.Value)
                return false;
            Roster.Roster roster = RosterService.Current;
            if (roster == null)
                return false;
            ObjectBase obj = unit as ObjectBase;
            RosterElement speaker = obj != null ? roster.Resolve(obj) : roster.CommandVoice();
            if (speaker?.Voice == null)
                return false;

            Transmission t = TransmissionBuilder.Start(speaker);
            bool any = false;
            for (int i = 0; i < phrases.Length; i++)
            {
                int n = numbers != null && i < numbers.Length ? numbers[i] : -1;
                any |= TransmissionBuilder.Append(t, phrases[i], n >= 0 ? n : (int?)null);
            }
            if (!any)
                return false;
            t.Alerts.AddRange(AlertLink.TakeFor(obj ?? speaker.Unit));
            Player.Submit(t);
            return true;
        }
        catch (Exception ex)
        {
            ModLog.Debug($"[api] say {(phrases != null ? string.Join("+", phrases) : "?")}: {ex.Message}");
            return false;
        }
    }
}
