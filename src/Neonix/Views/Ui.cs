using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Neonix.Views
{
    /// <summary>Small helpers for building the views in code.</summary>
    public static class Ui
    {
        public static Brush B(string key) => (Brush)Application.Current.FindResource(key);

        static Color C(string key) => (Color)Application.Current.FindResource(key);

        static readonly Color LineColor = Color.FromRgb(0x2A, 0x26, 0x50);

        static Color FromPacked(uint u) => Color.FromRgb((byte)(u >> 16), (byte)(u >> 8), (byte)u);

        // ───────── text ─────────

        public static TextBlock Title(string text) => new()
        {
            Text = text, FontSize = 28, FontWeight = FontWeights.Bold,
            Foreground = B("Text"), Margin = new Thickness(0, 0, 0, 4),
        };

        public static TextBlock Sub(string text) => new()
        {
            Text = text, FontSize = 13, Foreground = B("Muted"),
            Margin = new Thickness(0, 0, 0, 20), TextWrapping = TextWrapping.Wrap, LineHeight = 20,
        };

        public static TextBlock Label(string text, double size = 13, bool muted = false) => new()
        {
            Text = text, FontSize = size, Foreground = B(muted ? "Muted" : "Text"),
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        };

        // ───────── cards ─────────

        public static Border Card(UIElement content, string? heading = null)
        {
            var stack = new StackPanel();
            if (heading != null)
                stack.Children.Add(new TextBlock
                {
                    Text = heading, FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = B("Accent2"), Margin = new Thickness(0, 0, 0, 12),
                });
            stack.Children.Add(content);

            var border = new SolidColorBrush(LineColor);
            var card = new Border
            {
                Background = B("Card"), CornerRadius = new CornerRadius(14),
                BorderBrush = border, BorderThickness = new Thickness(1),
                Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 14), Child = stack,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35, Color = Colors.Black },
            };

            // Border glows towards the accent colour when hovered.
            card.MouseEnter += (_, _) => border.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(C("PurpleColor"), TimeSpan.FromMilliseconds(180)));
            card.MouseLeave += (_, _) => border.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(LineColor, TimeSpan.FromMilliseconds(260)));
            return card;
        }

        public static Button Btn(string text, RoutedEventHandler click)
        {
            var b = new Button { Content = text, Style = (Style)Application.Current.FindResource("NeonButton") };
            b.Click += click;
            return b;
        }

        // ───────── pages ─────────

        /// <summary>A scrolling page whose children slide and fade in one after another.</summary>
        public static ScrollViewer Page(params UIElement[] children)
        {
            var sp = new StackPanel { Margin = new Thickness(34, 28, 30, 28) };
            int i = 0;
            foreach (var c in children)
            {
                sp.Children.Add(c);
                Reveal(c, i++);
            }
            return new SmoothScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
        }

        /// <summary>Slide-up + fade-in, staggered by index.</summary>
        public static void Reveal(UIElement e, int index)
        {
            var tt = new TranslateTransform(0, 18);
            e.RenderTransform = tt;
            e.Opacity = 0;
            var delay = TimeSpan.FromMilliseconds(40 + index * 55);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            e.Dispatcher.BeginInvoke(new Action(() =>
            {
                e.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = delay });
                tt.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(380)) { BeginTime = delay, EasingFunction = ease });
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public static Grid Row(string label, UIElement control, double labelWidth = 130)
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
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
            var s = new Slider { Minimum = min, Maximum = max, Value = value, VerticalAlignment = VerticalAlignment.Center };
            s.ValueChanged += (_, e) => changed(e.NewValue);
            return s;
        }

        /// <summary>Hex colour box with a clickable swatch.</summary>
        public static FrameworkElement ColorPick(string hex, Action<string> changed)
        {
            var swatchBrush = new SolidColorBrush(FromPacked(Services.ColorUtil.Parse(hex)));
            var swatch = new Border
            {
                Width = 38, Height = 30, CornerRadius = new CornerRadius(8), Cursor = System.Windows.Input.Cursors.Hand,
                BorderBrush = B("Line"), BorderThickness = new Thickness(2),
                Background = swatchBrush,
            };
            var box = new TextBox { Text = hex, Width = 100, Margin = new Thickness(12, 0, 0, 0) };

            void Apply(string h)
            {
                var to = FromPacked(Services.ColorUtil.Parse(h));
                swatchBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, TimeSpan.FromMilliseconds(250)));
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

        // ───────── animated bits ─────────

        /// <summary>A thin rainbow bar whose gradient keeps rotating.</summary>
        public static Border RgbStrip(double height = 4)
        {
            var rot = new RotateTransform(0, 0.5, 0.5);
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5),
                SpreadMethod = GradientSpreadMethod.Reflect, RelativeTransform = rot,
            };
            foreach (var (c, o) in new[] { ("#FF2EAA", 0.0), ("#8C3CFF", 0.35), ("#00E1FF", 0.7), ("#00FF9C", 1.0) })
                brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c), o));

            rot.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromSeconds(8)) { RepeatBehavior = RepeatBehavior.Forever });

            return new Border { Height = height, CornerRadius = new CornerRadius(height / 2), Background = brush, Margin = new Thickness(0, 0, 0, 22) };
        }

        /// <summary>A progress bar that glides to each new value.</summary>
        public sealed class Meter : Grid
        {
            readonly ScaleTransform _scale = new(0, 1);

            public Meter(double height = 8)
            {
                Height = height;
                Children.Add(new Border { CornerRadius = new CornerRadius(height / 2), Background = (Brush)Application.Current.FindResource("Line") });
                Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(height / 2),
                    Background = (Brush)Application.Current.FindResource("NeonGradient"),
                    RenderTransformOrigin = new Point(0, 0.5),
                    RenderTransform = _scale,
                });
            }

            public void Set(double percent)
            {
                double to = Math.Clamp(percent / 100.0, 0, 1);
                _scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(to, TimeSpan.FromMilliseconds(600)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
        }

        /// <summary>Blue when cool, pink when hot.</summary>
        public static Brush TempBrush(double temp)
        {
            double k = Math.Clamp((temp - 35) / 50.0, 0, 1);
            var cool = Color.FromRgb(0, 225, 255);
            var hot = Color.FromRgb(255, 46, 120);
            return new SolidColorBrush(Color.FromRgb(
                (byte)(cool.R + (hot.R - cool.R) * k),
                (byte)(cool.G + (hot.G - cool.G) * k),
                (byte)(cool.B + (hot.B - cool.B) * k)));
        }
    }
}
