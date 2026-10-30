using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SeaPower;
using SeaPowerRadio.Roster;
using UnityEngine;
using UnityEngine.Audio;

namespace SeaPowerRadio.Radio;

/// <summary>
/// The radio net: highest priority first, one voice at a time - or, with "Overlapping voices", up to a few stations
/// talking over each other like a busy net.
/// <list type="bullet">
/// <item>Urgent calls (priority ≤ 2: missile inbound, we're hit, bingo) cut into a routine call (when every channel is busy).</item>
/// <item>Replies to the player's orders go before routine reports – and are mixed louder and cleaner.</item>
/// <item>Calls go stale: replies after 6 s, contact reports after 8 s, the rest after 15 s – a late "copy" is worse than none.</item>
/// <item>The same phrase from several units at once (a whole flight acknowledging) is said at most twice.</item>
/// <item>A short, varying gap between calls, like a real net. Overlapping: a new call starts 0.6–1.4 s after the
/// last start, on its own channel (panned a little left / right), never for a unit that is already talking.</item>
/// <item>Every call has a category (<see cref="CallCategory"/>) with its own volume; the master volume and the
/// category volume of the options page apply to the playing call at once, the noise level from the next call.</item>
/// </list>
/// Plays on the game's "Voice" mixer group, so the game's voice volume slider still applies.
/// </summary>
internal sealed class RadioPlayer
{
    private const int MaxQueue = 24;
    private const int MaxChannels = 4;
    private const float SamePhraseWindow = 2.5f;
    private static readonly float[] Pans = { 0f, -0.35f, 0.35f, 0f };

    /// <summary>One voice on the net: its own audio source and the call it is playing.</summary>
    private sealed class Channel
    {
        public int Index;
        public AudioSource Source;
        public Transmission Current;
        public AudioClip Clip;
        public bool Preparing;
        public int Version;
        public float Boost = 1f;            // gain baked into the clip above 1 (an audio source cannot go louder than 1)
        public CallCategory Category;
        public float StartedAt;

        public bool Busy => Current != null && (Preparing || Source.isPlaying);
    }

    private readonly MonoBehaviour host;
    private readonly List<Transmission> queue = new List<Transmission>();
    private readonly List<Channel> channels = new List<Channel>();
    private RosterElement lastSpeaker;      // who was on the net last: the callsign is only said on a change
    private float nextAllowed;
    private int seed;

    public RadioPlayer(MonoBehaviour host) => this.host = host;

    public void Submit(Transmission t)
    {
        if (t == null || t.Voice == null)
            return;
        float now = Time.unscaledTime;
        int same = queue.Count(q => q.Phrase == t.Phrase && now - q.SubmittedAt < SamePhraseWindow)
                   + channels.Count(c => c.Current != null && c.Current.Phrase == t.Phrase && now - c.Current.SubmittedAt < SamePhraseWindow);
        if (same >= 2)
        {
            ModLog.Debug($"[radio] drop duplicate {t.Text}");
            AlertLink.Release(t);
            return;
        }
        queue.Add(t);
        AlertLink.Hold(t);
        if (queue.Count > MaxQueue)
        {
            Transmission dropped = queue.OrderByDescending(q => q.Rank).ThenBy(q => q.SubmittedAt).First();
            queue.Remove(dropped);
            AlertLink.Release(dropped);
        }
        if (Settings.LogRadio.Value)
            ModLog.Info($"[radio] queued p{t.Priority} {t.Voice}: {t.Text}");
    }

    public void Clear()
    {
        foreach (Channel c in channels)
            Stop(c);
        lastSpeaker = null;
        foreach (Transmission q in queue)
            AlertLink.Release(q);
        queue.Clear();
    }

