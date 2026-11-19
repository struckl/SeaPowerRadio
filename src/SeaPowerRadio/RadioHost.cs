using System;
using SeaPowerRadio.Radio;

namespace SeaPowerRadio;

/// <summary>
/// The persistent object the radio net's per-frame work hangs on (ModHost, in the shared skeleton), on a DontDestroyOnLoad
/// object created at load, so it survives scene changes between menu and missions.
/// </summary>
internal sealed class RadioHost : ModHost
{
    /// <summary>The radio net. Null while no host runs (mod off): the stock voice router and RadioApi then stand aside.</summary>
    public static RadioPlayer Player { get; private set; }

    private RadioPlayer player;

    protected override void Awake()
    {
        base.Awake();
        Player = player = new RadioPlayer(this);
        gameObject.AddComponent<Ui.UpgradeNotice>();
        Settings.FemaleVoices.SettingChanged += OnFemaleVoicesChanged;
    }

    protected override void OnMissionReady() => player.Clear();

    private static void OnFemaleVoicesChanged(object sender, EventArgs args)
    {
        if (Settings.FemaleVoices.Value)
            return;                                   // back on: voices already chosen stay, new missions use the whole pool
        int changed = Roster.RosterService.Current?.Revoice() ?? 0;
        ModLog.Info($"Female voices off: {changed} speaker(s) moved to a male voice.");
    }

    protected override void OnMissionEnded()
    {
        player.Clear();
        AlertLink.Clear();
        ClipLibrary.Clear();
        Moods.Reset();
    }

    protected override void Tick()
    {
        player.Tick();
        AlertLink.Tick();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Settings.FemaleVoices.SettingChanged -= OnFemaleVoicesChanged;
        if (Player == player)
            Player = null;
    }
}
