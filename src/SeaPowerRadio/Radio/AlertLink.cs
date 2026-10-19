using System;
using System.Collections.Generic;
using System.Linq;
using SeaPower;
using SeapowerUI.ViewModels;
using UnityEngine;

namespace SeaPowerRadio.Radio;

/// <summary>
/// Ties radio calls to the messages bottom right (MainGameViewModel.TopAlerts):
/// <list type="bullet">
/// <item>The game raises a message and the matching voice line for the same unit in the same frame; the mod's own
/// why-not messages do the same. A call takes the messages raised for its speaker in the frame it was queued.</item>
/// <item>While the call waits or plays, its messages stay fully visible (their fade timer is held), and a new
/// message pushes out an older one that isn't waiting instead of a waiting one – the game keeps only three.</item>
/// <item>The message being spoken right now gets a thin amber bar in front of it.</item>
/// </list>
/// </summary>
internal static class AlertLink
{
    private const int TopAlertsKept = 3;   // the game's own limit
    private const int TopAlertsMax = 6;    // never grow beyond this, even if everything is waiting

    private static readonly List<(Alert alert, int frame)> Recent = new List<(Alert, int)>();
    private static readonly HashSet<Alert> Waiting = new HashSet<Alert>();
    private static readonly HashSet<Alert> Speaking = new HashSet<Alert>();
    private static readonly Dictionary<Alert, Noesis.Inline> Markers = new Dictionary<Alert, Noesis.Inline>();
    private static Noesis.Brush highlight;

    public static bool Enabled => Settings.LinkAlerts != null && Settings.LinkAlerts.Value;

    /// <summary>Replaces the game's TopAlerts setter: same behaviour, except waiting messages are not evicted.</summary>
    public static void Show(MainGameViewModel vm, Alert value)
    {
        var top = vm.TopAlerts;
        top.Add(value);
        value.AlertTextOpacity = 100f;
        value.alertTextSetTime = Time.time;
        Recent.Add((value, Time.frameCount));
        Recent.RemoveAll(r => Time.frameCount - r.frame > 2);
        while (top.Count > TopAlertsKept)
        {
            int index = -1;
            for (int i = 0; i < top.Count - 1; i++)  // never the one just added
                if (!Waiting.Contains(top[i]) && !Speaking.Contains(top[i])) { index = i; break; }
            if (index < 0)
            {
                if (top.Count <= TopAlertsMax) break;
                index = 0;
            }
            top.RemoveAt(index);
        }
    }

    /// <summary>The messages raised this frame for this unit (or without a unit, for task force reports).</summary>
    public static List<Alert> TakeFor(ObjectBase unit)
    {
        var mine = Recent.Where(r => r.frame == Time.frameCount
                                     && (unit == null ? r.alert.RaisingObject == null : r.alert.RaisingObject == unit))
                         .Select(r => r.alert).ToList();
        Recent.RemoveAll(r => mine.Contains(r.alert));
        return mine;
    }

    public static void Hold(Transmission t)
    {
        foreach (Alert a in t.Alerts)
            Waiting.Add(a);
    }

    public static void StartSpeaking(Transmission t)
    {
        foreach (Alert a in t.Alerts)
        {
            Waiting.Remove(a);
            Speaking.Add(a);
            SetHighlight(a, true);
        }
    }

    /// <summary>Played, cut off or dropped: un-highlight and let the message fade the normal way from now on.</summary>
    public static void Release(Transmission t)
    {
        if (t == null) return;
        foreach (Alert a in t.Alerts)
        {
            Waiting.Remove(a);
            Speaking.Remove(a);
            SetHighlight(a, false);
            a.alertTextSetTime = Time.time;
        }
    }

    /// <summary>Every frame: hold the fade of messages whose call hasn't finished.</summary>
    public static void Tick()
    {
        if (Waiting.Count == 0 && Speaking.Count == 0) return;
        float now = Time.time;
        foreach (Alert a in Waiting.Concat(Speaking))
        {
            a.alertTextSetTime = now;
            if (a.AlertTextOpacity < 100f) a.AlertTextOpacity = 100f;
        }
    }

    public static void Clear()
    {
        Recent.Clear();
        Waiting.Clear();
        Speaking.Clear();
        Markers.Clear();
    }

    /// <summary>A thin amber bar at the start of the message while it is being spoken.</summary>
    private static void SetHighlight(Alert a, bool on)
    {
        try
        {
            if (!(a.AlertContent is Noesis.TextBlock text))
                return;
            if (on && !Markers.ContainsKey(a))
            {
                highlight ??= new Noesis.SolidColorBrush(Noesis.Color.FromArgb(0xFF, 0xF0, 0xB0, 0x28));
                var bar = new Noesis.Border
                {
                    Width = 3f,
                    Height = Math.Max(10f, text.FontSize),
                    Background = highlight,
                    Margin = new Noesis.Thickness(0f, 0f, 5f, -2f),
                };
                var marker = new Noesis.InlineUIContainer(bar);
                text.Inlines.Insert(0, marker);
                Markers[a] = marker;
            }
            else if (!on && Markers.TryGetValue(a, out Noesis.Inline marker))
            {
                text.Inlines.Remove(marker);
                Markers.Remove(a);
            }
        }
        catch (Exception ex)
        {
            ModLog.Debug($"[alerts] highlight failed: {ex.Message}");
        }
    }
}
