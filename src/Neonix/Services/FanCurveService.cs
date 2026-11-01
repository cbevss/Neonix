using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neonix.Models;

namespace Neonix.Services
{
    /// <summary>Applies the active profile's fan curves once per second.</summary>
    public sealed class FanCurveService : IDisposable
    {
        readonly HardwareService _hw;
        readonly Timer _timer;
        readonly HashSet<string> _controlled = new();

        public FanCurveService(HardwareService hw)
        {
            _hw = hw;
            _timer = new Timer(_ => Tick(), null, 2000, 1000);
        }

        void Tick()
        {
            try
            {
                var curves = SettingsStore.ActiveProfile.Fans;
                var active = new HashSet<string>();
                foreach (var c in curves.Where(c => c.Enabled && c.Points.Count > 0))
                {
                    var temp = _hw.ReadValue(c.SourceId);
                    if (temp == null) continue;
                    if (_hw.SetFan(c.ControlId, (float)Evaluate(c.Points, temp.Value)))
                        active.Add(c.ControlId);
                }
                // Anything we were controlling but no longer are goes back to BIOS control.
                foreach (var id in _controlled.Except(active).ToList()) _hw.ResetFan(id);
                _controlled.Clear();
                _controlled.UnionWith(active);
            }
            catch { }
        }

        /// <summary>Linear interpolation between curve points.</summary>
        public static double Evaluate(IList<CurvePoint> pts, double temp)
        {
            var p = pts.OrderBy(x => x.Temp).ToList();
            if (temp <= p[0].Temp) return p[0].Percent;
            if (temp >= p[^1].Temp) return p[^1].Percent;
            for (int i = 1; i < p.Count; i++)
            {
                if (temp > p[i].Temp) continue;
                double k = (temp - p[i - 1].Temp) / Math.Max(0.001, p[i].Temp - p[i - 1].Temp);
                return p[i - 1].Percent + (p[i].Percent - p[i - 1].Percent) * k;
            }
            return p[^1].Percent;
        }

        public void Dispose()
        {
            _timer.Dispose();
            foreach (var id in _controlled) _hw.ResetFan(id);
        }
    }
}
