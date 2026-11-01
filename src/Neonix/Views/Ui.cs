using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Neonix.Views
{
    /// <summary>Small helpers for building the views in code.</summary>
    public static class Ui
    {
        public static Brush B(string key) => (Brush)Application.Current.FindResource(key);

        public static TextBlock Title(string text) => new()
        {
            Text = text, FontSize = 24, FontWeight = FontWeights.SemiBold,
            Foreground = B("Text"), Margin = new Thickness(0, 0, 0, 4),
        };

        public static TextBlock Sub(string text) => new()
        {
            Text = text, FontSize = 13, Foreground = B("Muted"),
            Margin = new Thickness(0, 0, 0, 18), TextWrapping = TextWrapping.Wrap,
        };

        public static TextBlock Label(string text, double size = 13, bool muted = false) => new()
        {
            Text = text, FontSize = size, Foreground = B(muted ? "Muted" : "Text"),
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        };

        public static Border Card(UIElement content, string? heading = null)
        {
            var stack = new StackPanel();
            if (heading != null)
                stack.Children.Add(new TextBlock
                {
                    Text = heading, FontSize = 15, FontWeight = FontWeights.SemiBold,
                    Foreground = B("Accent2"), Margin = new Thickness(0, 0, 0, 10),
                });
            stack.Children.Add(content);
            return new Border
            {
                Background = B("Card"), CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 14), Child = stack,
            };
        }

        public static Button Btn(string text, RoutedEventHandler click)
        {
            var b = new Button { Content = text, Style = (Style)Application.Current.FindResource("NeonButton") };
            b.Click += click;
            return b;
        }

        public static ScrollViewer Page(params UIElement[] children)
        {
            var sp = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };
            foreach (var c in children) sp.Children.Add(c);
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }

        public static Grid Row(string label, UIElement control, double labelWidth = 130)
        {
            var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = Label(label, 13, true);
            Grid.SetColumn(l, 0);
            Grid.SetColumn(control, 1);
            g.Children.Add(l);
            g.Children.Add(control);
            return g;
        }

        public static Slider Slide(double min, double max, double value, Action<double> changed)
        {
            var s = new Slider { Minimum = min, Maximum = max, Value = value, Height = 24, VerticalAlignment = VerticalAlignment.Center };
            s.ValueChanged += (_, e) => changed(e.NewValue);
            return s;
        }

        /// <summary>Hex colour box with a clickable swatch.</summary>
        public static FrameworkElement ColorPick(string hex, Action<string> changed)
        {
            var swatch = new Border
            {
                Width = 34, Height = 26, CornerRadius = new CornerRadius(6), Cursor = System.Windows.Input.Cursors.Hand,
                BorderBrush = B("Muted"), BorderThickness = new Thickness(1),
                Background = Brush(hex),
            };
            var box = new TextBox { Text = hex, Width = 90, Margin = new Thickness(10, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
            void Apply(string h)
            {
                swatch.Background = Brush(h);
                changed(h);
            }
            box.LostFocus += (_, _) => Apply(box.Text);
            swatch.MouseLeftButtonUp += (_, _) =>
            {
                using var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true };
                var c = Services.ColorUtil.Parse(box.Text);
                dlg.Color = System.Drawing.Color.FromArgb((int)(c >> 16) & 255, (int)(c >> 8) & 255, (int)c & 255);
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var h = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                    box.Text = h;
                    Apply(h);
                }
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(swatch);
            sp.Children.Add(box);
            return sp;
        }

        public static Brush Brush(string hex)
        {
            try { return (Brush)new BrushConverter().ConvertFromString(hex)!; }
            catch { return Brushes.White; }
        }
    }
}
