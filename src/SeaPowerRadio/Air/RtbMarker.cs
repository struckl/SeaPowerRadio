using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using SeaPower;
using SeaPower.LiveMap;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;

namespace SeaPowerRadio.Air;

/// <summary>
/// Aircraft on their way home stand out: yellow on the map (Patches/RtbPatches → MapContactVisuals.Resolve) and in
/// the formation manager, and returning groups sink to the bottom of the formation manager so the ones still
/// working stay on top. "On the way home" = the game's ReturnToBase state, which bingo and winchester derive from.
/// </summary>
internal static class RtbMarker
{
    private const float TickInterval = 0.5f;

    private static float nextTick;
    private static FormationManagerView view;
    private static readonly HashSet<Noesis.TextBlock> Colored = new HashSet<Noesis.TextBlock>();
    private static Noesis.SolidColorBrush night, day;

    public static bool Enabled => Settings.RtbHighlight != null && Settings.RtbHighlight.Value;

    public static bool IsRtb(ObjectBase unit)
    {
        if (unit == null || unit.IsDestroyed)
            return false;
        object state = unit.StateMachine?.CurrentState;
        return state is AircraftStates.ReturnToBase || state is HelicopterStates.ReturnToBase;
    }

    /// <summary>A formation manager row: a group (its leader, or all of it, heading home), a unit, a group member.</summary>
    public static bool IsRtbRow(object row)
    {
        switch (row)
        {
            case UnitFormation formation:
                ObjectBase leader = formation.LeaderStation?.UnitObject;
                if (leader != null)
                    return IsRtb(leader);
                List<ObjectBase> members = formation.Stations?.Select(s => s.UnitObject).Where(u => u != null).ToList();
                return members != null && members.Count > 0 && members.All(IsRtb);
            case Station station:
                return IsRtb(station.UnitObject);
            case ObjectBase unit:
                return IsRtb(unit);
            default:
                return false;
        }
    }

#if FLATMAP
    /// <summary>Yellow for the map's night or day colours (Sea Power 0.8.7: one theme for every map).</summary>
    public static Noesis.SolidColorBrush Brush()
    {
        bool dark = !Singleton<ThemeTracker>.InstanceExists() || Singleton<ThemeTracker>.Instance.CurrentTheme != ThemeMode.Light;
#else
    /// <summary>
    /// Yellow for the map's night or day colours. In the 3D-globe build every map has its own palette (Auto follows
    /// the sun at the centre of its view): the symbol being drawn passes its map's, the formation manager has none
    /// and takes the player's tactical map; night when there is none yet.
    /// </summary>
    public static Noesis.SolidColorBrush Brush(MapPalette palette = null)
    {
        palette ??= Globals._mainGameViewModel?.Map?.DisplayMap?.Palette;
        bool dark = palette == null || GameApi.IsDark(palette);
#endif
        if (dark)
            return night ??= new Noesis.SolidColorBrush(Noesis.Color.FromArgb(0xFF, 0xFF, 0xD2, 0x3C));
        return day ??= new Noesis.SolidColorBrush(Noesis.Color.FromArgb(0xFF, 0xB8, 0x86, 0x00));
    }

    public static void ViewCreated(FormationManagerView created)
    {
        view = created;
        Colored.Clear();
    }

    public static void Clear()
    {
        view = null;
        Colored.Clear();
    }

    public static void Tick()
    {
        if (!Enabled || Time.unscaledTime < nextTick)
            return;
        nextTick = Time.unscaledTime + TickInterval;
        try
        {
            FormationManagerViewModel vm = Globals._mainGameViewModel?.FormationManager;
            if (vm != null)
            {
                Sink(vm.AllUnits);
                Sink(vm.SurfaceUnits);
                Sink(vm.SubmarineUnits);
                Sink(vm.AircraftUnits);
                Sink(vm.HelicopterUnits);
                Sink(vm.LandUnits);
            }
            if (view != null && view.IsVisible)
                Paint(view, false);
        }
        catch (Exception ex)
        {
            ModLog.Debug($"[rtb] {ex.Message}");
        }
    }

    /// <summary>Returning groups to the bottom of the list, keeping their order (and everyone else's).</summary>
    private static void Sink(ObservableCollection<INotifyPropertyChanged> rows)
    {
        List<INotifyPropertyChanged> top = rows.Where(IsTopLevel).ToList();
        int first = top.FindIndex(IsRtbRow);
        if (first < 0 || top.Skip(first).All(IsRtbRow))
            return;                                 // none, or already all at the bottom
        foreach (INotifyPropertyChanged row in top.Where(IsRtbRow).ToList())
            rows.Move(rows.IndexOf(row), rows.Count - 1);
    }

    // Same as FormationManagerViewModel.IsTopLevelRow: groups, and units not in a group.
    private static bool IsTopLevel(INotifyPropertyChanged row) =>
        row is UnitFormation || (row is ObjectBase unit && !unit.InFormation.Value);

    /// <summary>Text in rows of returning groups/units yellow; back to the template's colour when they aren't.</summary>
    private static void Paint(Noesis.DependencyObject node, bool rtb)
    {
        if (node is Noesis.TreeViewItem item)
            rtb = IsRtbRow(item.DataContext);
        if (node is Noesis.TextBlock text)
        {
            if (rtb)
            {
                text.Foreground = Brush();
                Colored.Add(text);
            }
            else if (Colored.Remove(text))
            {
                text.ClearValue(Noesis.TextBlock.ForegroundProperty);
            }
        }
        if (!(node is Noesis.Visual visual))
            return;
        int count = Noesis.VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < count; i++)
            Paint(Noesis.VisualTreeHelper.GetChild(visual, i), rtb);
    }
}
