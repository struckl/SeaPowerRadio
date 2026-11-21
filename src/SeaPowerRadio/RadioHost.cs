using System;
using SeaPowerRadio.Air;
using SeaPowerRadio.Contacts;
using SeaPowerRadio.Radio;
using SeaPowerRadio.WhyNot;

namespace SeaPowerRadio;

/// <summary>
/// The persistent object the radio net's and the reporters' per-frame work hangs on (RigHost, R.A.T.S. Rigging), on a
/// DontDestroyOnLoad object created at load, so it survives scene changes between menu and missions.
/// </summary>
internal sealed class RadioHost : RigHost
{
    private RadioPlayer player;
    private ContactReporter contacts;
    private WhyNotMonitor whyNot;
    private FuelMonitor fuel;
    private SarReporter sar;

    protected override void Awake()
    {
        base.Awake();
        player = new RadioPlayer(this);
        contacts = new ContactReporter(player);
        whyNot = new WhyNotMonitor(player);
        fuel = new FuelMonitor(player);
        sar = new SarReporter(player);
        gameObject.AddComponent<QueueOverlay>();
        StockVoiceRouter.Player = player;
        Settings.FemaleVoices.SettingChanged += OnFemaleVoicesChanged;
    }

    protected override void OnMissionReady()
    {
        player.Clear();
        whyNot.Clear();
        fuel.Clear();
        sar.Clear();
        RtbMarker.Clear();
        contacts.Attach();
    }

    private static void OnFemaleVoicesChanged(object sender, EventArgs args)
    {
        if (Settings.FemaleVoices.Value)
            return;                                   // back on: voices already chosen stay, new missions use the whole pool
        int changed = Roster.RosterService.Current?.Revoice() ?? 0;
        Rig.Log.Info($"Female voices off: {changed} speaker(s) moved to a male voice.");
    }

    protected override void OnMissionEnded()
    {
        contacts.Detach();
        player.Clear();
        AlertLink.Clear();
        RtbMarker.Clear();
        ClipLibrary.Clear();
        Moods.Reset();
    }

    protected override void Tick()
    {
        player.Tick();
        contacts.Tick();
        whyNot.Tick();
        fuel.Tick();
        sar.Tick();
        RtbMarker.Tick();
        AlertLink.Tick();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Settings.FemaleVoices.SettingChanged -= OnFemaleVoicesChanged;
    }
}
