using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Neonix.Services;

namespace Neonix.Views
{
    public class DevicesView : UserControl
    {
        readonly StackPanel _rgbList = new();
        readonly StackPanel _usbList = new();
        readonly TextBlock _status = Ui.Label("", 12, true);

        public DevicesView()
        {
            var rescan = Ui.Btn("Rescan", (_, _) => { App.Rgb.Rescan(); Rebuild(); });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            buttons.Children.Add(rescan);

            Content = Ui.Page(
                Ui.Title("Devices"),
                Ui.Sub("Detected RGB hardware and USB devices. Rename devices to whatever you like; names are saved."),
                buttons,
                Ui.Card(_rgbList, "RGB devices"),
                Ui.Card(BuildSegmentTool(), "Daisy-chain setup"),
                Ui.Card(_usbList, "USB devices from RGB / cooling brands"),
                _status);

            App.Rgb.DevicesChanged += OnDevicesChanged;
            Unloaded += (_, _) => App.Rgb.DevicesChanged -= OnDevicesChanged;
            Rebuild();
        }

        void OnDevicesChanged() => App.RunOnUi(Rebuild);

        void Rebuild()
        {
            _status.Text = "RGB driver: " + App.Rgb.Status;
            _rgbList.Children.Clear();
            if (App.Rgb.Devices.Count == 0)
                _rgbList.Children.Add(Ui.Label("No RGB devices found yet. Close RGB Fusion / SignalRGB / iCUE, then press Rescan.", 13, true));

            foreach (var d in App.Rgb.Devices)
            {
                var dev = d;
                var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel();
                var name = new TextBox { Text = SettingsStore.FriendlyName(dev.Name), Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
                name.LostFocus += (_, _) =>
                {
                    SettingsStore.Current.DeviceNames[dev.Name] = name.Text;
                    SettingsStore.Save();
                };
                info.Children.Add(name);
                info.Children.Add(Ui.Label($"{dev.Type} · {dev.LedCount} LEDs · {string.Join(", ", dev.Zones)}", 12, true));

                var enabled = new CheckBox
                {
                    Content = "Lighting on", IsChecked = !SettingsStore.ActiveProfile.Lighting.DisabledDevices.Contains(dev.Name),
                    Foreground = Ui.B("Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0),
                };
                void SetEnabled(bool on)
                {
                    var list = SettingsStore.ActiveProfile.Lighting.DisabledDevices;
                    list.Remove(dev.Name);
                    if (!on) list.Add(dev.Name);
                    SettingsStore.Save();
                }
                enabled.Checked += (_, _) => SetEnabled(true);
                enabled.Unchecked += (_, _) => SetEnabled(false);

                var identify = Ui.Btn("Identify", (_, _) =>
                {
                    App.Effects.IdentifyDevice = dev.Index;
                    var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    t.Tick += (_, _) => { App.Effects.IdentifyDevice = null; t.Stop(); };
                    t.Start();
                });

                Grid.SetColumn(info, 0);
                Grid.SetColumn(enabled, 1);
                Grid.SetColumn(identify, 2);
                row.Children.Add(info);
                row.Children.Add(enabled);
                row.Children.Add(identify);
                _rgbList.Children.Add(row);
            }

            _usbList.Children.Clear();
            var usb = UsbDetector.Scan();
            if (usb.Count == 0) _usbList.Children.Add(Ui.Label("Nothing recognised.", 13, true));
            foreach (var u in usb)
                _usbList.Children.Add(Ui.Label($"{u.Vendor} – {u.Product}   (VID {u.VendorId:X4} / PID {u.ProductId:X4})", 13));
        }

        /// <summary>
        /// Daisy-chained RGB can't report what's on it, so this lights a range of LEDs
        /// so you can see which physical part it is.
        /// </summary>
        UIElement BuildSegmentTool()
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.Label(
                "Cable covers chained off one header can't be detected. Pick the header, then light a range of LEDs to see which part it is.", 12, true));

            var dev = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
            void FillDevices()
            {
                dev.Items.Clear();
                foreach (var d in App.Rgb.Devices) dev.Items.Add($"{d.Index}: {d.Name} ({d.LedCount} LEDs)");
                if (dev.Items.Count > 0 && dev.SelectedIndex < 0) dev.SelectedIndex = 0;
            }
            FillDevices();
            App.Rgb.DevicesChanged += () => App.RunOnUi(FillDevices);

            var start = new TextBox { Text = "0", Width = 60, HorizontalAlignment = HorizontalAlignment.Left };
            var count = new TextBox { Text = "10", Width = 60, HorizontalAlignment = HorizontalAlignment.Left };

            sp.Children.Add(Ui.Row("Device", dev));
            sp.Children.Add(Ui.Row("First LED", start));
            sp.Children.Add(Ui.Row("How many", count));

            var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            btns.Children.Add(Ui.Btn("Light this range", (_, _) =>
            {
                if (dev.SelectedIndex < 0) return;
                int.TryParse(start.Text, out var s);
                int.TryParse(count.Text, out var c);
                App.Effects.Highlight = (App.Rgb.Devices[dev.SelectedIndex].Index, Math.Max(0, s), Math.Max(1, c));
            }));
            btns.Children.Add(Ui.Btn("Stop", (_, _) => App.Effects.Highlight = null));
            sp.Children.Add(btns);
            return sp;
        }
    }
}
