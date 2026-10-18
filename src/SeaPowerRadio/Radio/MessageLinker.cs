using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerRadio.Radio;

/// <summary>
/// Makes every unit and track a message mentions clickable – bottom right and in the message log. The game links
/// at most the unit that raised a message and one track; whatever it wrote as plain text ("Track 23", a ship or
/// squadron name, "Rebasing to Kitty Hawk") stays dead text. After the game has built a message, its plain text is
/// searched for track numbers of the player's plot and for the names of own units and identified contacts, and
/// each mention becomes a link that moves the camera there, like the game's own links.
/// </summary>
internal static class MessageLinker
{
    // "Track 23", "track #23", "Tracks 12, 13 and 14" (a list only after the plural: "Track 23, 300 kts" is one track)
    private static readonly Regex Tracks = new Regex(@"\btrack\s+#?\d+|\btracks\s+#?\d+(?:\s*(?:,|and|&)\s*#?\d+)*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Digits = new Regex(@"\d+");

    private static readonly HashSet<ObjectBase.ObjectType> NamedTypes = new HashSet<ObjectBase.ObjectType>
    {
        ObjectBase.ObjectType.Vessel, ObjectBase.ObjectType.Submarine, ObjectBase.ObjectType.Aircraft,
        ObjectBase.ObjectType.Helicopter, ObjectBase.ObjectType.LandUnit,
    };

    public static bool Enabled => Settings.LinkMentions != null && Settings.LinkMentions.Value;

    public static void Link(Alert alert)
    {
        if (!Enabled || !(alert?.AlertContent is Noesis.TextBlock block))
            return;
        try
        {
            Index index = Index.Build();
            if (index != null)
                LinkInlines(block.Inlines, index);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"[links] {ex.Message}");
        }
    }

    /// <summary>What can be linked right now: track numbers and names, from the player's plot.</summary>
    private sealed class Index
    {
        public readonly Dictionary<int, ObjectBase> ByTrack = new Dictionary<int, ObjectBase>();
        public readonly List<(string name, ObjectBase obj)> ByName = new List<(string, ObjectBase)>();

        public static Index Build()
        {
            Taskforce player = Globals._playerTaskforce;
            var vehicles = player?.PlottingTable?.LocalVehicles;
            if (vehicles == null)
                return null;
            var index = new Index();
            var names = new Dictionary<string, (ObjectBase obj, bool own)>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<ObjectBase, Vehicle> kv in vehicles)
            {
                ObjectBase obj = kv.Key;
                Vehicle v = kv.Value;
                if (obj == null || v == null)
                    continue;
                if (!index.ByTrack.ContainsKey(v.Id))
                    index.ByTrack[v.Id] = obj;

                bool own = obj._taskforce == player;
                if (!NamedTypes.Contains(obj._type) || !(own || v.Identified.Value))
                    continue;
                string name = obj.Name.Value?.Trim();
                if (string.IsNullOrEmpty(name) || name.Length < 3 || name.All(char.IsDigit))
                    continue;
                // Two objects with one name: an own unit wins over a contact, otherwise the first one stays.
                if (!names.TryGetValue(name, out var known) || (own && !known.own))
                    names[name] = (obj, own);
            }
            index.ByName.AddRange(names.OrderByDescending(n => n.Key.Length).Select(n => (n.Key, n.Value.obj)));
            return index;
        }
    }

    private static void LinkInlines(Noesis.InlineCollection inlines, Index index)
    {
        for (int i = 0; i < inlines.Count; i++)
        {
            Noesis.Inline inline = inlines[i];
            if (inline is Noesis.Hyperlink)
                continue;                          // already a link (the game's ${Name} / ${TrackId})
            if (inline is Noesis.Span span)
            {
                LinkInlines(span.Inlines, index);  // colour / bold spans
                continue;
            }
            if (!(inline is Noesis.Run run) || string.IsNullOrEmpty(run.Text))
                continue;

            List<(int start, int length, ObjectBase obj)> mentions = Find(run.Text, index);
            if (mentions.Count == 0)
                continue;
            List<Noesis.Inline> parts = Split(run.Text, mentions);
            inlines.Remove(run);
            for (int p = 0; p < parts.Count; p++)
                inlines.Insert(i + p, parts[p]);
            i += parts.Count - 1;
        }
    }

    private static List<(int start, int length, ObjectBase obj)> Find(string text, Index index)
    {
        var found = new List<(int start, int length, ObjectBase obj)>();
        foreach (Match list in Tracks.Matches(text))
        {
            foreach (Match number in Digits.Matches(list.Value))
            {
                if (int.TryParse(number.Value, out int id) && index.ByTrack.TryGetValue(id, out ObjectBase obj))
                    found.Add((list.Index + number.Index, number.Length, obj));
            }
        }
        foreach ((string name, ObjectBase obj) in index.ByName)
        {
            for (int at = text.IndexOf(name, StringComparison.OrdinalIgnoreCase); at >= 0;
                 at = text.IndexOf(name, at + name.Length, StringComparison.OrdinalIgnoreCase))
            {
                if (!WordAt(text, at, name.Length))
                    continue;
                if (found.Any(f => at < f.start + f.length && f.start < at + name.Length))
                    continue;                      // inside a longer name or a track number
                found.Add((at, name.Length, obj));
            }
        }
        found.Sort((a, b) => a.start.CompareTo(b.start));
        return found;
    }

    private static bool WordAt(string text, int start, int length)
    {
        int end = start + length;
        return (start == 0 || !char.IsLetterOrDigit(text[start - 1]))
               && (end >= text.Length || !char.IsLetterOrDigit(text[end]));
    }

    private static List<Noesis.Inline> Split(string text, List<(int start, int length, ObjectBase obj)> mentions)
    {
        var parts = new List<Noesis.Inline>();
        int at = 0;
        foreach ((int start, int length, ObjectBase obj) in mentions)
        {
            if (start > at)
                parts.Add(new Noesis.Run(text.Substring(at, start - at)));
            parts.Add(LinkTo(text.Substring(start, length), obj));
            at = start + length;
        }
        if (at < text.Length)
            parts.Add(new Noesis.Run(text.Substring(at)));
        return parts;
    }

    private static Noesis.Hyperlink LinkTo(string text, ObjectBase target)
    {
        return new Noesis.Hyperlink(new Noesis.Run(text))
        {
            Command = new DelegateCommand(_ => Jump(target)),
        };
    }

    /// <summary>What the game's own links do: move the camera to the object.</summary>
    private static void Jump(ObjectBase target)
    {
        if (target == null)
            return;                                // destroyed since
        try
        {
            Singleton<RenderPosition>.Instance.switchToObject(target, performSmoothMove: false);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"[links] jump failed: {ex.Message}");
        }
    }
}
