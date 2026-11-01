using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Neonix.Models;
using Neonix.Services;

namespace Neonix.Views
{
    public class ProfilesView : UserControl
    {
        readonly StackPanel _list = new();

        public ProfilesView()
        {
            var newName = new TextBox { Width = 200, Text = "Gaming", Margin = new Thickness(0, 0, 8, 0) };
            var add = new StackPanel { Orientation = Orientation.Horizontal };
            add.Children.Add(newName);
            add.Children.Add(Ui.Btn("Create from current", (_, _) =>
            {
                var name = newName.Text.Trim();
                if (name.Length == 0 || SettingsStore.Current.Profiles.Any(p => p.Name == name)) return;
                // Deep copy of the active profile.
                var json = JsonSerializer.Serialize(SettingsStore.ActiveProfile);
                var copy = JsonSerializer.Deserialize<Profile>(json)!;
                copy.Name = name;
                copy.TriggerProcess = null;
                SettingsStore.Current.Profiles.Add(copy);
                SettingsStore.Save();
                Rebuild();
            }));

            Content = Ui.Page(
                Ui.Title("Profiles"),
                Ui.Sub("A profile stores lighting, sync and fan curves together. Set a program name (for example cs2 or valorant) " +
                       "and Neonix switches to that profile while the program is running, then switches back."),
                Ui.Card(add, "New profile"),
                _list);
            Rebuild();
        }

        void Rebuild()
        {
            _list.Children.Clear();
            foreach (var p in SettingsStore.Current.Profiles.ToList())
            {
                var prof = p;
                bool active = prof.Name == SettingsStore.Current.ActiveProfile;
                var sp = new StackPanel();

                var trigger = new TextBox { Text = prof.TriggerProcess ?? "", Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
                trigger.LostFocus += (_, _) => { prof.TriggerProcess = trigger.Text.Trim(); SettingsStore.Save(); };
                sp.Children.Add(Ui.Row("Auto-switch when running", trigger, 190));

                var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
                if (!active)
                    btns.Children.Add(Ui.Btn("Activate", (_, _) => { SettingsStore.SwitchProfile(prof.Name); Rebuild(); }));
                if (SettingsStore.Current.Profiles.Count > 1)
                    btns.Children.Add(Ui.Btn("Delete", (_, _) =>
                    {
                        SettingsStore.Current.Profiles.Remove(prof);
                        if (active) SettingsStore.SwitchProfile(SettingsStore.Current.Profiles[0].Name);
                        SettingsStore.Save();
                        Rebuild();
                    }));
                sp.Children.Add(btns);

                _list.Children.Add(Ui.Card(sp, prof.Name + (active ? "   (active)" : "")));
            }
        }
    }
}
