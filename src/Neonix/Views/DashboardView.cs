using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;

namespace Neonix.Views
{
    public class DashboardView : UserControl
    {
        readonly TextBlock _specs = Ui.Label("");
        readonly TextBlock _temps = Ui.Label("");
        readonly TextBlock _fans = Ui.Label("");
        readonly TextBlock _loads = Ui.Label("");
        readonly TextBlock _status = Ui.Label("", 12, true);
        readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

        public DashboardView()
        {
            Content = Ui.Page(
                Ui.Title("Dashboard"),
                Ui.Sub("Your system at a glance. Everything here was detected automatically."),
                Ui.Card(_specs, "System"),
                Ui.Card(_loads, "Usage"),
                Ui.Card(_temps, "Temperatures"),
                Ui.Card(_fans, "Fans & pumps"),
                _status);

            Loaded += (_, _) => { Refresh(); _timer.Start(); };
            Unloaded += (_, _) => _timer.Stop();
            _timer.Tick += (_, _) => Refresh();
        }

        void Refresh()
        {
            var hw = App.Hardware;

            var sb = new StringBuilder();
            sb.AppendLine($"Motherboard:  {hw.BoardName}");
            sb.AppendLine($"CPU:  {hw.CpuName}");
            sb.AppendLine($"GPU:  {hw.GpuName}");
            if (hw.CoolerNames.Count > 0) sb.AppendLine($"Cooler / AIO:  {string.Join(", ", hw.CoolerNames)}");
            sb.Append($"RGB devices:  {App.Rgb.Devices.Count}");
            _specs.Text = sb.ToString();

            var cpu = hw.Load(HardwareType.Cpu);
            var ram = hw.Load(HardwareType.Memory);
            var gpu = hw.GpuLoad;
            _loads.Text =
                $"CPU  {Fmt(cpu, "%")}      GPU  {Fmt(gpu, "%")}      RAM  {Fmt(ram, "%")}";

            var temps = hw.Temperatures.Where(t => !t.Name.Contains("Distance")).Take(14).ToList();
            _temps.Text = temps.Count == 0
                ? "No temperature sensors found yet. Neonix needs to run as administrator."
                : string.Join("\n", temps.Select(t => $"{t.Hardware} – {t.Name}:  {t.Value:0.#} °C"));

            var fans = hw.Fans.Where(f => f.Value.HasValue).ToList();
            _fans.Text = fans.Count == 0
                ? "No fan headers reported. Some boards hide them until a driver is loaded."
                : string.Join("\n", fans.Select(f => $"{f.Hardware} – {f.Name}:  {f.Value:0} RPM"));

            _status.Text = hw.Error != null ? "Hardware warning: " + hw.Error : "RGB: " + App.Rgb.Status;
        }

        static string Fmt(float? v, string unit) => v.HasValue ? $"{v.Value:0}{unit}" : "–";
    }
}
