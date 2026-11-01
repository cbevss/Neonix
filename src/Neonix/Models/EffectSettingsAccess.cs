using Neonix.Services;

namespace Neonix.Models
{
    /// <summary>Lets background services read live lighting settings without referencing the UI.</summary>
    public static class EffectSettingsAccess
    {
        public static int Smoothing() => SettingsStore.ActiveProfile.Lighting.Smoothing;
        public static int Sensitivity() => SettingsStore.ActiveProfile.Lighting.Sensitivity;
    }
}
