using System;
using System.Collections.Generic;
using System.Linq;
using SeaPower;
using SeaPowerRadio.Core;
using UnityEngine;

namespace SeaPowerRadio.WhyNot;

/// <summary>
/// The selected unit's stacked attack orders, each with a delete button. The game only offers "cease fire",
/// which throws away the whole queue; here orders 3 and 4 can go while 1, 2 and 5 stay. Each row also shows what
/// the order is waiting for (the weapon's engage state, or the reload time left).
/// Toggle with the configured key (F8).
/// <para>Clicks: IMGUI mouse events never reach this window in the game (the game's UI, Noesis, takes them - a
/// lower GUI depth didn't help). So Update reads the press the way the game reads its own map clicks (the old input
/// manager) and hit-tests it against the row layout itself; the IMGUI click stays as a second path, and a frame guard
/// keeps one click from deleting twice. The layout is computed, not collected while drawing: in the VM test of
/// 2026-11-20 the rows were on screen while the list filled during the repaint was empty at click time.</para>
/// </summary>
internal sealed class QueueOverlay : MonoBehaviour
{
    private const int MaxRows = 8;
    private const int GuiDepth = -10000;           // before NoesisView (depth = -camera depth), which uses up clicks over its panels
    private const int WindowId = 0x5EA9;           // any id no other IMGUI window of the game uses
    private const float FirstRowY = 22f, RowHeight = 24f;
    private static readonly Rect DeleteInRow = new Rect(392, 0, 36, 20);   // the delete button, x and size within a row
    private bool visible = true;
    private Rect window = new Rect(0, 0, 440, 60);
    private GUIStyle row;
    private ObjectBase shownFor;
    private int shownFrame = -1;                   // last frame the window was drawn
    private float scale = 1f;
    private int deletedFrame = -1;

    private void Update()
    {
        // only a window drawn just now counts: after a selection change shownFor still names the old unit
        if (!LegacyPress() || shownFor == null || Time.frameCount - shownFrame > 2 || deletedFrame == Time.frameCount)
            return;
        Vector2 at = UnityEngine.Input.mousePosition;
        Vector2 p = new Vector2(at.x, Screen.height - at.y) / scale;   // window coordinates are unscaled
        if (!window.Contains(p))
            return;
        List<EngageTask> tasks = Orders(shownFor);
        for (int i = 0; i < tasks.Count && i < MaxRows; i++)
        {
            if (!DeleteRect(i).Contains(p - window.position))
                continue;
            deletedFrame = Time.frameCount;
            Delete(shownFor, tasks[i], shownFor._obp?._weaponSystems ?? new List<WeaponSystem>());
            return;
        }
        Rig.Log.Info($"[queue] click at {p.x:0},{p.y:0} inside the window hit no delete button ({tasks.Count} rows," +
                    $" window at {window.x:0},{window.y:0}, scale {scale:0.##})");
    }

    /// <summary>A left press this frame by the old input manager; false where the game has it switched off.</summary>
    private static bool LegacyPress()
    {
        try { return UnityEngine.Input.GetMouseButtonDown(0); }
        catch (InvalidOperationException) { return false; }
    }

    private static Rect DeleteRect(int row) =>
        new Rect(DeleteInRow.x, FirstRowY + row * RowHeight, DeleteInRow.width, DeleteInRow.height);

    private static List<EngageTask> Orders(ObjectBase unit) =>
        unit._currentEngageTasks?.Where(t => !t._isAutoAttack).ToList() ?? new List<EngageTask>();

    private void OnGUI()
    {
        if (!Settings.QueueOverlay.Value)
            return;
        GUI.depth = GuiDepth;
        Event ev = Event.current;
        if (ev.type == EventType.KeyDown && ev.keyCode == Settings.QueueOverlayKey.Value)
        {
            visible = !visible;
            ev.Use();
        }
        ObjectBase unit = SelectedUnit();
        if (unit == null)
            return;
        List<EngageTask> tasks = Orders(unit);
        if (tasks.Count > 0)
            DrawWindow(unit, tasks);
    }

