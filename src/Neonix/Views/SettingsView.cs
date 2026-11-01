using System.Windows;
using System.Windows.Controls;
using Neonix.Services;

namespace Neonix.Views
{
    public class SettingsView : UserControl
    {
        public SettingsView()
        {
            var s = SettingsStore.Current;

            CheckBox Check(string text, bool value, System.Action<bool> set)
            {
                var c = new CheckBox { Content = text, IsChecked = value, Foreground = Ui.B("Text"), Margin = new Thickness(0, 6, 0, 6) };
                c.Checked += (_, _) => { set(true); SettingsStore.Save(); };
                c.Unchecked += (_, _) => { set(false); SettingsStore.Save(); };
                return c;
            }

            var startup = new CheckBox
            {
                Content = "Start Neonix with Windows (minimised to tray)", IsChecked = s.StartWithWindows,
                Foreground = Ui.B("Text"), Margin = new Thickness(0, 6, 0, 6),
            };
            startup.Checked += (_, _) => { s.StartWithWindows = Startup.Set(true); SettingsStore.Save(); };
            startup.Unchecked += (_, _) => { Startup.Set(false); s.StartWithWindows = false; SettingsStore.Save(); };

            var general = new StackPanel();
            general.Children.Add(startup);
            general.Children.Add(Check("Keep running in the tray when the window is closed", s.MinimizeToTray, v => s.MinimizeToTray = v));

            var path = new TextBox { Text = s.OpenRgbPath ?? "", Width = 420, HorizontalAlignment = HorizontalAlignment.Left };
            path.LostFocus += (_, _) => { s.OpenRgbPath = string.IsNullOrWhiteSpace(path.Text) ? null : path.Text.Trim(); SettingsStore.Save(); };

            var drivers = new StackPanel();
            drivers.Children.Add(Check("Start the RGB driver automatically", s.StartOpenRgbAutomatically, v => s.StartOpenRgbAutomatically = v));
            drivers.Children.Add(Ui.Row("OpenRGB.exe path", path, 140));
            drivers.Children.Add(Ui.Label(
                "Leave the path empty to use an 'OpenRGB' folder next to Neonix.exe. Use a recent experimental OpenRGB build for newer controllers. " +
                "Only one program can talk to a device at a time, so close RGB Fusion, SignalRGB and iCUE first.", 12, true));

            var about = Ui.Label(
                "Neonix 0.1.0 – open-source RGB and fan control.\n" +
                "RGB driver layer: OpenRGB (GPL-2.0). Sensors and fans: LibreHardwareMonitor (MPL-2.0).\n" +
                "github.com/cbevss/Neonix", 13, true);

            Content = Ui.Page(
                Ui.Title("Settings"),
                Ui.Sub(""),
                Ui.Card(general, "General"),
                Ui.Card(drivers, "RGB driver"),
                Ui.Card(about, "About"));
        }
    }
}
