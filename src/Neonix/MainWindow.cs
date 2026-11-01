using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Neonix.Views;

namespace Neonix
{
    /// <summary>Main window: a collapsible side panel of tabs on the left, the selected view on the right.</summary>
    public class MainWindow : Window
    {
        const double SideWidth = 224;
        const double RailWidth = 58;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        readonly ContentControl _content = new();
        readonly List<(string Name, string Icon, Func<UIElement> Make)> _tabs = new();
        readonly Border _indicator;
        readonly Grid _navHost = new();
        readonly Border _side = new();
        readonly Grid _contentHost = new();
        readonly Button _openButton;
        readonly Button _hideButton;
        bool _hidden;

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
            _tabs.Add(("Account", "", () => new AccountView()));
            _tabs.Add(("Settings", "", () => new SettingsView()));

            var root = new Grid();

            // ───── side panel ─────
            _side.Width = SideWidth;
            _side.HorizontalAlignment = HorizontalAlignment.Left;
            _side.Background = Ui.B("Panel");
            _side.BorderBrush = Ui.B("Line");
            _side.BorderThickness = new Thickness(0, 0, 1, 0);
            _side.RenderTransform = new TranslateTransform(0, 0);

            var sideStack = new DockPanel();

            var logo = new TextBlock
            {
                Text = "NEONIX", FontSize = 24, FontWeight = FontWeights.Black,
                Margin = new Thickness(28, 30, 0, 4),
                Foreground = Ui.AnimatedGradient(7), // the gradient slowly rotates
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

            // Status + version at the bottom.
            var dotBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB020"));
            var dot = new Ellipse { Width = 8, Height = 8, Fill = dotBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            dot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromSeconds(1.2))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            var statusText = new TextBlock { Text = "RGB driver starting…", FontSize = 11, Foreground = Ui.B("Muted"), VerticalAlignment = VerticalAlignment.Center };
            var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(29, 0, 0, 6) };
            status.Children.Add(dot);
            status.Children.Add(statusText);
            var version = new TextBlock { Text = "v0.1.0", FontSize = 11, Foreground = Ui.B("Muted"), Margin = new Thickness(29, 0, 0, 18) };
            var bottom = new StackPanel();
            bottom.Children.Add(status);
            bottom.Children.Add(version);
            DockPanel.SetDock(bottom, Dock.Bottom);
            sideStack.Children.Add(bottom);

            var statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            statusTimer.Tick += (_, _) =>
            {
                bool ok = App.Rgb.Connected;
                statusText.Text = ok ? "RGB driver connected" : "RGB driver offline";
                var to = (Color)ColorConverter.ConvertFromString(ok ? "#00FF9C" : "#FFB020");
                dotBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, TimeSpan.FromMilliseconds(400)));
            };
            statusTimer.Start();

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

            // Hide button, top right of the side panel.
            _hideButton = new Button
            {
                Content = "", Style = (Style)Application.Current.FindResource("IconButton"),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 24, 12, 0), ToolTip = "Hide sidebar (Ctrl+B)",
            };
            _hideButton.Click += (_, _) => SetSidebar(true, true);

            var sideGrid = new Grid();
            sideGrid.Children.Add(sideStack);
            sideGrid.Children.Add(_hideButton);
            _side.Child = sideGrid;

            bool first = true;
            foreach (var tab in _tabs)
            {
                var icon = new TextBlock
                {
                    Text = tab.Icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    FontSize = 16, Width = 30, VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.35, 0.5),
                    RenderTransform = new ScaleTransform(1, 1),
                };
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(icon);
                row.Children.Add(new TextBlock { Text = tab.Name, VerticalAlignment = VerticalAlignment.Center });

                var rb = new RadioButton
                {
                    Content = row, GroupName = "nav", IsChecked = first,
                    Style = (Style)Application.Current.FindResource("NavButton"),
                };
                var make = tab.Make;
                rb.Checked += (_, _) =>
                {
                    _content.Content = make();
                    MoveIndicator(rb, animate: true);
                    Bounce(icon);
                };
                if (first) rb.Loaded += (_, _) => MoveIndicator(rb, animate: false);
                nav.Children.Add(rb);
                first = false;
            }

            // ───── content area ─────
            _contentHost.Margin = new Thickness(SideWidth, 0, 0, 0);
            _contentHost.Children.Add(_content);

            // Reopens the sidebar; fades in once the sidebar has slid away.
            _openButton = new Button
            {
                Content = "", Style = (Style)Application.Current.FindResource("IconButton"),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12, 24, 0, 0), Opacity = 0, IsHitTestVisible = false,
                ToolTip = "Show sidebar (Ctrl+B)",
            };
            _openButton.Click += (_, _) => SetSidebar(false, true);

            root.Children.Add(_contentHost);
            root.Children.Add(_side);
            root.Children.Add(_openButton);
            Content = root;

            _content.Content = _tabs[0].Make();

            // Ctrl+B toggles the sidebar.
            InputBindings.Add(new KeyBinding(new RelayCommand(() => SetSidebar(!_hidden, true)), Key.B, ModifierKeys.Control));

            if (Services.SettingsStore.Current.SidebarHidden) SetSidebar(true, false);

            // Window fades in.
            Opacity = 0;
            Loaded += (_, _) => BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)));
        }

        // ───────── sidebar slide ─────────

        void SetSidebar(bool hidden, bool animate)
        {
            _hidden = hidden;
            Services.SettingsStore.Current.SidebarHidden = hidden;
            Services.SettingsStore.Save();

            double x = hidden ? -SideWidth : 0;
            var margin = new Thickness(hidden ? RailWidth : SideWidth, 0, 0, 0);
            var tt = (TranslateTransform)_side.RenderTransform;

            if (!animate)
            {
                tt.X = x;
                _contentHost.Margin = margin;
                _openButton.Opacity = hidden ? 1 : 0;
                _openButton.IsHitTestVisible = hidden;
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var duration = TimeSpan.FromMilliseconds(380);

            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, duration) { EasingFunction = ease });
            _contentHost.BeginAnimation(MarginProperty, new ThicknessAnimation(margin, duration) { EasingFunction = ease });

            _openButton.IsHitTestVisible = hidden;
            _openButton.BeginAnimation(OpacityProperty, new DoubleAnimation(hidden ? 1 : 0, TimeSpan.FromMilliseconds(240))
            { BeginTime = hidden ? TimeSpan.FromMilliseconds(200) : TimeSpan.Zero });
        }

        static void Bounce(TextBlock icon)
        {
            if (icon.RenderTransform is not ScaleTransform st) return;
            var kf = new DoubleAnimationUsingKeyFrames();
            kf.KeyFrames.Add(new EasingDoubleKeyFrame(1.35, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
            kf.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)),
                new BounceEase { EasingMode = EasingMode.EaseOut, Bounces = 2, Bounciness = 3 }));
            st.BeginAnimation(ScaleTransform.ScaleXProperty, kf);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, kf);
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

        sealed class RelayCommand : ICommand
        {
            readonly Action _run;
            public RelayCommand(Action run) => _run = run;
            public event EventHandler? CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _run();
        }
    }
}
