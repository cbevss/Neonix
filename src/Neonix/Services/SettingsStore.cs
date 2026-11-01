using System;
using System.Collections.Generic;
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

        public static string DataDirectory => Dir;

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

        /// <summary>Profiles the signed-in account (or everyone, if signed out) may see.</summary>
        public static IEnumerable<Profile> VisibleProfiles()
        {
            var me = Current.SignedInEmail;
            return Current.Profiles.Where(p => p.Owner == null || p.Owner == me);
        }

        /// <summary>After signing in or out, make sure the active profile is one the user may use.</summary>
        public static void EnsureVisibleActive()
        {
            var visible = VisibleProfiles().ToList();
            if (visible.Count == 0)
            {
                Current.Profiles.Add(new Profile { Name = "Default" });
                visible = VisibleProfiles().ToList();
            }
            if (visible.All(p => p.Name != Current.ActiveProfile))
                SwitchProfile(visible[0].Name);
            else
                ProfileChanged?.Invoke();
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
