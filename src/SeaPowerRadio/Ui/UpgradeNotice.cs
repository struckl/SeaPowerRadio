using System;
using System.Collections.Generic;
using SeaPowerRadio.Voices;
using Steamworks;
using UnityEngine;

namespace SeaPowerRadio.Ui;

/// <summary>
/// The 2.0 note for players who had Radio before (Settings.ShowUpgradeNotice, set once by the config migration): a small
/// window in the main menu that says what moved and offers to subscribe what is missing. New players never see it.
/// <list type="bullet">
/// <item>R.A.T.S. Command Tools: order queue, clickable messages, unit roles, RTB highlight, why-not, contact, fuel and
/// SAR reports. Missing = <c>rats.commandtools.version</c> is not set.</item>
/// <item>R.A.T.S. Radio English: the 43 voices. Missing = no voice pack <c>nato-en</c> was found.</item>
/// </list>
/// "Subscribe" asks Steam through the game's own Steamworks (both items are Required Items of Radio, so the game's Mod
/// menu "Sync dependencies" does the same); if that fails, the item's Workshop page opens. Subscribed items arrive with
/// the next start of the game. "Later" hides it for this session, "Don't show again" for good.
/// </summary>
internal sealed class UpgradeNotice : MonoBehaviour
{
    // Workshop items. 0 = not published yet: the window then names the item without a button.
    public const ulong CommandToolsItem = 0;
    public const ulong EnglishItem = 0;

    private const float ShowAfterSeconds = 4f;     // in the main menu, once the game has settled
    private bool closed;
    private float menuSince = -1f;
    private Rect window = new Rect(0, 0, 560, 10);
    private GUIStyle text;
    private readonly HashSet<ulong> asked = new HashSet<ulong>();

    private void OnGUI()
    {
        if (closed || Settings.ShowUpgradeNotice == null || !Settings.ShowUpgradeNotice.Value)
            return;
        if (Roster.RosterService.Current != null)
        {
            menuSince = -1f;                        // a mission: never over the game
            return;
        }
        if (menuSince < 0f)
            menuSince = Time.realtimeSinceStartup;
        if (Time.realtimeSinceStartup - menuSince < ShowAfterSeconds)
            return;

        float scale = Mathf.Max(1f, Screen.height / 1080f);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUI.depth = -10000;
        text ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
        bool tools = RatsBus.ToolsRunning;
        bool english = VoicePacks.ById(VoicePacks.NatoId) != null;
        window.height = 250f + (tools ? 0f : 34f) + (english ? 0f : 34f);
        window.x = (Screen.width / scale - window.width) / 2f;
        window.y = (Screen.height / scale - window.height) / 2f;
        window = GUI.Window(0x5EAA, window, id => Draw(tools, english), "R.A.T.S. Radio 2.0 - what moved");
        GUI.matrix = old;
    }

    private void Draw(bool tools, bool english)
    {
        GUILayout.Space(4);
        GUILayout.Label(
            "Radio is now the radio itself. Two things that came with it are Workshop items of their own:\n\n" +
            "<b>R.A.T.S. Command Tools</b> - order queue (F8), clickable units and tracks in messages, unit roles, RTB " +
            "highlight, why-not, contact, fuel and search-and-rescue reports. Radio still speaks them.\n" +
            "<b>R.A.T.S. Radio English</b> - the 43 voices. More languages and accents will be items like it.\n\n" +
            "Your settings carry over.", text);
        GUILayout.Space(6);
        Line("R.A.T.S. Command Tools", tools, CommandToolsItem);
        Line("R.A.T.S. Radio English", english, EnglishItem);
        if (!tools || !english)
            GUILayout.Label("Subscribed items load at the next start of the game. The game's Mod menu can also fetch them: " +
                            "\"Sync dependencies\".", text);
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Later", GUILayout.Width(110)))
            closed = true;
        if (GUILayout.Button("Don't show again", GUILayout.Width(150)))
        {
            closed = true;
            Settings.ShowUpgradeNotice.Value = false;
            Settings.SaveSoon();
        }
        GUILayout.EndHorizontal();
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    private void Line(string name, bool present, ulong item)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(present ? $"✓ {name}: installed" : $"✗ {name}: not installed", text, GUILayout.Width(360));
        if (!present && item != 0)
        {
            if (asked.Contains(item))
                GUILayout.Label("subscribed - restart the game", text);
            else if (GUILayout.Button("Subscribe", GUILayout.Width(120)))
                Subscribe(item);
        }
        GUILayout.EndHorizontal();
    }

    private void Subscribe(ulong item)
    {
        try
        {
            SteamUGC.SubscribeItem(new PublishedFileId_t(item));
            asked.Add(item);
            ModLog.Info($"2.0 notice: asked Steam to subscribe {item}.");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"2.0 notice: Steam subscribe failed ({ex.Message}); opening the Workshop page.");
            Application.OpenURL("steam://url/CommunityFilePage/" + item);
        }
    }
}
