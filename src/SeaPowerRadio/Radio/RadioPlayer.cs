using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace SeaPowerRadio.Radio;

/// <summary>
/// The radio net: one voice at a time, highest priority first.
/// <list type="bullet">
/// <item>Urgent calls (priority ≤ 2: missile inbound, we're hit, bingo) cut into a routine call.</item>
/// <item>Replies to the player's orders go before routine reports.</item>
/// <item>Calls go stale: replies after 6 s, contact reports after 8 s, the rest after 15 s – a late "copy" is worse than none.</item>
/// <item>The same phrase from several units at once (a whole flight acknowledging) is said at most twice.</item>
/// <item>A short, varying gap between calls, like a real net.</item>
/// </list>
/// Plays on the game's "Voice" mixer group, so the game's voice volume slider still applies.
/// </summary>
internal sealed class RadioPlayer
{
    private const int MaxQueue = 24;
    private const float SamePhraseWindow = 2.5f;

    private readonly MonoBehaviour host;
    private readonly List<Transmission> queue = new List<Transmission>();
    private AudioSource source;
    private Transmission current;
    private AudioClip currentClip;
    private bool preparing;
    private int prepareVersion;
    private float nextAllowed;
    private int seed;

    public RadioPlayer(MonoBehaviour host) => this.host = host;

    public void Submit(Transmission t)
    {
        if (t == null || t.Voice == null)
            return;
        float now = Time.unscaledTime;
        int same = queue.Count(q => q.Phrase == t.Phrase && now - q.SubmittedAt < SamePhraseWindow)
                   + (current != null && current.Phrase == t.Phrase && now - current.SubmittedAt < SamePhraseWindow ? 1 : 0);
        if (same >= 2)
        {
            ModLog.Debug($"[radio] drop duplicate {t.Text}");
            return;
        }
        queue.Add(t);
        if (queue.Count > MaxQueue)
            queue.Remove(queue.OrderByDescending(q => q.Rank).ThenBy(q => q.SubmittedAt).First());
        if (Settings.LogRadio.Value)
            ModLog.Info($"[radio] queued p{t.Priority} {t.Voice}: {t.Text}");
    }

    public void Clear()
    {
        Stop();
        queue.Clear();
    }

    public void Tick()
    {
        if (!EnsureSource())
            return;
        float now = Time.unscaledTime;
        queue.RemoveAll(q => !q.Urgent && now - q.SubmittedAt > (q.Response ? 6f : q.Priority >= 5 ? 8f : 15f));

        bool playing = current != null && (preparing || source.isPlaying);
        if (playing)
        {
            // An urgent call cuts into a routine one.
            if (!current.Urgent && queue.Any(q => q.Urgent))
                Stop();
            else
                return;
        }
        else if (current != null)
        {
            Finish();
        }

        if (queue.Count == 0 || (now < nextAllowed && !queue.Any(q => q.Urgent)))
            return;

        Transmission next = queue.OrderBy(q => q.Rank).ThenBy(q => q.SubmittedAt).First();
        queue.Remove(next);
        current = next;
        preparing = true;
        host.StartCoroutine(Prepare(next, ++prepareVersion));
    }

    private IEnumerator Prepare(Transmission t, int version)
    {
        var clips = new AudioClip[t.Clips.Count];
        yield return ClipLibrary.LoadAll(t.Voice, t.Clips, clips);
        if (version != prepareVersion || current != t)
            yield break;

        var parts = new List<AudioClip>();
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null || t.Clips[i] == null)
                parts.Add(clips[i]);
        if (!parts.Any(c => c != null))
        {
            preparing = false;
            current = null;
            yield break;
        }

        currentClip = RadioFx.Build(parts, t.Profile, unchecked(++seed * 7919), Settings.RadioNoise.Value);
        source.clip = currentClip;
        source.volume = Settings.Volume.Value;
        source.Play();
        preparing = false;
        if (Settings.LogRadio.Value)
            ModLog.Info($"[radio] >> {t.Voice}: {t.Text}");
    }

    private void Finish()
    {
        nextAllowed = Time.unscaledTime + Random.Range(0.35f, 0.9f);
        ReleaseClip();
        current = null;
    }

    private void Stop()
    {
        prepareVersion++;
        preparing = false;
        if (source != null)
            source.Stop();
        ReleaseClip();
        current = null;
    }

    private void ReleaseClip()
    {
        if (source != null)
            source.clip = null;
        if (currentClip != null)
            Object.Destroy(currentClip);
        currentClip = null;
    }

    private bool EnsureSource()
    {
        if (source != null)
            return true;
        source = host.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        AudioMixer mixer = Resources.Load<AudioMixer>("audio/Master");
        AudioMixerGroup[] groups = mixer != null ? mixer.FindMatchingGroups("Voice") : null;
        if (groups != null && groups.Length > 0)
            source.outputAudioMixerGroup = groups[0];
        else
            ModLog.Warn("Voice mixer group not found; radio plays on the master bus.");
        return true;
    }
}
