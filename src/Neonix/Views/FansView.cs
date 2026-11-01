using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Neonix.Models;
using Neonix.Services;

namespace Neonix.Views
{
    /// <summary>Fan curves: pick a fan control, a temperature source and a curve.</summary>
    public class FansView : UserControl
    {
        readonly StackPanel _list = new();

        public FansView()
        {
            var presets = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            presets.Children.Add(Ui.Btn("Silent", (_, _) => ApplyPreset(FanPresets.Silent())));
            presets.Children.Add(Ui.Btn("Balanced", (_, _) => ApplyPreset(FanPresets.Balanced())));
            presets.Children.Add(Ui.Btn("Performance", (_, _) => ApplyPreset(FanPresets.Performance())));
            presets.Children.Add(Ui.Btn("100%", (_, _) => ApplyPreset(new() { new(0, 100), new(100, 100) })));

            Content = Ui.Page(
                Ui.Title("Fans"),
                Ui.Sub("Tick a fan to let Neonix control it with a curve. Unticked fans stay under your BIOS. " +
                       "Closing Neonix always hands fans back to the BIOS. Preset buttons apply to all ticked fans."),
                presets,
                _list);
            Rebuild();
        }

        void ApplyPreset(System.Collections.Generic.List<CurvePoint> pts)
        {
            foreach (var f in SettingsStore.ActiveProfile.Fans)
                f.Points = pts.Select(p => new CurvePoint(p.Temp, p.Percent)).ToList();
            SettingsStore.Save();
            Rebuild();
        }

        void Rebuild()
        {
            _list.Children.Clear();
            var hw = App.Hardware;
            var controls = hw.Controls.ToList();
            var temps = hw.Temperatures.ToList();

            if (controls.Count == 0)
            {
                _list.Children.Add(Ui.Card(Ui.Label(
                    "No controllable fans found. Make sure Neonix is running as administrator. " +
                    "Some boards can't be controlled from Windows; AIO pumps connected by USB may need their own driver.", 13, true),
                    "Nothing to control"));
                return;
            }

            var profile = SettingsStore.ActiveProfile;
            foreach (var c in controls)
            {
                var ctl = c;
                var curve = profile.Fans.FirstOrDefault(f => f.ControlId == ctl.Id);
                if (curve == null)
                {
                    curve = new FanCurve
                    {
                        ControlId = ctl.Id,
                        SourceId = hw.Temperatures.FirstOrDefault(t => t.Name.Contains("Package") || t.Name.Contains("Tctl"))?.Id
                                   ?? temps.FirstOrDefault()?.Id ?? "",
                    };
                    profile.Fans.Add(curve);
                }
                var cv = curve;

                var p = new StackPanel();

                var on = new CheckBox
                {
                    Content = $"Control this fan   (now {ctl.Value:0}%)", IsChecked = cv.Enabled,
                    Foreground = Ui.B("Text"), Margin = new Thickness(0, 0, 0, 8),
                };
                on.Checked += (_, _) => { cv.Enabled = true; SettingsStore.Save(); };
                on.Unchecked += (_, _) => { cv.Enabled = false; SettingsStore.Save(); };
                p.Children.Add(on);

                var src = new ComboBox { Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
                foreach (var t in temps) src.Items.Add(new ComboBoxItem { Content = $"{t.Hardware} – {t.Name}", Tag = t.Id });
                src.SelectedItem = src.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == cv.SourceId);
                src.SelectionChanged += (_, _) =>
                {
                    if (src.SelectedItem is ComboBoxItem i) { cv.SourceId = (string)i.Tag; SettingsStore.Save(); }
                };
                p.Children.Add(Ui.Row("Follow temperature", src, 150));

                var grid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                for (int i = 0; i < cv.Points.Count; i++)
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i < cv.Points.Count; i++)
                {
                    var pt = cv.Points[i];
                    var cell = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
                    cell.Children.Add(Ui.Label($"{pt.Temp:0}°C", 12, true));
                    var pct = new TextBox { Text = pt.Percent.ToString("0") };
                    pct.LostFocus += (_, _) =>
                    {
                        if (double.TryParse(pct.Text, out var v)) { pt.Percent = Math.Clamp(v, 0, 100); SettingsStore.Save(); }
                    };
                    cell.Children.Add(pct);
                    cell.Children.Add(Ui.Label("% speed", 11, true));
                    Grid.SetColumn(cell, i);
                    grid.Children.Add(cell);
                }
                p.Children.Add(grid);

                _list.Children.Add(Ui.Card(p, $"{ctl.Hardware} – {ctl.Name}"));
            }
        }
    }
}
