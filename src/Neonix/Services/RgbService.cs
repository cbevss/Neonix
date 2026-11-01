using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenRGB.NET;

namespace Neonix.Services
{
    public record RgbDevice(int Index, string Name, string Type, int LedCount, IReadOnlyList<string> Zones);

    /// <summary>
    /// Talks to RGB hardware. Version 0.1 uses an OpenRGB server running in the background
    /// (bundled next to Neonix.exe or found on disk) as the driver layer. Native drivers
    /// for common controllers will replace it device by device.
    /// </summary>
    public sealed class RgbService : IDisposable
    {
        OpenRgbClient? _client;
        Process? _server;
        readonly object _lock = new();

        public IReadOnlyList<RgbDevice> Devices { get; private set; } = Array.Empty<RgbDevice>();
        public bool Connected => _client?.Connected == true;
        public string Status { get; private set; } = "Not connected";

        public event Action? DevicesChanged;

        public async Task StartAsync()
        {
            if (SettingsStore.Current.StartOpenRgbAutomatically) TryStartServer();

            for (int attempt = 0; attempt < 15 && !Connected; attempt++)
            {
                TryConnect();
                if (!Connected) await Task.Delay(1000);
            }
            if (!Connected)
                Status = FindOpenRgb() == null
                    ? "RGB driver not found. Put OpenRGB in the 'OpenRGB' folder next to Neonix.exe, or set its path in Settings."
                    : "Couldn't connect to the RGB driver. Close other RGB apps (RGB Fusion, SignalRGB) and press Rescan.";
        }

        public static string? FindOpenRgb()
        {
            var candidates = new List<string?>
            {
                SettingsStore.Current.OpenRgbPath,
                Path.Combine(AppContext.BaseDirectory, "OpenRGB", "OpenRGB.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenRGB", "OpenRGB.exe"),
            };
            return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
        }

        void TryStartServer()
        {
            if (Process.GetProcessesByName("OpenRGB").Any()) return; // already running
            var exe = FindOpenRgb();
            if (exe == null) return;
            try
            {
                _server = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--server --noautoconnect",
                    WorkingDirectory = Path.GetDirectoryName(exe)!,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            }
            catch (Exception ex) { Status = "Failed to start RGB driver: " + ex.Message; }
        }

        void TryConnect()
        {
            lock (_lock)
            {
                try
                {
                    _client?.Dispose();
                    _client = new OpenRgbClient(name: "Neonix", autoConnect: true);
                    Refresh();
                }
                catch { _client = null; }
            }
        }

        public void Rescan()
        {
            if (!Connected) { TryConnect(); return; }
            lock (_lock) { Refresh(); }
        }

        void Refresh()
        {
            if (_client == null) return;
            var data = _client.GetAllControllerData();
            Devices = data.Select((d, i) => new RgbDevice(
                    i, d.Name, d.Type.ToString(), d.Leds.Length,
                    d.Zones.Select(z => z.Name).ToList()))
                .ToList();
            foreach (var d in Devices)
            {
                try { _client.SetCustomMode(d.Index); } catch { }
            }
            Status = $"Connected – {Devices.Count} RGB device(s)";
            DevicesChanged?.Invoke();
        }

        /// <summary>Sends one colour per LED to a device. Colours are packed 0xRRGGBB.</summary>
        public void SetLeds(int deviceIndex, uint[] colors)
        {
            lock (_lock)
            {
                if (_client == null || !_client.Connected) return;
                var arr = new Color[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                    arr[i] = new Color((byte)(colors[i] >> 16), (byte)(colors[i] >> 8), (byte)colors[i]);
                try { _client.UpdateLeds(deviceIndex, arr); }
                catch { Status = "Lost connection to RGB driver"; }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                try { _client?.Dispose(); } catch { }
                _client = null;
            }
            try { if (_server is { HasExited: false }) _server.Kill(); } catch { }
        }
    }
}
