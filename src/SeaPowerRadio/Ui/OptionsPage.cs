using System;
using System.Linq;
using BepInEx.Configuration;
using SeapowerUI;
using SeaPowerRadio.Voices;

namespace SeaPowerRadio.Ui;

/// <summary>
/// The mod's settings as a group "Rats Radio" at the bottom of the Audio tab of the game's Options menu. The page
/// (OptionsMenuView) is a Noesis XAML control whose Audio tab is a vertical StackPanel of rows (a Grid with label,
/// slider and value for the volumes, CheckBoxes below). When a view has loaded its XAML (its constructor) the group
/// is built in code and appended to that panel, found through the named "cb_MuteSoundInPause" check box (or, if
/// that is renamed, another named Audio control). Styles and brushes come from the page's own resources, so the
/// rows look like the game's. The controls are not bound to the game's options view model: a change is applied and
/// kept in the mod's config at once (Accept / Revert of the page do not touch it).
/// </summary>
internal static class OptionsPage
{
    private const string GroupTag = "SeaPowerRadio.Options";
    private static bool reported;

    public static void ViewCreated(OptionsMenuView view)
    {
        if (view == null)
            return;
        try
        {
            Noesis.StackPanel panel = FindAudioPanel(view);
            if (panel == null)
            {
                if (!reported)
                    ModLog.Warn("[options] the Audio tab of the Options menu was not found (game update?) - the radio settings are only in the config file.");
                reported = true;
                return;
            }
            for (int i = 0; i < panel.Children.Count; i++)
                if (panel.Children[i] is Noesis.FrameworkElement fe && fe.Tag as string == GroupTag)
                    return;                                       // this view has the group already
            Build(view, panel);
            if (!reported)
                ModLog.Info("[options] radio settings added to the Audio tab of the Options menu");
            reported = true;
        }
        catch (Exception ex)
        {
            ModLog.Warn($"[options] radio settings not added to the Options menu: {ex}");
        }
    }

    private static Noesis.StackPanel FindAudioPanel(OptionsMenuView view)
    {
        foreach (string name in new[] { "cb_MuteSoundInPause", "cb_SimultaneousVoiceovers", "sliderVoiceVolume", "sliderMasterVolume" })
        {
            Noesis.DependencyObject at = view.FindName(name) as Noesis.DependencyObject;
            for (int depth = 0; at != null && depth < 6; depth++)
            {
                at = at is Noesis.FrameworkElement fe && fe.Parent != null ? fe.Parent : Noesis.VisualTreeHelper.GetParent(at);
                if (at is Noesis.StackPanel sp && sp.Orientation == Noesis.Orientation.Vertical)
                    return sp;
            }
        }
        return null;
    }

    private static void Build(OptionsMenuView view, Noesis.StackPanel panel)
    {
        var look = new Look(view);

        var header = new Noesis.TextBlock { Tag = GroupTag, Text = "Rats Radio", Margin = new Noesis.Thickness(0, 12, 0, 4) };
        if (look.Header != null)
            header.Style = look.Header;
        panel.Children.Add(header);
        var rule = new Noesis.Border { Height = 1, Opacity = 0.35f, Margin = new Noesis.Thickness(0, 0, 0, 12) };
        if (look.Rule != null)
            rule.Style = look.Rule;
        else if (look.Highlight != null)
            rule.Background = look.Highlight;
        panel.Children.Add(rule);

        panel.Children.Add(Note(look, "Changes apply at once and are kept by the mod (Accept and Revert on this page do not affect them)."));

        panel.Children.Add(Check(look, "Female voices", Settings.FemaleVoices,
            "Off: units speak with male voices only. Applies at once to the units of a running mission; a unit whose name exists only in a female voice keeps it."));
        panel.Children.Add(Check(look, "Overlapping voices", Settings.OverlapVoices,
            "On: several stations can talk over each other like a busy net (up to the number in the config file, 3 by default). Off: one call at a time, the others wait."));

        panel.Children.Add(Slider(look, "Radio volume", "Master volume of all radio calls, on top of the game's voice volume.", Settings.Volume, 2f));
        panel.Children.Add(Slider(look, "Radio static", "Hiss and crackle under the voices. 0 = clean voice, 100 % = the default amount. Applies from the next call.", Settings.RadioNoise, 1.5f));

        panel.Children.Add(Note(look, "Volume per kind of call (100 % = default):"));
        panel.Children.Add(Slider(look, "Contact calls", "New, lost and identified contacts and tracks (new contact, track hostile, identified by radar).", Settings.VolumeContacts, 2f));
        panel.Children.Add(Slider(look, "Combat calls", "Weapons away, splash, hit, target destroyed, jamming.", Settings.VolumeCombat, 2f));
        panel.Children.Add(Slider(look, "Warning calls", "Missile or torpedo inbound, radar lock, we are hit, flooding, bingo, emergencies.", Settings.VolumeWarnings, 2f));
        panel.Children.Add(Slider(look, "Order replies", "Acknowledgements, status replies and the reasons why an order is refused.", Settings.VolumeOrders, 2f));
        panel.Children.Add(Slider(look, "Other calls", "Launch and recovery, fuel, search and rescue, mission messages and everything else.", Settings.VolumeOther, 2f));

        BuildVoicePacks(look, panel);
    }

