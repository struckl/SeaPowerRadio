using UnityEngine;

namespace SeaPowerRadio;

/// <summary>
/// Persistent MonoBehaviour the mod hangs its per-frame work on (voice queue, overlay). Lives on a
/// DontDestroyOnLoad object created at load, so it survives scene changes between menu and missions.
/// </summary>
internal sealed class RadioHost : MonoBehaviour
{
    public static RadioHost Instance { get; private set; }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
