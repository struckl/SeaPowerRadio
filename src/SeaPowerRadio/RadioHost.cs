using System;
using SeaPowerRadio.Core;
using SeaPowerRadio.Radio;
using UnityEngine;

namespace SeaPowerRadio;

/// <summary>
/// Persistent MonoBehaviour the mod hangs its per-frame work on: the radio net. Lives on
/// a DontDestroyOnLoad object created at load, so it survives scene changes between menu and missions.
/// </summary>
internal sealed class RadioHost : MonoBehaviour
{
    private RadioPlayer player;

    private void Awake()
    {
        player = new RadioPlayer(this);
        StockVoiceRouter.Player = player;
        RadioApi.Player = player;
        gameObject.AddComponent<Ui.UpgradeNotice>();
        EventBus.MissionStarted += OnMissionStarted;
        Settings.FemaleVoices.SettingChanged += OnFemaleVoicesChanged;
        EventBus.MissionEnded += OnMissionEnded;
    }

    private void OnMissionStarted()
    {
        player.Clear();
    }

    private static void OnFemaleVoicesChanged(object sender, EventArgs args)
    {
        if (Settings.FemaleVoices.Value)
            return;                                   // back on: voices already chosen stay, new missions use the whole pool
        int changed = Roster.RosterService.Current?.Revoice() ?? 0;
        ModLog.Info($"Female voices off: {changed} speaker(s) moved to a male voice.");
    }

    private void OnMissionEnded()
    {
        player.Clear();
        AlertLink.Clear();
        ClipLibrary.Clear();
        Moods.Reset();
    }

    private void Update()
    {
        try
        {
            player.Tick();
            Settings.FlushIfDue();
            AlertLink.Tick();
        }
        catch (Exception ex)
        {
            ModLog.Error($"Radio tick failed: {ex}");
        }
    }

    private void OnDestroy()
    {
        EventBus.MissionStarted -= OnMissionStarted;
        EventBus.MissionEnded -= OnMissionEnded;
        Settings.FemaleVoices.SettingChanged -= OnFemaleVoicesChanged;
    }
}