    /// <summary>
    /// "Voice packs": which pack speaks for the units of each country. Countries come from the game's own nation list,
    /// in collapsible groups (NATO, Warsaw Pact, Other; nations.txt), one row per country with a drop-down:
    /// Auto (what it resolves to), Game voice, or an installed pack. Applies at the next mission start.
    /// </summary>
    private static void BuildVoicePacks(Look look, Noesis.StackPanel panel)
    {
        var header = new Noesis.TextBlock { Text = "Voice packs", Margin = new Noesis.Thickness(0, 12, 0, 4) };
        if (look.Header != null)
            header.Style = look.Header;
        panel.Children.Add(header);
        var rule = new Noesis.Border { Height = 1, Opacity = 0.35f, Margin = new Noesis.Thickness(0, 0, 0, 12) };
        if (look.Rule != null)
            rule.Style = look.Rule;
        else if (look.Highlight != null)
            rule.Background = look.Highlight;
        panel.Children.Add(rule);

        var packs = VoicePacks.Installed;
        panel.Children.Add(Note(look, "Installed: " + (packs.Count == 0 ? "none" : string.Join(", ", packs.Select(p => $"{p.Name} {p.Version}")))
            + ". Every unit speaks with the pack of its own country; a country without a pack keeps the game's own voice. Applies from the next mission."));

        var nations = Nations.All();
        foreach (var group in nations.GroupBy(n => n.Group))
        {
            var rows = new Noesis.StackPanel { Orientation = Noesis.Orientation.Vertical, Margin = new Noesis.Thickness(12, 4, 0, 4) };
            foreach (NationInfo nation in group)
                rows.Children.Add(PackRow(look, nation));
            var expander = new Noesis.Expander
            {
                Header = $"{group.Key} ({group.Count()})",
                IsExpanded = false,
                Content = rows,
                Margin = new Noesis.Thickness(0, 0, 0, 8),
            };
            if (look.Highlight != null)
                expander.Foreground = look.Highlight;
            panel.Children.Add(expander);
        }
    }

    private static Noesis.Grid PackRow(Look look, NationInfo nation)
    {
        var row = new Noesis.Grid { Margin = new Noesis.Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new Noesis.ColumnDefinition { Width = new Noesis.GridLength(1, Noesis.GridUnitType.Star) });
        row.ColumnDefinitions.Add(new Noesis.ColumnDefinition { Width = new Noesis.GridLength(1, Noesis.GridUnitType.Auto) });

        var label = new Noesis.TextBlock { Text = nation.Name, VerticalAlignment = Noesis.VerticalAlignment.Center, MinWidth = 150 };
        if (look.Label != null)
            label.Style = look.Label;
        Noesis.Grid.SetColumn(label, 0);

        var entry = Settings.NationEntry(nation.Id);
        var packs = VoicePacks.Installed;
        var combo = new Noesis.ComboBox { MinWidth = 240, ToolTip = $"Voice pack for the units of {nation.Name}. Applies at the next mission start." };
        VoicePack auto = VoicePacks.AutoPack(nation.Id);
        combo.Items.Add($"Auto ({auto?.Name ?? VoicePacks.GameVoice})");
        combo.Items.Add(VoicePacks.GameVoice);
        foreach (VoicePack pack in packs)
            combo.Items.Add(pack.Name);

        string current = Settings.NationChoice(nation.Id);
        int selected = 0;                                               // Auto, also for a pack that is not installed any more
        if (string.Equals(current, VoicePacks.GameVoice, StringComparison.OrdinalIgnoreCase))
            selected = 1;
        else
            for (int i = 0; i < packs.Count; i++)
                if (string.Equals(packs[i].Id, current, StringComparison.OrdinalIgnoreCase))
                    selected = i + 2;
        combo.SelectedIndex = selected;
        combo.SelectionChanged += (sender, args) =>
        {
            int index = combo.SelectedIndex;
            if (index < 0) return;
            string value = index == 0 ? VoicePacks.Auto : index == 1 ? VoicePacks.GameVoice : packs[index - 2].Id;
            if (string.Equals(entry.Value, value, StringComparison.Ordinal)) return;
            entry.Value = value;
            Settings.SaveSoon();
        };
        Noesis.Grid.SetColumn(combo, 1);

        row.Children.Add(label);
        row.Children.Add(combo);
        return row;
    }

