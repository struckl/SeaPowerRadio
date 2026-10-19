using System;
using System.Collections.Generic;
using System.Linq;
using SeaPower;
using UnityEngine;

namespace SeaPowerRadio.WhyNot;

/// <summary>
/// The selected unit's stacked attack orders, each with a delete button. The game only offers "cease fire",
/// which throws away the whole queue; here orders 3 and 4 can go while 1, 2 and 5 stay. Each row also shows what
/// the order is waiting for (the weapon's engage state, or the reload time left).
/// Toggle with the configured key (F8).
/// <para>Clicks: IMGUI mouse events never reach this window in the game (the game's UI, Noesis, takes them – a
/// lower GUI depth didn't help). So the delete buttons are hit-tested against the mouse read straight from the Input
/// System in Update; the IMGUI click stays as a second path, and a frame guard keeps one click from deleting
/// twice.</para>
/// </summary>
internal sealed class QueueOverlay : MonoBehaviour
{
    private const int MaxRows = 8;
    private bool visible = true;
    private Rect window = new Rect(0, 0, 440, 60);
    private GUIStyle row;
    private ObjectBase shownFor;
    private float scale = 1f;

    // The delete buttons as drawn last, in screen pixels (top-left origin), for the Input System hit test.
    private readonly List<(Rect screen, EngageTask task)> buttons = new List<(Rect, EngageTask)>();
    private ObjectBase buttonsFor;
    private List<WeaponSystem> buttonsSystems;
    private int deletedFrame = -1;

    private void Update()
    {
        if (buttons.Count == 0 || buttonsFor == null)
            return;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;
        Vector2 p = mouse.position.ReadValue();
        p.y = Screen.height - p.y;
        foreach ((Rect screen, EngageTask task) in buttons)
        {
            if (!screen.Contains(p))
                continue;
            deletedFrame = Time.frameCount;
            Delete(buttonsFor, task, buttonsSystems);
            buttons.Clear();                        // the rows move up: wait for the next draw
            return;
        }
    }

    private void OnGUI()
    {
        if (!Settings.QueueOverlay.Value)
            return;
        GUI.depth = -10000; // before NoesisView (depth = -camera depth), which uses up clicks over its panels
        Event ev = Event.current;
        if (ev.type == EventType.KeyDown && ev.keyCode == Settings.QueueOverlayKey.Value)
        {
            visible = !visible;
            ev.Use();
        }
        if (ev.type == EventType.Repaint)
            buttons.Clear();                        // re-filled below if the window is drawn
        if (!visible || Roster.RosterService.Current == null || !Singleton<RenderPosition>.InstanceExists())
            return;

        ObjectBase unit = null;
        try { unit = Singleton<RenderPosition>.Instance.getSelectedObject(); }
        catch (Exception) { /* no selection API in this state */ }
        if (unit == null || !unit.IsPlayerUnit || unit._currentEngageTasks == null)
            return;
        List<EngageTask> tasks = unit._currentEngageTasks.Where(t => !t._isAutoAttack).ToList();
        if (tasks.Count == 0)
            return;

        scale = Mathf.Max(1f, Screen.height / 1080f);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        if (unit != shownFor)
        {
            shownFor = unit;
            window.x = Screen.width / scale - window.width - 20f;
            window.y = 120f;
        }
        row ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false };
        window.height = 34f + Math.Min(tasks.Count, MaxRows) * 24f;
        window = GUI.Window(0x5EA9, window, id => DrawQueue(unit, tasks), $"Orders – {unit.getName()}  ({Settings.QueueOverlayKey.Value} hides)");
        GUI.matrix = old;
    }

    private void DrawQueue(ObjectBase unit, List<EngageTask> tasks)
    {
        float y = 22f;
        List<WeaponSystem> systems = unit._obp?._weaponSystems ?? new List<WeaponSystem>();
        for (int i = 0; i < tasks.Count && i < MaxRows; i++)
        {
            EngageTask t = tasks[i];
            string ammo = (t._ammunition ?? unit.getAmmunitionByName(t._ammoId))?._ap?._displayedName ?? t._ammoId;
            string target = t._targetObject != null ? $"Track {t._targetObject.GetMapContactID()}" : "position";
            GUI.Label(new Rect(8, y, 330, 22), $"{i + 1}. {ammo} → {target} · {Status(unit, t, systems)}", row);
            var delete = new Rect(392, y, 36, 20);
            Event ev = Event.current;
            if (ev.type == EventType.Repaint)
            {
                buttons.Add((new Rect((window.x + delete.x) * scale, (window.y + delete.y) * scale,
                                      delete.width * scale, delete.height * scale), t));
                buttonsFor = unit;
                buttonsSystems = systems;
            }
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
            y += 24f;
        }
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
        // Whatever is left of a click inside the window is ours – not a click on the game's UI or the map below.
        Event rest = Event.current;
        if ((rest.type == EventType.MouseDown || rest.type == EventType.MouseUp)
            && new Rect(0, 0, window.width, window.height).Contains(rest.mousePosition))
            rest.Use();
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
            WhyNotMonitor.MarkCleared(unit);
            foreach (WeaponSystem w in systems.Where(w => w._takenEngageTask == t))
                w.EndEngageTask();
            unit.RemoveEngageTask(t);
            ModLog.Info($"[queue] {unit.getName()}: deleted order {t._ammoId} -> {t._targetObject?.GetMapContactID()}" +
                        $" ({unit._currentEngageTasks?.Count ?? 0} left)");
        }
        catch (Exception ex)
        {
            ModLog.Error($"[queue] delete failed: {ex.Message}");
        }
    }
}
