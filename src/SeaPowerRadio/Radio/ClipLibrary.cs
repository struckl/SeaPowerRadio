using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeaPowerRadio.Voices;
using UnityEngine;
using UnityEngine.Networking;

namespace SeaPowerRadio.Radio;

/// <summary>
/// Loads voice clips (&lt;pack&gt;/&lt;persona&gt;/&lt;key&gt;.mp3, voice = packId/personaId) on demand and keeps recently used ones decoded. The
/// library is ~27k clips; a mission only ever touches a few hundred, so nothing is preloaded.
/// </summary>
internal static class ClipLibrary
{
    private sealed class Entry
    {
        public AudioClip Clip;
        public long Bytes;
        public long LastUsed;
    }

    private const long CacheLimitBytes = 96L * 1024 * 1024;
    private static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>();
    private static readonly Dictionary<string, bool> Known = new Dictionary<string, bool>();   // file checks, per session
    private static long cachedBytes;
    private static long tick;

    /// <summary>File of a clip. <paramref name="voice"/> is <c>packId/personaId</c>; null when the pack is not installed.</summary>
    public static string PathOf(string voice, string key) =>
        VoicePacks.OfVoice(voice)?.PathOf(VoicePacks.PersonaOf(voice), key);

    public static bool Exists(string voice, string key)
    {
        string path = PathOf(voice, key);
        if (path == null) return false;
        string id = voice + "/" + key;
        return Known.TryGetValue(id, out bool yes) ? yes : Known[id] = File.Exists(path);
    }

    /// <summary>Load every clip of a transmission; missing clips come back null and are skipped by the caller.</summary>
    public static IEnumerator LoadAll(string voice, IList<string> keys, AudioClip[] result)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i] == null)
                continue;
            int index = i;
            yield return Load(voice, keys[i], clip => result[index] = clip);
        }
    }

    public static IEnumerator Load(string voice, string key, Action<AudioClip> done)
    {
        string id = voice + "/" + key;
        if (Cache.TryGetValue(id, out Entry hit) && hit.Clip != null)
        {
            hit.LastUsed = ++tick;
            done(hit.Clip);
            yield break;
        }

        string path = PathOf(voice, key);
        if (path == null || !File.Exists(path))
        {
            ModLog.Warn($"Missing voice clip {id}");
            done(null);
            yield break;
        }

        using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.MPEG);
        ((DownloadHandlerAudioClip)request.downloadHandler).compressed = false; // decoded: RadioFx reads samples
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            ModLog.Warn($"Could not load {id}: {request.error}");
            done(null);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        if (clip == null)
        {
            done(null);
            yield break;
        }
        clip.name = id;
        var entry = new Entry { Clip = clip, Bytes = (long)clip.samples * clip.channels * 4, LastUsed = ++tick };
        Cache[id] = entry;
        cachedBytes += entry.Bytes;
        Trim();
        done(clip);
    }

    private static void Trim()
    {
        while (cachedBytes > CacheLimitBytes && Cache.Count > 1)
        {
            var oldest = Cache.OrderBy(p => p.Value.LastUsed).First();
            Cache.Remove(oldest.Key);
            cachedBytes -= oldest.Value.Bytes;
            if (oldest.Value.Clip != null)
                UnityEngine.Object.Destroy(oldest.Value.Clip);
        }
    }

    public static void Clear()
    {
        foreach (Entry e in Cache.Values)
            if (e.Clip != null)
                UnityEngine.Object.Destroy(e.Clip);
        Cache.Clear();
        cachedBytes = 0;
    }
}