    /// <summary>The page's own styles and brushes (null where a resource is missing: the control keeps its default).</summary>
    private sealed class Look
    {
        public readonly Noesis.Style Header, Rule, Label;
        public readonly Noesis.Brush Highlight, Background;
        public readonly float NoteSize;

        public Look(OptionsMenuView view)
        {
            Header = view.TryFindResource("OptionsSectionHeader") as Noesis.Style;
            Rule = view.TryFindResource("OptionsSectionRule") as Noesis.Style;
            Label = view.TryFindResource("TextBlockLabel") as Noesis.Style;
            Highlight = view.TryFindResource("Brush.Foreground.Highlight") as Noesis.Brush;
            Background = view.TryFindResource("Brush.Background.Normal") as Noesis.Brush;
            object size = view.TryFindResource("Font.Size.ToolTip");
            NoteSize = size is float f ? f : size is double d ? (float)d : 0f;
        }
    }

    private static Noesis.TextBlock Note(Look look, string text)
    {
        var note = new Noesis.TextBlock { Text = text, TextWrapping = Noesis.TextWrapping.Wrap, Margin = new Noesis.Thickness(0, 0, 0, 12), Opacity = 0.8f };
        if (look.Highlight != null)
            note.Foreground = look.Highlight;
        if (look.NoteSize > 0f)
            note.FontSize = look.NoteSize;
        return note;
    }

    private static Noesis.CheckBox Check(Look look, string label, ConfigEntry<bool> entry, string tip)
    {
        var box = new Noesis.CheckBox { Content = label, Margin = new Noesis.Thickness(0, 0, 0, 15), IsChecked = entry.Value, ToolTip = tip };
        if (look.Highlight != null)
            box.Foreground = look.Highlight;
        box.Click += (sender, args) =>
        {
            entry.Value = box.IsChecked == true;
            Settings.SaveSoon();
        };
        return box;
    }

    private static Noesis.Grid Slider(Look look, string label, string tip, ConfigEntry<float> entry, float max)
    {
        var row = new Noesis.Grid { Margin = new Noesis.Thickness(0, 0, 0, 15), ToolTip = tip };
        row.ColumnDefinitions.Add(new Noesis.ColumnDefinition { Width = new Noesis.GridLength(1, Noesis.GridUnitType.Auto) });
        row.ColumnDefinitions.Add(new Noesis.ColumnDefinition { Width = new Noesis.GridLength(1, Noesis.GridUnitType.Star) });
        row.ColumnDefinitions.Add(new Noesis.ColumnDefinition { Width = new Noesis.GridLength(1, Noesis.GridUnitType.Auto) });

        var text = new Noesis.TextBlock { Text = label, MinWidth = 150 };
        if (look.Label != null)
            text.Style = look.Label;
        Noesis.Grid.SetColumn(text, 0);

        var slider = new Noesis.Slider
        {
            Minimum = 0f,
            Maximum = max,
            TickFrequency = 0.05f,
            IsSnapToTickEnabled = true,
            Value = Math.Min(entry.Value, max),
        };
        Noesis.Grid.SetColumn(slider, 1);

        var value = new Noesis.TextBlock
        {
            Text = Percent(slider.Value),
            HorizontalAlignment = Noesis.HorizontalAlignment.Center,
            VerticalAlignment = Noesis.VerticalAlignment.Center,
        };
        if (look.Highlight != null)
            value.Foreground = look.Highlight;
        var box = new Noesis.Border { BorderThickness = new Noesis.Thickness(1), Width = 48, Child = value };
        if (look.Background != null)
            box.Background = look.Background;
        Noesis.Grid.SetColumn(box, 2);

        slider.ValueChanged += (sender, args) =>
        {
            float v = (float)Math.Round(slider.Value * 20.0) / 20f;     // 5 % steps
            value.Text = Percent(v);
            if (Math.Abs(entry.Value - v) > 0.001f)
            {
                entry.Value = v;
                Settings.SaveSoon();
            }
        };

        row.Children.Add(text);
        row.Children.Add(slider);
        row.Children.Add(box);
        return row;
    }

    private static string Percent(double v) => Math.Round(v * 100.0) + "%";
}
