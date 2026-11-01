using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Neonix.Models;

namespace Neonix.Services
{
    /// <summary>Loads and saves settings to %AppData%\Neonix\settings.json.</summary>
    public static class SettingsStore
    {
        static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Neonix");
        static readonly string FilePath = Path.Combine(Dir, "settings.json");
        static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static AppSettings Current { get; private set; } = new();

        public static Profile ActiveProfile
        {
            get
            {
                var p = Current.Profiles.FirstOrDefault(x => x.Name == Current.ActiveProfile);
                if (p != null) return p;
                if (Current.Profiles.Count == 0) Current.Profiles.Add(new Profile());
                Current.ActiveProfile = Current.Profiles[0].Name;
                return Current.Profiles[0];
            }
        }

        public static event Action? ProfileChanged;

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new();
            }
            catch { Current = new(); }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, Options));
            }
            catch { /* best effort */ }
        }

        public static void SwitchProfile(string name)
        {
            if (Current.Profiles.All(p => p.Name != name)) return;
            Current.ActiveProfile = name;
            Save();
            ProfileChanged?.Invoke();
        }

        public static string FriendlyName(string detected) =>
            Current.DeviceNames.TryGetValue(detected, out var n) && !string.IsNullOrWhiteSpace(n) ? n : detected;
    }
}
