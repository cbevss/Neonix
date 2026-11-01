using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Animation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Neonix.Views;

namespace Neonix
{
    /// <summary>Main window: a side panel of tabs on the left, the selected view on the right.</summary>
    public class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        readonly ContentControl _content = new();
        readonly List<(string Name, string Icon, Func<UIElement> Make)> _tabs = new();
        readonly Border _indicator;
        readonly Grid _navHost = new();

        public MainWindow()
        {
            Title = "Neonix";
            Width = 1120;
            Height = 740;
            MinWidth = 920;
            MinHeight = 580;
            Background = Ui.B("Bg");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            try
            {
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                    new Uri("pack://application:,,,/Assets/neonix.ico", UriKind.Absolute));
            }
            catch { /* cosmetic only */ }

            // Segoe MDL2 Assets glyphs
            _tabs.Add(("Dashboard", "", () => new DashboardView()));
            _tabs.Add(("Devices", "", () => new DevicesView()));
            _tabs.Add(("Lighting", "", () => new LightingView()));
            _tabs.Add(("Sync", "", () => new SyncView()));
            _tabs.Add(("Fans", "", () => new FansView()));
            _tabs.Add(("Profiles", "", () => new ProfilesView()));
            _tabs.Add(("Settings", "", () => new SettingsView()));

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ───── side panel ─────
            var side = new Border
            {
                Background = Ui.B("Panel"),
                BorderBrush = Ui.B("Line"),
                BorderThickness = new Thickness(0, 0, 1, 0),
            };
            var sideStack = new DockPanel();

            var logo = new TextBlock
            {
                Text = "NEONIX", FontSize = 24, FontWeight = FontWeights.Black,
                Margin = new Thickness(28, 30, 0, 4),
                Foreground = Ui.B("NeonGradient"),
            };
            var tag = new TextBlock
            {
                Text = "RGB  ·  FANS  ·  CONTROL", FontSize = 9.5, Foreground = Ui.B("Muted"),
                Margin = new Thickness(29, 0, 0, 26),
            };
            var header = new StackPanel();
            header.Children.Add(logo);
            header.Children.Add(tag);
            DockPanel.SetDock(header, Dock.Top);
            sideStack.Children.Add(header);

            var version = new TextBlock
            {
                Text = "v0.1.0", FontSize = 11, Foreground = Ui.B("Muted"), Margin = new Thickness(29, 0, 0, 18),
            };
            DockPanel.SetDock(version, Dock.Bottom);
            sideStack.Children.Add(version);

            var nav = new StackPanel();
            _navHost.Children.Add(nav);

            // The accent bar that slides to the selected tab.
            _indicator = new Border
            {
                Width = 4, Height = 24, CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 0, 0, 0),
                Background = Ui.B("NeonGradient"),
                RenderTransform = new TranslateTransform(0, 0),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = (Color)ColorConverter.ConvertFromString("#FF2EAA"), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.9 },
            };
            _navHost.Children.Add(_indicator);
            sideStack.Children.Add(_navHost);
            side.Child = sideStack;

            bool first = true;
            foreach (var tab in _tabs)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock
                {
                    Text = tab.Icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    FontSize = 16, Width = 30, VerticalAlignment = VerticalAlignment.Center,
                });
                row.Children.Add(new TextBlock { Text = tab.Name, VerticalAlignment = VerticalAlignment.Center });

                var rb = new RadioButton
                {
                    Content = row, GroupName = "nav", IsChecked = first,
                    Style = (Style)Application.Current.FindResource("NavButton"),
                };
                var make = tab.Make;
                rb.Checked += (_, _) =>
                {
                    ShowPage(make());
                    MoveIndicator(rb, animate: true);
                };
                if (first) rb.Loaded += (_, _) => MoveIndicator(rb, animate: false);
                nav.Children.Add(rb);
                first = false;
            }

            Grid.SetColumn(side, 0);
            Grid.SetColumn(_content, 1);
            root.Children.Add(side);
            root.Children.Add(_content);
            Content = root;

            _content.Content = _tabs[0].Make();

            // Window fades in.
            Opacity = 0;
            Loaded += (_, _) => BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)));
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                // Dark title bar on Windows 10/11.
                int on = 1;
                DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref on, sizeof(int));
            }
            catch { /* cosmetic only */ }
        }

        void ShowPage(UIElement page)
        {
            _content.Content = page;
        }

        void MoveIndicator(FrameworkElement target, bool animate)
        {
            try
            {
                var y = target.TranslatePoint(new Point(0, 0), _navHost).Y + (target.ActualHeight - _indicator.Height) / 2;
                var tt = (TranslateTransform)_indicator.RenderTransform;
                if (!animate) { tt.Y = y; return; }
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, TimeSpan.FromMilliseconds(320))
                { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } });
            }
            catch { }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Services.SettingsStore.Current.MinimizeToTray)
            {
                e.Cancel = true; // keep running in the tray so lighting and fan curves stay active
                Hide();
            }
            else
            {
                ((App)Application.Current).Quit();
            }
            base.OnClosing(e);
        }
    }
}
