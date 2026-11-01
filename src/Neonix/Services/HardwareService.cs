using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace Neonix.Services
{
    public record SensorReading(string Id, string Hardware, string Name, SensorType Type, float? Value);

    /// <summary>
    /// Reads CPU, GPU, motherboard, memory and cooler sensors through LibreHardwareMonitor,
    /// and exposes motherboard/cooler fan controls.
    /// </summary>
    public sealed class HardwareService : IDisposable
    {
        readonly Computer _computer;
        readonly Timer _timer;
        readonly object _lock = new();

        public IReadOnlyList<SensorReading> Sensors { get; private set; } = Array.Empty<SensorReading>();
        public string CpuName { get; private set; } = "Unknown CPU";
        public string GpuName { get; private set; } = "Unknown GPU";
        public string BoardName { get; private set; } = "Unknown motherboard";
        public List<string> CoolerNames { get; } = new();
        public string? Error { get; private set; }

        public event Action? Updated;

        public HardwareService()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsMemoryEnabled = true,
                IsControllerEnabled = true, // AIOs and fan controllers
            };
            try
            {
                _computer.Open();
                foreach (var hw in _computer.Hardware)
                {
                    switch (hw.HardwareType)
                    {
                        case HardwareType.Cpu: CpuName = hw.Name; break;
                        case HardwareType.GpuAmd:
                        case HardwareType.GpuNvidia:
                        case HardwareType.GpuIntel: GpuName = hw.Name; break;
                        case HardwareType.Motherboard: BoardName = hw.Name; break;
                        case HardwareType.Cooler: CoolerNames.Add(hw.Name); break;
                    }
                }
            }
            catch (Exception ex) { Error = ex.Message; }

            _timer = new Timer(_ => Poll(), null, 0, 1000);
        }

        void Poll()
        {
            if (!Monitor.TryEnter(_lock)) return;
            try
            {
                var list = new List<SensorReading>();
                foreach (var hw in _computer.Hardware) Collect(hw, list);
                Sensors = list;
                Updated?.Invoke();
            }
            catch (Exception ex) { Error = ex.Message; }
            finally { Monitor.Exit(_lock); }
        }

        static void Collect(IHardware hw, List<SensorReading> list)
        {
            hw.Update();
            foreach (var s in hw.Sensors)
                list.Add(new SensorReading(s.Identifier.ToString(), hw.Name, s.Name, s.SensorType, s.Value));
            foreach (var sub in hw.SubHardware) Collect(sub, list);
        }

        public IEnumerable<SensorReading> Temperatures =>
            Sensors.Where(s => s.Type == SensorType.Temperature && s.Value.HasValue);

        public IEnumerable<SensorReading> Fans =>
            Sensors.Where(s => s.Type == SensorType.Fan);

        public IEnumerable<SensorReading> Controls =>
            Sensors.Where(s => s.Type == SensorType.Control);

        public float? CpuTemp =>
            Pick(HardwareType.Cpu, "Package", "Tctl", "Tdie", "Core");

        public float? GpuTemp =>
            PickGpu();

        float? Pick(HardwareType type, params string[] names)
        {
            var hw = _computer.Hardware.FirstOrDefault(h => h.HardwareType == type);
            if (hw == null) return null;
            foreach (var n in names)
            {
                var s = hw.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Temperature && x.Name.Contains(n));
                if (s?.Value != null) return s.Value;
            }
            return hw.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Temperature)?.Value;
        }

        float? PickGpu()
        {
            var hw = _computer.Hardware.FirstOrDefault(h =>
                h.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel);
            return hw?.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Temperature)?.Value;
        }

        public float? Load(HardwareType type) =>
            _computer.Hardware.FirstOrDefault(h => h.HardwareType == type)?
                .Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"))?.Value;

        public float? GpuLoad =>
            _computer.Hardware.FirstOrDefault(h =>
                    h.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel)?
                .Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load)?.Value;

        public float? ReadValue(string id) => Sensors.FirstOrDefault(s => s.Id == id)?.Value;

        ISensor? FindSensor(string id)
        {
            foreach (var hw in _computer.Hardware)
            {
                var s = Find(hw, id);
                if (s != null) return s;
            }
            return null;

            static ISensor? Find(IHardware hw, string id)
            {
                var s = hw.Sensors.FirstOrDefault(x => x.Identifier.ToString() == id);
                if (s != null) return s;
                foreach (var sub in hw.SubHardware)
                {
                    s = Find(sub, id);
                    if (s != null) return s;
                }
                return null;
            }
        }

        /// <summary>Sets a fan control to a fixed percentage (0-100).</summary>
        public bool SetFan(string controlId, float percent)
        {
            lock (_lock)
            {
                var ctrl = FindSensor(controlId)?.Control;
                if (ctrl == null) return false;
                ctrl.SetSoftware(Math.Clamp(percent, 0, 100));
                return true;
            }
        }

        /// <summary>Hands a fan back to the motherboard's own control.</summary>
        public void ResetFan(string controlId)
        {
            lock (_lock) { FindSensor(controlId)?.Control?.SetDefault(); }
        }

        public void Dispose()
        {
            _timer.Dispose();
            lock (_lock)
            {
                try
                {
                    foreach (var c in Controls) FindSensor(c.Id)?.Control?.SetDefault();
                    _computer.Close();
                }
                catch { }
            }
        }
    }
}
