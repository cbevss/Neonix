using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Neonix.Models
{
    // New effects are appended so settings saved by older versions keep their meaning.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EffectType
    {
        Static, Breathing, Rainbow, Wave, ColorCycle, Off,
        Fire, Comet, Sparkle,
        ScreenAmbience, AudioSpectrum, AudioPulse,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SyncMode { None, CpuTemperature, GpuTemperature }

    public class LightingSettings
    {
        public EffectType Effect { get; set; } = EffectType.Rainbow;
        public string Color1 { get; set; } = "#FF2EAA";
        public string Color2 { get; set; } = "#00E1FF";
        public int Speed { get; set; } = 50;       // 0-100
        public int Brightness { get; set; } = 100; // 0-100
        /// <summary>Audio effects: how strongly the lights react (0-100).</summary>
        public int Sensitivity { get; set; } = 60;
        /// <summary>Screen ambience: how much colour changes are smoothed (0-95).</summary>
        public int Smoothing { get; set; } = 60;
        /// <summary>Device names (as reported) that are excluded from lighting.</summary>
        public List<string> DisabledDevices { get; set; } = new();
    }

    public class SyncSettings
    {
        public SyncMode Mode { get; set; } = SyncMode.None;
        public int CoolTemp { get; set; } = 40;
        public int HotTemp { get; set; } = 80;
        public string CoolColor { get; set; } = "#00E1FF";
        public string HotColor { get; set; } = "#FF2020";
    }

    public class CurvePoint
    {
        public double Temp { get; set; }
        public double Percent { get; set; }
        public CurvePoint() { }
        public CurvePoint(double t, double p) { Temp = t; Percent = p; }
    }

    public class FanCurve
    {
        /// <summary>Identifier of the fan control sensor.</summary>
        public string ControlId { get; set; } = "";
        /// <summary>Identifier of the temperature sensor that drives the curve.</summary>
        public string SourceId { get; set; } = "";
        public bool Enabled { get; set; }
        public List<CurvePoint> Points { get; set; } = FanPresets.Balanced();
    }

    public static class FanPresets
    {
        public static List<CurvePoint> Silent() => new()
            { new(30, 20), new(50, 30), new(65, 45), new(75, 70), new(85, 100) };
        public static List<CurvePoint> Balanced() => new()
            { new(30, 30), new(50, 40), new(65, 60), new(75, 80), new(85, 100) };
        public static List<CurvePoint> Performance() => new()
            { new(30, 45), new(50, 60), new(65, 80), new(75, 95), new(80, 100) };
    }

    public class Profile
    {
        public string Name { get; set; } = "Default";
        public LightingSettings Lighting { get; set; } = new();
        public SyncSettings Sync { get; set; } = new();
        public List<FanCurve> Fans { get; set; } = new();
        /// <summary>Optional process name (e.g. "cs2") that auto-activates this profile.</summary>
        public string? TriggerProcess { get; set; }
        /// <summary>Email of the account that owns this profile. Null means shared by everyone on this PC.</summary>
        public string? Owner { get; set; }
    }

    public class AppSettings
    {
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTray { get; set; } = true;
        public bool StartOpenRgbAutomatically { get; set; } = true;
        public bool SidebarHidden { get; set; }
        public string? OpenRgbPath { get; set; }
        public string? SignedInEmail { get; set; }
        public string ActiveProfile { get; set; } = "Default";
        public List<Profile> Profiles { get; set; } = new() { new Profile() };
        /// <summary>User-given friendly names, keyed by the detected device name.</summary>
        public Dictionary<string, string> DeviceNames { get; set; } = new();
    }
}