    public void Tick()
    {
        EnsureChannel(0);
        float now = Time.unscaledTime;
        foreach (Transmission stale in queue.Where(q => !q.Urgent && now - q.SubmittedAt > (q.Role == CallRole.Reply ? 6f : q.Priority >= 5 ? 8f : 15f)).ToList())
        {
            queue.Remove(stale);
            AlertLink.Release(stale);
        }

        int active = 0;
        foreach (Channel c in channels)
        {
            if (c.Current != null && !c.Busy)
                Finish(c);
            if (c.Current != null)
            {
                active++;
                ApplyVolume(c);
            }
        }

        int max = Settings.OverlapVoices.Value ? Mathf.Clamp(Settings.MaxOverlap.Value, 2, MaxChannels) : 1;
        bool urgentWaiting = queue.Any(q => q.Urgent);
        if (active >= max && urgentWaiting)
        {
            // An urgent call cuts into a routine one.
            Channel victim = channels.Where(c => c.Current != null && !c.Current.Urgent).OrderBy(c => c.StartedAt).FirstOrDefault();
            if (victim == null)
                return;
            Stop(victim);
            active--;
        }
        if (active >= max || queue.Count == 0 || (now < nextAllowed && !urgentWaiting))
            return;

        Transmission next = null;
        foreach (Transmission t in queue.OrderBy(q => q.Rank).ThenBy(q => q.SubmittedAt))
        {
            if (active == 0 || !SpeakerBusy(t))
            {
                next = t;
                break;
            }
        }
        if (next == null)
            return;

        Channel channel = FreeChannel();
        if (channel == null)
            return;
        queue.Remove(next);
        Abbreviate(next);
        lastSpeaker = next.Speaker;
        channel.Current = next;
        channel.Preparing = true;
        channel.StartedAt = now;
        channel.Category = CallCategories.Of(next.Phrase, next.Role);
        if (max > 1)
            nextAllowed = now + Random.Range(0.6f, 1.4f);
        host.StartCoroutine(Prepare(channel, next, ++channel.Version));
    }

    /// <summary>True if the unit (or roster element) of this call is already talking on another channel.</summary>
    private bool SpeakerBusy(Transmission t)
    {
        foreach (Channel c in channels)
        {
            Transmission o = c.Current;
            if (o == null || o.Speaker == null || t.Speaker == null)
                continue;
            if (o.Speaker == t.Speaker)
                return true;
            ObjectBase a = o.Speaker.Unit, b = t.Speaker.Unit;
            if (a != null && b != null && a == b)
                return true;
        }
        return false;
    }

    private Channel FreeChannel()
    {
        foreach (Channel c in channels)
            if (c.Current == null)
                return c;
        if (channels.Count < MaxChannels)
        {
            EnsureChannel(channels.Count);
            return channels[channels.Count - 1];
        }
        return null;
    }

    /// <summary>Master volume x category volume; above 1 the clip carries the extra gain (<see cref="Channel.Boost"/>).</summary>
    private static float TotalGain(Channel c) => Settings.Volume.Value * CallCategories.Volume(c.Category);

    private static void ApplyVolume(Channel c)
    {
        if (c.Source != null)
            c.Source.volume = Mathf.Clamp01(TotalGain(c) / c.Boost);
    }

