using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Neonix.Models;
using Neonix.Services;

namespace Neonix.Views
{
    public class LightingView : UserControl
    {
        const int PreviewLeds = 48;

        static readonly Dictionary<EffectType, string> Descriptions = new()
        {
            [EffectType.Static] = "One solid colour (Colour 1).",
            [EffectType.Breathing] = "Colour 1 fades in and out.",
            [EffectType.Rainbow] = "A rainbow that flows along every strip.",
            [EffectType.Wave] = "A smooth wave between Colour 1 and Colour 2.",
            [EffectType.ColorCycle] = "Every light slowly cycles through all colours together.",
            [EffectType.Fire] = "Flickering flames rising along the strip.",
            [EffectType.Comet] = "A comet of Colour 1 with a Colour 2 tail races along the strip.",
            [EffectType.Sparkle] = "Colour 1 glow with Colour 2 twinkles popping at random.",
            [EffectType.ScreenAmbience] = "Lights match the colours on your main monitor, left to right. " +
                "Works best with windowed or borderless games; some fullscreen-exclusive games show black.",
            [EffectType.AudioSpectrum] = "Lights dance to whatever is playing on your PC, low notes to high notes along the strip.",
            [EffectType.AudioPulse] = "Lights pulse with the bass of whatever is playing on your PC.",
            [EffectType.Off] = "Lights off.",
        };

        readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
        readonly SolidColorBrush[] _cells = new SolidColorBrush[PreviewLeds];

        public LightingView()
        {
            var l = SettingsStore.ActiveProfile.Lighting;

            // ───── live preview ─────
            var preview = new UniformGrid { Rows = 1, Height = 30 };
            for (int i = 0; i < PreviewLeds; i++)
            {
                _cells[i] = new SolidColorBrush(Colors.Black);
                preview.Children.Add(new Border { Background = _cells[i], CornerRadius = new CornerRadius(3), Margin = new Thickness(1) });
            }
            _timer.Tick += (_, _) =>
            {
                var frame = App.Effects.Preview(PreviewLeds);
                for (int i = 0; i < PreviewLeds; i++)
                {
                    var c = frame[i];
                    _cells[i].Color = Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c);
                }
            };
            Loaded += (_, _) => _timer.Start();
            Unloaded += (_, _) => _timer.Stop();

            // ───── controls ─────
            var description = Ui.Label(Descriptions[l.Effect], 12, true);
            description.Margin = new Thickness(0, 4, 0, 10);

            var effect = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var e in Enum.GetValues<EffectType>()) effect.Items.Add(e);
            effect.SelectedItem = l.Effect;

            var sensitivityRow = Ui.Row("Audio sensitivity", Ui.Slide(0, 100, l.Sensitivity, v => { l.Sensitivity = (int)v; SettingsStore.Save(); }));
            var smoothingRow = Ui.Row("Screen smoothing", Ui.Slide(0, 95, l.Smoothing, v => { l.Smoothing = (int)v; SettingsStore.Save(); }));

            void ApplyVisibility(EffectType t)
            {
                sensitivityRow.Visibility = t is EffectType.AudioSpectrum or EffectType.AudioPulse ? Visibility.Visible : Visibility.Collapsed;
                smoothingRow.Visibility = t == EffectType.ScreenAmbience ? Visibility.Visible : Visibility.Collapsed;
                description.Text = Descriptions[t];
                Ui.PopIn(description);
            }
            ApplyVisibility(l.Effect);

            effect.SelectionChanged += (_, _) =>
            {
                if (effect.SelectedItem is EffectType t)
                {
                    l.Effect = t;
                    SettingsStore.Save();
                    ApplyVisibility(t);
                }
            };

            var panel = new StackPanel();
            panel.Children.Add(Ui.Row("Effect", effect));
            panel.Children.Add(description);
            panel.Children.Add(Ui.Row("Colour 1", Ui.ColorPick(l.Color1, h => { l.Color1 = h; SettingsStore.Save(); })));
            panel.Children.Add(Ui.Row("Colour 2", Ui.ColorPick(l.Color2, h => { l.Color2 = h; SettingsStore.Save(); })));
            panel.Children.Add(Ui.Row("Speed", Ui.Slide(0, 100, l.Speed, v => { l.Speed = (int)v; SettingsStore.Save(); })));
            panel.Children.Add(Ui.Row("Brightness", Ui.Slide(0, 100, l.Brightness, v => { l.Brightness = (int)v; SettingsStore.Save(); })));
            panel.Children.Add(sensitivityRow);
            panel.Children.Add(smoothingRow);

            Content = Ui.Page(
                Ui.Title("Lighting"),
                Ui.Sub("Effects apply to every RGB device that has lighting switched on in the Devices tab. " +
                       "If Sync is turned on it takes over from the effect."),
                Ui.Card(preview, "Live preview"),
                Ui.Card(panel, $"Profile: {SettingsStore.Current.ActiveProfile}"));
        }
    }
}
