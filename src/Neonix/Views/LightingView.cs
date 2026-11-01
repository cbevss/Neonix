using System;
using System.Windows.Controls;
using Neonix.Models;
using Neonix.Services;

namespace Neonix.Views
{
    public class LightingView : UserControl
    {
        public LightingView()
        {
            var l = SettingsStore.ActiveProfile.Lighting;

            var effect = new ComboBox { Width = 200, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            foreach (var e in Enum.GetValues<EffectType>()) effect.Items.Add(e);
            effect.SelectedItem = l.Effect;
            effect.SelectionChanged += (_, _) =>
            {
                if (effect.SelectedItem is EffectType t) { l.Effect = t; SettingsStore.Save(); }
            };

            var panel = new StackPanel();
            panel.Children.Add(Ui.Row("Effect", effect));
            panel.Children.Add(Ui.Row("Colour 1", Ui.ColorPick(l.Color1, h => { l.Color1 = h; SettingsStore.Save(); })));
            panel.Children.Add(Ui.Row("Colour 2", Ui.ColorPick(l.Color2, h => { l.Color2 = h; SettingsStore.Save(); })));
            panel.Children.Add(Ui.Row("Speed", Ui.Slide(0, 100, l.Speed, v => { l.Speed = (int)v; SettingsStore.Save(); })));
            panel.Children.Add(Ui.Row("Brightness", Ui.Slide(0, 100, l.Brightness, v => { l.Brightness = (int)v; SettingsStore.Save(); })));

            Content = Ui.Page(
                Ui.Title("Lighting"),
                Ui.Sub("Effects apply to every RGB device that has lighting switched on in the Devices tab. " +
                       "Colour 2 is used by Wave. If Sync is turned on it takes over from the effect."),
                Ui.Card(panel, $"Profile: {SettingsStore.Current.ActiveProfile}"));
        }
    }
}
