using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neonix.Models;

namespace Neonix.Services
{
    /// <summary>Renders lighting effects at ~30 fps and pushes them to every RGB device.</summary>
    public sealed class EffectEngine : IDisposable
    {
        readonly RgbService _rgb;
        readonly HardwareService _hw;
        readonly CancellationTokenSource _cts = new();
        readonly Task _loop;

        /// <summary>When set, this device index is lit solid white (used by "Identify").</summary>
        public int? IdentifyDevice { get; set; }

        /// <summary>When set, lights only LEDs [start, start+count) of a device white, the rest dim.</summary>
        public (int Device, int Start, int Count)? Highlight { get; set; }

        public EffectEngine(RgbService rgb, HardwareService hw)
        {
            _rgb = rgb;
            _hw = hw;
            _loop = Task.Run(() => Loop(_cts.Token));
        }

        async Task Loop(CancellationToken ct)
        {
            var start = DateTime.UtcNow;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    double t = (DateTime.UtcNow - start).TotalSeconds;
                    var profile = SettingsStore.ActiveProfile;
                    foreach (var dev in _rgb.Devices)
                    {
                        if (dev.LedCount == 0) continue;
                        uint[] frame;
                        if (Highlight is { } h && h.Device == dev.Index)
                        {
                            frame = new uint[dev.LedCount];
                            for (int i = 0; i < frame.Length; i++)
                                frame[i] = i >= h.Start && i < h.Start + h.Count ? 0xFFFFFFu : 0x080808u;
                        }
                        else if (IdentifyDevice == dev.Index)
                            frame = Enumerable.Repeat(0xFFFFFFu, dev.LedCount).ToArray();
                        else if (profile.Lighting.DisabledDevices.Contains(dev.Name))
                            continue;
                        else
                            frame = Render(profile, dev.LedCount, t);
                        _rgb.SetLeds(dev.Index, frame);
                    }
                }
                catch { /* keep the loop alive */ }

                try { await Task.Delay(33, ct); } catch (TaskCanceledException) { break; }
            }
        }

        uint[] Render(Profile p, int n, double t)
        {
            var l = p.Lighting;
            var s = p.Sync;
            double speed = 0.1 + l.Speed / 100.0 * 2.0; // cycles per second-ish
            double bright = l.Brightness / 100.0;
            var c1 = ColorUtil.Parse(l.Color1);
            var c2 = ColorUtil.Parse(l.Color2);
            var frame = new uint[n];

            // Sync modes override the effect with a temperature-driven colour.
            if (s.Mode != SyncMode.None)
            {
                float? temp = s.Mode == SyncMode.CpuTemperature ? _hw.CpuTemp : _hw.GpuTemp;
                double k = temp.HasValue
                    ? Math.Clamp((temp.Value - s.CoolTemp) / Math.Max(1.0, s.HotTemp - s.CoolTemp), 0, 1)
                    : 0;
                uint c = ColorUtil.Scale(ColorUtil.Lerp(ColorUtil.Parse(s.CoolColor), ColorUtil.Parse(s.HotColor), k), bright);
                Array.Fill(frame, c);
                return frame;
            }

            switch (l.Effect)
            {
                case EffectType.Off:
                    break;
                case EffectType.Static:
                    Array.Fill(frame, ColorUtil.Scale(c1, bright));
                    break;
                case EffectType.Breathing:
                {
                    double b = (Math.Sin(t * speed * Math.PI) + 1) / 2;
                    Array.Fill(frame, ColorUtil.Scale(c1, bright * (0.05 + 0.95 * b)));
                    break;
                }
                case EffectType.ColorCycle:
                    Array.Fill(frame, ColorUtil.Scale(ColorUtil.Hsv((t * speed * 60) % 360, 1, 1), bright));
                    break;
                case EffectType.Rainbow:
                    for (int i = 0; i < n; i++)
                        frame[i] = ColorUtil.Scale(ColorUtil.Hsv((t * speed * 90 + i * 360.0 / n) % 360, 1, 1), bright);
                    break;
                case EffectType.Wave:
                    for (int i = 0; i < n; i++)
                    {
                        double k = (Math.Sin(t * speed * 3 - i * 0.35) + 1) / 2;
                        frame[i] = ColorUtil.Scale(ColorUtil.Lerp(c1, c2, k), bright);
                    }
                    break;
            }
            return frame;
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _loop.Wait(500); } catch { }
        }
    }

    public static class ColorUtil
    {
        public static uint Parse(string hex)
        {
            try
            {
                hex = hex.Trim().TrimStart('#');
                if (hex.Length == 8) hex = hex[2..];
                return Convert.ToUInt32(hex, 16) & 0xFFFFFF;
            }
            catch { return 0xFFFFFF; }
        }

        public static string ToHex(uint c) => $"#{c & 0xFFFFFF:X6}";

        public static uint Pack(double r, double g, double b) =>
            ((uint)Math.Clamp(r, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(b, 0, 255);

        public static uint Scale(uint c, double k) =>
            Pack(((c >> 16) & 0xFF) * k, ((c >> 8) & 0xFF) * k, (c & 0xFF) * k);

        public static uint Lerp(uint a, uint b, double k) =>
            Pack(((a >> 16) & 0xFF) + ((double)((b >> 16) & 0xFF) - ((a >> 16) & 0xFF)) * k,
                 ((a >> 8) & 0xFF) + ((double)((b >> 8) & 0xFF) - ((a >> 8) & 0xFF)) * k,
                 (a & 0xFF) + ((double)(b & 0xFF) - (a & 0xFF)) * k);

        public static uint Hsv(double h, double s, double v)
        {
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
            (double r, double g, double b) = h switch
            {
                < 60 => (c, x, 0d),
                < 120 => (x, c, 0d),
                < 180 => (0d, c, x),
                < 240 => (0d, x, c),
                < 300 => (x, 0d, c),
                _ => (c, 0d, x),
            };
            return Pack((r + m) * 255, (g + m) * 255, (b + m) * 255);
        }
    }
}
