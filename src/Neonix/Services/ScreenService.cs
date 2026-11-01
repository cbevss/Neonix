using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Neonix.Services
{
    /// <summary>
    /// Screen ambience: samples the primary monitor ~20 times a second and reduces it to
    /// a row of average colours (left to right). Everything stays on this PC; no image is saved or sent.
    /// </summary>
    public sealed class ScreenService : IDisposable
    {
        public const int BandCount = 64;
        const int W = 64, H = 36;

        readonly object _lock = new();
        readonly uint[] _bands = new uint[BandCount];
        Thread? _thread;
        volatile bool _run;

        public bool Active => _run;

        public void SetActive(bool on)
        {
            if (on && !_run)
            {
                _run = true;
                _thread = new Thread(Loop) { IsBackground = true, Name = "Neonix screen capture" };
                _thread.Start();
            }
            else if (!on && _run)
            {
                _run = false;
            }
        }

        /// <summary>The latest colours, left to right (0xRRGGBB).</summary>
        public uint[] Snapshot()
        {
            lock (_lock) { return (uint[])_bands.Clone(); }
        }

        void Loop()
        {
            Bitmap? full = null;
            using var small = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            var smooth = new double[BandCount * 3];
            var pixels = new byte[W * H * 4];

            try
            {
                while (_run)
                {
                    try
                    {
                        var b = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                        if (full == null || full.Width != b.Width || full.Height != b.Height)
                        {
                            full?.Dispose();
                            full = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
                        }

                        using (var g = Graphics.FromImage(full))
                            g.CopyFromScreen(b.Location, Point.Empty, b.Size);
                        using (var g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            g.DrawImage(full, 0, 0, W, H);
                        }

                        var data = small.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                        small.UnlockBits(data);

                        double smoothing = Math.Clamp(Models.EffectSettingsAccess.Smoothing(), 0, 95) / 100.0;
                        double alpha = 1.0 - smoothing * 0.92;

                        for (int x = 0; x < W; x++)
                        {
                            double r = 0, g2 = 0, bl = 0;
                            for (int y = 0; y < H; y++)
                            {
                                int i = (y * W + x) * 4;
                                bl += pixels[i]; g2 += pixels[i + 1]; r += pixels[i + 2];
                            }
                            r /= H; g2 /= H; bl /= H;

                            smooth[x * 3] += (r - smooth[x * 3]) * alpha;
                            smooth[x * 3 + 1] += (g2 - smooth[x * 3 + 1]) * alpha;
                            smooth[x * 3 + 2] += (bl - smooth[x * 3 + 2]) * alpha;
                        }

                        lock (_lock)
                        {
                            for (int x = 0; x < BandCount; x++)
                                _bands[x] = ColorUtil.Pack(smooth[x * 3], smooth[x * 3 + 1], smooth[x * 3 + 2]);
                        }
                    }
                    catch { /* e.g. secure desktop / UAC prompt: skip this frame */ }

                    Thread.Sleep(50);
                }
            }
            finally
            {
                full?.Dispose();
                lock (_lock) { Array.Clear(_bands); }
                _thread = null;
            }
        }

        public void Dispose() => _run = false;
    }
}
