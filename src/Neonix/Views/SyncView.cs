using System;
using System.Windows.Controls;
using Neonix.Models;
using Neonix.Services;

namespace Neonix.Views
{
    /// <summary>Reactive lighting: colour follows a temperature.</summary>
    public class SyncView : UserControl
    {
        public SyncView()
        {
            var s = SettingsStore.ActiveProfile.Sync;

            var mode = new ComboBox { Width = 220, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            foreach (var m in Enum.GetValues<SyncMode>()) mode.Items.Add(m);
            mode.SelectedItem = s.Mode;
            mode.SelectionChanged += (_, _) =>
            {
                if (mode.SelectedItem is SyncMode m) { s.Mode = m; SettingsStore.Save(); }
            };

            var cool = new TextBox { Text = s.CoolTemp.ToString(), Width = 60, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            var hot = new TextBox { Text = s.HotTemp.ToString(), Width = 60, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            cool.LostFocus += (_, _) => { if (int.TryParse(cool.Text, out var v)) { s.CoolTemp = v; SettingsStore.Save(); } };
            hot.LostFocus += (_, _) => { if (int.TryParse(hot.Text, out var v)) { s.HotTemp = v; SettingsStore.Save(); } };

            var p = new StackPanel();
            p.Children.Add(Ui.Row("Colour follows", mode));
            p.Children.Add(Ui.Row("Cool at (°C)", cool));
            p.Children.Add(Ui.Row("Hot at (°C)", hot));
            p.Children.Add(Ui.Row("Cool colour", Ui.ColorPick(s.CoolColor, h => { s.CoolColor = h; SettingsStore.Save(); })));
            p.Children.Add(Ui.Row("Hot colour", Ui.ColorPick(s.HotColor, h => { s.HotColor = h; SettingsStore.Save(); })));

            Content = Ui.Page(
                Ui.Title("Sync"),
                Ui.Sub("Make your lighting react to the system. Set Colour follows to None to go back to normal effects. " +
                       "Screen ambience and audio visualiser are planned for a later version."),
                Ui.Card(p, "Temperature to colour"));
        }
    }
}
