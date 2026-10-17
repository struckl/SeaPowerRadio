using System;
using SeaPowerRadio.Contacts;
using SeaPowerRadio.Core;
using SeaPowerRadio.Radio;
using UnityEngine;

namespace SeaPowerRadio;

/// <summary>
/// Persistent MonoBehaviour the mod hangs its per-frame work on: the radio net and the contact reporter. Lives on
/// a DontDestroyOnLoad object created at load, so it survives scene changes between menu and missions.
/// </summary>
internal sealed class RadioHost : MonoBehaviour
{
    public static RadioHost Instance { get; private set; }

    private RadioPlayer player;
    private ContactReporter contacts;

    private void Awake()
    {
        Instance = this;
        player = new RadioPlayer(this);
        contacts = new ContactReporter(player);
        StockVoiceRouter.Player = player;
        EventBus.MissionStarted += OnMissionStarted;
        EventBus.MissionEnded += OnMissionEnded;
    }

    private void OnMissionStarted()
    {
        player.Clear();
        contacts.Attach();
    }

    private void OnMissionEnded()
    {
        contacts.Detach();
        player.Clear();
        ClipLibrary.Clear();
    }

    private void Update()
    {
        try
        {
            player.Tick();
            contacts.Tick();
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
        if (Instance == this)
            Instance = null;
    }
}