    private IEnumerator Prepare(Channel ch, Transmission t, int version)
    {
        var clips = new AudioClip[t.Clips.Count];
        yield return ClipLibrary.LoadAll(t.Voice, t.Clips, clips);
        if (version != ch.Version || ch.Current != t)
            yield break;

        var parts = new List<AudioClip>();
        var pauses = new List<float>();    // most silence where each part joins the one before
        string previous = null;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null && t.Clips[i] != null)
                continue;
            parts.Add(clips[i]);
            pauses.Add(previous != null && t.Clips[i] != null ? TransmissionBuilder.MaxPause(previous, t.Clips[i]) : float.MaxValue);
            previous = t.Clips[i];
        }
        if (!parts.Any(c => c != null))
        {
            ch.Preparing = false;
            AlertLink.Release(t);   // nothing to say after all: its messages fade normally instead of staying pinned
            ch.Current = null;
            yield break;
        }

        // The mix hierarchy: replies to the player clean and in front, alerts just as loud, reports behind them.
        // On top: the master radio volume and the volume of the call's category (options page).
        ch.Boost = Mathf.Max(1f, TotalGain(ch));
        float level = (t.Role == CallRole.Report ? Settings.ReportVolume.Value : 1f) * ch.Boost;
        float noise = Settings.RadioNoise.Value * (t.Role == CallRole.Reply ? 0.6f : 1f);
        ch.Clip = RadioFx.Build(parts, pauses, t.Profile, unchecked(++seed * 7919), noise, level);
        ch.Source.clip = ch.Clip;
        ch.Source.panStereo = Settings.OverlapVoices.Value ? Pans[ch.Index] : 0f;
        ApplyVolume(ch);
        ch.Source.Play();
        AlertLink.StartSpeaking(t);
        ch.Preparing = false;
        if (Settings.LogRadio.Value)
            ModLog.Info($"[radio] >> {t.Voice}: {t.Text} [{ch.Category}, channel {ch.Index + 1}]");
    }

    /// <summary>
    /// The callsign only when someone else is talking than before: the same unit again just goes on ("fox three"),
    /// a wingman of the same flight gives its number only ("two, fox three") – like a real net once everyone
    /// knows who is up. The message bottom right still names the unit.
    /// </summary>
    private void Abbreviate(Transmission t)
    {
        if (Settings.CallsignEveryCall.Value || lastSpeaker == null || t.Speaker == null || t.CallsignClips == 0)
            return;
        ObjectBase unit = t.Speaker.Unit, last = lastSpeaker.Unit;
        bool sameUnit = t.Speaker == lastSpeaker || (unit != null && unit == last);
        if (sameUnit)
        {
            t.Clips.RemoveRange(0, t.CallsignClips);
            t.CallsignClips = 0;
            t.Text = "(same unit)" + t.Text.Substring(t.Text.IndexOf(',') + 1);
            return;
        }
        if (unit != null && last != null
            && Names.TryParseAircraft(unit.Name.Value, out string word, out int number, out int position)
            && Names.TryParseAircraft(last.Name.Value, out string lastWord, out int lastNumber, out _)
            && position > 0 && number == lastNumber && string.Equals(word, lastWord, System.StringComparison.OrdinalIgnoreCase)
            && ClipLibrary.Exists(t.Voice, $"cn_{position}"))
        {
            t.Clips.RemoveRange(0, t.CallsignClips);
            t.Clips.Insert(0, $"cn_{position}");
            t.CallsignClips = 1;
            t.Text = $"({position})" + t.Text.Substring(t.Text.IndexOf(',') + 1);
        }
    }

    private void Finish(Channel c)
    {
        if (!channels.Any(o => o != c && o.Current != null))
            nextAllowed = Mathf.Max(nextAllowed, Time.unscaledTime + Random.Range(0.35f, 0.9f));
        AlertLink.Release(c.Current);
        ReleaseClip(c);
        c.Current = null;
    }

    private void Stop(Channel c)
    {
        c.Version++;
        c.Preparing = false;
        if (c.Current != null)
            AlertLink.Release(c.Current);
        if (c.Source != null)
            c.Source.Stop();
        ReleaseClip(c);
        c.Current = null;
    }

    private static void ReleaseClip(Channel c)
    {
        if (c.Source != null)
            c.Source.clip = null;
        if (c.Clip != null)
            Object.Destroy(c.Clip);
        c.Clip = null;
    }

    /// <summary>Makes sure channel <paramref name="index"/> exists (one audio source each).</summary>
    private void EnsureChannel(int index)
    {
        while (channels.Count <= index)
        {
            var source = host.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            AudioMixer mixer = Resources.Load<AudioMixer>("audio/Master");
            AudioMixerGroup[] groups = mixer != null ? mixer.FindMatchingGroups("Voice") : null;
            if (groups != null && groups.Length > 0)
                source.outputAudioMixerGroup = groups[0];
            else if (channels.Count == 0)
                ModLog.Warn("Voice mixer group not found; radio plays on the master bus.");
            channels.Add(new Channel { Index = channels.Count, Source = source });
        }
    }
}
