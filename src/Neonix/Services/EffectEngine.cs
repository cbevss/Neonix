using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        readonly ScreenService _screen;
        readonly AudioService _audio;
        readonly CancellationTokenSource _cts = new();
        readonly Task _loop;
        readonly Stopwatch _clock = Stopwatch.StartNew();

        // Per-device animation state, keyed by device index (-1 is the in-app preview).
        readonly ConcurrentDictionary<int, float[]> _heat = new();
        readonly ConcurrentDictionary<int, float[]> _spark = new();
        double _lastPreview;

        /// <summary>When set, this device index is lit solid white (used by "Identify").</summary>
        public int? IdentifyDevice { get; set; }

        /// <summary>When set, lights only LEDs [start, start+count) of a device white, the rest dim.</summary>
        public (int Device, int Start, int Count)? Highlight { get; set; }

        public EffectEngine(RgbService rgb, HardwareService hw, ScreenService screen, AudioService audio)
        {
            _rgb = rgb;
            _hw = hw;
            _screen = screen;
            _audio = audio;
            _loop = Task.Run(() => Loop(_cts.Token));
        }

        async Task Loop(CancellationToken ct)
        {
            double last = _clock.Elapsed.TotalSeconds;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    double now = _clock.Elapsed.TotalSeconds;
                    double dt = Math.Clamp(now - last, 0.001, 0.25);
                    last = now;

                    var profile = SettingsStore.ActiveProfile;
                    UpdateSources(profile);

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
                            frame = Render(profile, dev.Index, dev.LedCount, now, dt);
                        _rgb.SetLeds(dev.Index, frame);
                    }
                }
                catch { /* keep the loop alive */ }

                try { await Task.Delay(33, ct); } catch (TaskCanceledException) { break; }
            }
        }

        /// <summary>Starts screen capture / audio listening only while an effect needs it.</summary>
        void UpdateSources(Profile p)
        {
            var e = p.Lighting.Effect;
            bool plain = p.Sync.Mode == SyncMode.None;
            _screen.SetActive(plain && e == EffectType.ScreenAmbience);
            _audio.SetActive(plain && (e == EffectType.AudioSpectrum || e == EffectType.AudioPulse));
        }

        /// <summary>What the current effect looks like on a strip of n LEDs (for the in-app preview).</summary>
        public uint[] Preview(int n)
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = Math.Clamp(now - _lastPreview, 0.001, 0.25);
            _lastPreview = now;
            return Render(SettingsStore.ActiveProfile, -1, n, now, dt);
        }

        uint[] Render(Profile p, int key, int n, double t, double dt)
        {
            var l = p.Lighting;
            var s = p.Sync;
            double speed = 0.1 + l.Speed / 100.0 * 2.0;
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
                Array.Fill(frame, ColorUtil.Scale(ColorUtil.Lerp(ColorUtil.Parse(s.CoolColor), ColorUtil.Parse(s.HotColor), k), bright));
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

                case EffectType.Comet:
                {
                    int tail = Math.Max(4, n / 4);
                    double head = (t * speed * 14) % n;
                    for (int i = 0; i < n; i++)
                    {
                        double d = ((head - i) % n + n) % n;
                        if (d >= tail) continue;
                        double k = 1 - d / tail;
                        frame[i] = ColorUtil.Scale(ColorUtil.Lerp(c2, c1, k), bright * k * k);
                    }
                    break;
                }

                case EffectType.Sparkle:
                {
                    var spark = Buffer(_spark, key, n);
                    double chance = 0.01 + l.Speed / 100.0 * 0.05;
                    for (int i = 0; i < n; i++)
                    {
                        spark[i] = (float)Math.Max(0, spark[i] - dt * (1.2 + speed));
                        if (Random.Shared.NextDouble() < chance) spark[i] = 1f;
                        var baseColor = ColorUtil.Scale(c1, 0.12);
                        frame[i] = ColorUtil.Scale(ColorUtil.Lerp(baseColor, c2, spark[i]), bright);
                    }
                    break;
                }

                case EffectType.Fire:
                {
                    var heat = Buffer(_heat, key, n);
                    // Cool, drift upwards, then add new sparks near the start of the strip.
                    for (int i = 0; i < n; i++)
                        heat[i] = Math.Max(0, heat[i] - (float)(Random.Shared.NextDouble() * ((55.0 * 10 / n) + 2) / 255.0));
                    for (int k = n - 1; k >= 2; k--)
                        heat[k] = (heat[k - 1] + heat[k - 2] + heat[k - 2]) / 3f;
                    if (Random.Shared.NextDouble() < 0.35 + l.Speed / 200.0)
                    {
                        int y = Random.Shared.Next(Math.Min(7, n));
                        heat[y] = Math.Min(1f, heat[y] + (float)(160 + Random.Shared.Next(95)) / 255f);
                    }
                    for (int i = 0; i < n; i++)
                        frame[i] = ColorUtil.Scale(HeatColor(heat[i]), bright);
                    break;
                }

                case EffectType.ScreenAmbience:
                {
                    var bands = _screen.Snapshot();
                    for (int i = 0; i < n; i++)
                    {
                        int band = Math.Min(ScreenService.BandCount - 1, (int)((i + 0.5) * ScreenService.BandCount / n));
                        frame[i] = ColorUtil.Scale(bands[band], bright);
                    }
                    break;
                }

                case EffectType.AudioSpectrum:
                {
                    var bands = _audio.Bands();
                    for (int i = 0; i < n; i++)
                    {
                        int band = Math.Min(AudioService.BandCount - 1, (int)((i + 0.5) * AudioService.BandCount / n));
                        double level = bands[band];
                        double pos = n > 1 ? (double)i / (n - 1) : 0;
                        frame[i] = ColorUtil.Scale(ColorUtil.Lerp(c1, c2, pos), bright * (0.06 + 0.94 * level));
                    }
                    break;
                }

                case EffectType.AudioPulse:
                {
                    double level = _audio.Bass();
                    Array.Fill(frame, ColorUtil.Scale(ColorUtil.Lerp(c1, c2, level), bright * (0.15 + 0.85 * level)));
                    break;
                }
            }
            return frame;
        }

        static float[] Buffer(ConcurrentDictionary<int, float[]> store, int key, int n)
        {
            var arr = store.GetOrAdd(key, _ => new float[n]);
            if (arr.Length != n)
            {
                arr = new float[n];
                store[key] = arr;
            }
            return arr;
        }

        /// <summary>Black → red → orange → yellow → white.</summary>
        static uint HeatColor(float h)
        {
            h = Math.Clamp(h, 0f, 1f);
            double r = Math.Min(1, h * 3), g = Math.Clamp(h * 3 - 1, 0, 1), b = Math.Clamp(h * 3 - 2, 0, 1);
            return ColorUtil.Pack(r * 255, g * 255, b * 255);
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
