using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;

namespace Neonix.Views
{
    public class DashboardView : UserControl
    {
        readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

        readonly TextBlock _specs = Ui.Label("");
        readonly TextBlock _fans = Ui.Label("");
        readonly TextBlock _status = Ui.Label("", 12, true);

        readonly (TextBlock Value, Ui.Meter Bar) _cpu, _gpu, _ram;
        readonly WrapPanel _tempTiles = new();
        readonly Dictionary<string, (TextBlock Value, TextBlock Name)> _tiles = new();

        public DashboardView()
        {
            _cpu = UsageTile();
            _gpu = UsageTile();
            _ram = UsageTile();

            var usage = new UniformGrid { Rows = 1 };
            usage.Children.Add(Tile("CPU", _cpu));
            usage.Children.Add(Tile("GPU", _gpu));
            usage.Children.Add(Tile("RAM", _ram));

            Content = Ui.Page(
                Ui.RgbStrip(),
                Ui.Title("Dashboard"),
                Ui.Sub("Your system at a glance. Everything here was detected automatically."),
                usage,
                Ui.Card(_tempTiles, "Temperatures"),
                Ui.Card(_specs, "System"),
                Ui.Card(_fans, "Fans & pumps"),
                _status);

            Loaded += (_, _) => { Refresh(); _timer.Start(); };
            Unloaded += (_, _) => _timer.Stop();
            _timer.Tick += (_, _) => Refresh();
        }

        static (TextBlock, Ui.Meter) UsageTile() =>
            (new TextBlock { Text = "–", FontSize = 34, FontWeight = FontWeights.Bold, Foreground = Ui.B("Text") },
             new Ui.Meter(8) { Margin = new Thickness(0, 10, 0, 0) });

        static UIElement Tile(string name, (TextBlock Value, Ui.Meter Bar) t)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            { Text = name, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Ui.B("Accent2") });
            sp.Children.Add(t.Value);
            sp.Children.Add(t.Bar);
            var card = Ui.Card(sp);
            card.Margin = new Thickness(0, 0, 14, 14);
            return card;
        }

        void Refresh()
        {
            var hw = App.Hardware;

            var cpu = hw.Load(HardwareType.Cpu);
            var gpu = hw.GpuLoad;
            var ram = hw.Load(HardwareType.Memory);
            SetUsage(_cpu, cpu);
            SetUsage(_gpu, gpu);
            SetUsage(_ram, ram);

            // Temperature tiles: one per sensor, created the first time we see it.
            var temps = hw.Temperatures.Where(t => !t.Name.Contains("Distance")).Take(12).ToList();
            if (temps.Count == 0 && _tiles.Count == 0)
            {
                if (_tempTiles.Children.Count == 0)
                    _tempTiles.Children.Add(Ui.Label("No temperature sensors found yet. Neonix needs to run as administrator.", 13, true));
            }
            foreach (var t in temps)
            {
                if (!_tiles.TryGetValue(t.Id, out var tile))
                {
                    if (_tiles.Count == 0) _tempTiles.Children.Clear();
                    var value = new TextBlock { FontSize = 24, FontWeight = FontWeights.Bold };
                    var label = new TextBlock
                    {
                        Text = $"{Short(t.Hardware)} · {t.Name}", FontSize = 11, Foreground = Ui.B("Muted"),
                        TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 150,
                    };
                    var sp = new StackPanel { Width = 160, Margin = new Thickness(0, 0, 8, 14) };
                    sp.Children.Add(value);
                    sp.Children.Add(label);
                    _tempTiles.Children.Add(sp);
                    Ui.Reveal(sp, _tiles.Count);
                    tile = (value, label);
                    _tiles[t.Id] = tile;
                }
                if (t.Value.HasValue)
                {
                    tile.Value.Text = $"{t.Value.Value:0} °C";
                    tile.Value.Foreground = Ui.TempBrush(t.Value.Value);
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Motherboard:  {hw.BoardName}");
            sb.AppendLine($"CPU:  {hw.CpuName}");
            sb.AppendLine($"GPU:  {hw.GpuName}");
            if (hw.CoolerNames.Count > 0) sb.AppendLine($"Cooler / AIO:  {string.Join(", ", hw.CoolerNames)}");
            sb.Append($"RGB devices:  {App.Rgb.Devices.Count}");
            _specs.Text = sb.ToString();

            var fans = hw.Fans.Where(f => f.Value.HasValue).ToList();
            _fans.Text = fans.Count == 0
                ? "No fan headers reported. Some boards hide them until a driver is loaded."
                : string.Join("\n", fans.Select(f => $"{f.Hardware} – {f.Name}:  {f.Value:0} RPM"));

            _status.Text = hw.Error != null ? "Hardware warning: " + hw.Error : "RGB: " + App.Rgb.Status;
        }

        static void SetUsage((TextBlock Value, Ui.Meter Bar) t, float? v)
        {
            t.Value.Text = v.HasValue ? $"{v.Value:0}%" : "–";
            t.Bar.Set(v ?? 0);
        }

        static string Short(string s) => s.Length > 18 ? s[..18] + "…" : s;
    }
}
