using System;
using System.Collections.Generic;
using System.Globalization;
using SeaPower;
using SeapowerUI;

namespace SeaPowerRadio.Formation;

/// <summary>
/// A third line in every formation manager row that says what the unit is (<see cref="UnitRole"/>). The view's row
/// templates (group, unit, group member) each lay out a grid: name in row 0, class or group summary in row 1, a thin
/// spacer in row 2. When a FormationManagerView is built — before it has made a single row — each template's grid
/// gets a row under the class line with a text bound to the role: same column and font, a bit smaller and dimmer.
/// The templates are the view's own, so every row it makes from then on has the line, right-click and double-click
/// work on it as on the rest of the row, and the RTB colouring (Air/RtbMarker) covers it too.
/// </summary>
internal static class RoleLine
{
    private const string LineTag = "SeaPowerRadio.Role";   // a Tag, not a Name: names in templates are registered
    private const double SizeFactor = 0.85;
    private const float LineOpacity = 0.7f;

    // Row kinds the view has templates for, keyed by data type. Each binds the role to what its row shows: the
    // group's summary changes with its members, a member row with the unit standing in the station.
    private static readonly Type[] RowTypes = { typeof(UnitFormation), typeof(ObjectBase), typeof(Station) };

    private static bool reported;

    public static bool Enabled => Settings.UnitRoles != null && Settings.UnitRoles.Value;

    public static void ViewCreated(FormationManagerView view)
    {
        if (!Enabled || view == null)
            return;
        try
        {
            var missing = new List<string>();
            foreach (Type rowType in RowTypes)
            {
                if (!AddTo(view.Resources, rowType))
                    missing.Add(rowType.Name);
            }
            if (reported)
                return;
            reported = true;
            if (missing.Count == 0)
                ModLog.Info("[roles] formation manager: role line added to group, unit and member rows");
            else
                ModLog.Warn($"[roles] formation manager: no row template with a second line for {string.Join(", ", missing)} " +
                            "(game update?) - those rows stay without a role");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[roles] formation manager left without roles: {ex.Message}");
        }
    }

    private static bool AddTo(Noesis.ResourceDictionary resources, Type rowType)
    {
        if (resources == null || !resources.Contains(rowType) || !(resources[rowType] is Noesis.DataTemplate template)
            || !(template.VisualTree is Noesis.Grid grid))
            return false;

        Noesis.TextBlock secondLine = null;
        int count = grid.Children.Count;
        for (int i = 0; i < count; i++)
        {
            Noesis.UIElement child = grid.Children[i];
            if (child is Noesis.FrameworkElement element && element.Tag as string == LineTag)
                return true;                                  // this template already has it
            if (child is Noesis.TextBlock text && Noesis.Grid.GetRow(text) == 1)
                secondLine = text;
        }
        if (secondLine == null || grid.RowDefinitions.Count < 2)
            return false;

        const int row = 2;
        for (int i = 0; i < count; i++)
        {
            Noesis.UIElement child = grid.Children[i];
            int at = Noesis.Grid.GetRow(child);
            if (at >= row)
                Noesis.Grid.SetRow(child, at + 1);
        }
        grid.RowDefinitions.Insert(row, new Noesis.RowDefinition { Height = new Noesis.GridLength(1, Noesis.GridUnitType.Auto) });

        var line = new Noesis.TextBlock
        {
            Tag = LineTag,
            Opacity = LineOpacity,
            VerticalAlignment = secondLine.VerticalAlignment,
            TextTrimming = Noesis.TextTrimming.CharacterEllipsis,
        };
        Noesis.Grid.SetRow(line, row);
        Noesis.Grid.SetColumn(line, Noesis.Grid.GetColumn(secondLine));
        Noesis.Grid.SetColumnSpan(line, Noesis.Grid.GetColumnSpan(secondLine));
        line.SetBinding(Noesis.TextBlock.TextProperty, TextBinding(rowType));
        line.SetBinding(Noesis.UIElement.VisibilityProperty, new Noesis.Binding("Text")
        {
            RelativeSource = new Noesis.RelativeSource(Noesis.RelativeSourceMode.Self),
            Converter = Shown.Instance,
        });
        line.SetBinding(Noesis.TextBlock.FontSizeProperty, new Noesis.Binding("FontSize")
        {
            RelativeSource = new Noesis.RelativeSource(Noesis.RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Noesis.TreeViewItem),
            },
            Converter = Smaller.Instance,
        });
        grid.Children.Add(line);
        return true;
    }

    private static Noesis.BindingBase TextBinding(Type rowType)
    {
        if (rowType == typeof(UnitFormation))
        {
            var multi = new Noesis.MultiBinding { Converter = RoleText.Instance };
            multi.Bindings.Add(new Noesis.Binding());
            multi.Bindings.Add(new Noesis.Binding(nameof(UnitFormation.InFormationSummary)));
            return multi;
        }
        if (rowType == typeof(Station))
            return new Noesis.Binding(nameof(Station.UnitObject)) { Converter = RoleText.Instance };
        return new Noesis.Binding { Converter = RoleText.Instance };
    }

    /// <summary>The row (or the unit in a station) → its role. With several values the first is the row, the rest
    /// are only there so the text is worked out again when they change.</summary>
    private sealed class RoleText : Noesis.IValueConverter, Noesis.IMultiValueConverter
    {
        public static readonly RoleText Instance = new RoleText();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Role(value);

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            Role(values != null && values.Length > 0 ? values[0] : null);

        private static string Role(object row)
        {
            if (!Enabled)
                return "";
            try
            {
                return UnitRole.ForRow(row);
            }
            catch (Exception ex)
            {
                ModLog.Debug($"[roles] {ex.Message}");
                return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>No role, no line - an empty text would still take a line's height.</summary>
    private sealed class Shown : Noesis.IValueConverter
    {
        public static readonly Shown Instance = new Shown();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.IsNullOrEmpty(value as string) ? Noesis.Visibility.Collapsed : Noesis.Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>The row's font size, scaled down: follows the game's UI and text size settings.</summary>
    private sealed class Smaller : Noesis.IValueConverter
    {
        public static readonly Smaller Instance = new Smaller();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is float size ? size * (float)SizeFactor
            : value is double d ? d * SizeFactor
            : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
