using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Neonix.Services
{
    /// <summary>Switches profile automatically when a profile's trigger process (e.g. a game) is running.</summary>
    public sealed class ProfileWatcher : IDisposable
    {
        readonly Timer _timer;
        string? _autoSwitchedFrom;

        public ProfileWatcher() => _timer = new Timer(_ => Tick(), null, 5000, 5000);

        void Tick()
        {
            try
            {
                var running = Process.GetProcesses().Select(p => p.ProcessName.ToLowerInvariant()).ToHashSet();
                var match = SettingsStore.Current.Profiles.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(p.TriggerProcess) &&
                    running.Contains(p.TriggerProcess!.Trim().ToLowerInvariant().Replace(".exe", "")));

                var current = SettingsStore.Current.ActiveProfile;
                if (match != null && match.Name != current)
                {
                    _autoSwitchedFrom ??= current;
                    App.RunOnUi(() => SettingsStore.SwitchProfile(match.Name));
                }
                else if (match == null && _autoSwitchedFrom != null)
                {
                    var back = _autoSwitchedFrom;
                    _autoSwitchedFrom = null;
                    App.RunOnUi(() => SettingsStore.SwitchProfile(back));
                }
            }
            catch { }
        }

        public void Dispose() => _timer.Dispose();
    }
}