    /// <summary>The selected own unit with an order list, while the window is shown in a mission; else null.</summary>
    private ObjectBase SelectedUnit()
    {
        if (!visible || Roster.RosterService.Current == null || !Singleton<RenderPosition>.InstanceExists())
            return null;
        ObjectBase unit = null;
        try { unit = Singleton<RenderPosition>.Instance.getSelectedObject(); }
        catch (Exception) { /* no selection API in this state */ }
        return unit != null && unit.IsPlayerUnit && unit._currentEngageTasks != null ? unit : null;
    }

    private void DrawWindow(ObjectBase unit, List<EngageTask> tasks)
    {
        scale = Mathf.Max(1f, Screen.height / 1080f);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        if (unit != shownFor)
        {
            shownFor = unit;
            window.x = Screen.width / scale - window.width - 20f;
            window.y = 120f;
        }
        shownFrame = Time.frameCount;
        row ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false };
        window.height = FirstRowY + 12f + Math.Min(tasks.Count, MaxRows) * RowHeight;
        window = GUI.Window(WindowId, window, id => DrawQueue(unit, tasks), $"Orders – {unit.getName()}  ({Settings.QueueOverlayKey.Value} hides)");
        GUI.matrix = old;
    }

    private void DrawQueue(ObjectBase unit, List<EngageTask> tasks)
    {
        List<WeaponSystem> systems = unit._obp?._weaponSystems ?? new List<WeaponSystem>();
        for (int i = 0; i < tasks.Count && i < MaxRows; i++)
            DrawRow(unit, tasks[i], i, systems);
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
        // Whatever is left of a click inside the window is ours - not a click on the game's UI or the map below.
        Event rest = Event.current;
        if ((rest.type == EventType.MouseDown || rest.type == EventType.MouseUp)
            && new Rect(0, 0, window.width, window.height).Contains(rest.mousePosition))
            rest.Use();
    }

    private void DrawRow(ObjectBase unit, EngageTask t, int i, List<WeaponSystem> systems)
    {
        Rect delete = DeleteRect(i);
        float y = delete.y;
        string ammo = OrderText.Weapon(unit, t);
        string target = OrderText.Target(t);
        GUI.Label(new Rect(8, y, 330, 22), $"{i + 1}. {ammo} → {target} · {Status(unit, t, systems)}", row);
        Event ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0 && delete.Contains(ev.mousePosition))
        {
            // On the press, not the release: a click can change the game's selection before the release arrives.
            if (deletedFrame != Time.frameCount)
            {
                deletedFrame = Time.frameCount;
                Delete(unit, t, systems);
            }
            ev.Use();
        }
        GUI.Button(delete, "✕");
    }

    private static string Status(ObjectBase unit, EngageTask t, List<WeaponSystem> systems)
    {
        WeaponSystem working = systems.FirstOrDefault(w => w._executingEngageTask && w._takenEngageTask == t);
        if (working != null)
            return working._engageState.ToString();
        List<WeaponSystem> forAmmo = unit.GetWeaponSystemsForAmmunition(t._ammoId) ?? new List<WeaponSystem>();
        var reloading = forAmmo.Where(w => w._isReloading).ToList();
        if (reloading.Count > 0 && forAmmo.All(w => w._isReloading || w._executingEngageTask))
        {
            float s = reloading.Min(w => Math.Max(0f, w._remainingReloadTime));
            return $"reloading {(int)(s / 60)}:{(int)(s % 60):00}";
        }
        return "waiting";
    }

    private static void Delete(ObjectBase unit, EngageTask t, List<WeaponSystem> systems)
    {
        try
        {
            WhyNotMonitor.MarkCleared(unit);   // the player's doing, not "dropped by the game"
            foreach (WeaponSystem w in systems.Where(w => w._takenEngageTask == t))
                w.EndEngageTask();
            unit.RemoveEngageTask(t);
            Rig.Log.Info($"[queue] {unit.getName()}: deleted order {t._ammoId} -> {t._targetObject?.GetMapContactID()}" +
                        $" ({unit._currentEngageTasks?.Count ?? 0} left)");
        }
        catch (Exception ex)
        {
            Rig.Log.Error($"[queue] delete failed: {ex.Message}");
        }
    }
}
