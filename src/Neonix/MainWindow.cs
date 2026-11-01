using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Neonix.Views;

namespace Neonix
{
    /// <summary>Main window: a side panel of tabs on the left, the selected view on the right.</summary>
    public class MainWindow : Window
    {
        readonly ContentControl _content = new();
        readonly Dictionary<string, Func<UIElement>> _tabs = new();

        public MainWindow()
        {
            Title = "Neonix";
            Width = 1100;
            Height = 720;
            MinWidth = 900;
            MinHeight = 560;
            Background = Ui.B("Bg");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            _tabs["Dashboard"] = () => new DashboardView();
            _tabs["Devices"] = () => new DevicesView();
            _tabs["Lighting"] = () => new LightingView();
            _tabs["Sync"] = () => new SyncView();
            _tabs["Fans"] = () => new FansView();
            _tabs["Profiles"] = () => new ProfilesView();
            _tabs["Settings"] = () => new SettingsView();

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var side = new Border { Background = Ui.B("Panel") };
            var nav = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };

            var logo = new TextBlock
            {
                Text = "NEONIX", FontSize = 22, FontWeight = FontWeights.Bold,
                Margin = new Thickness(22, 0, 0, 22),
                Foreground = new LinearGradientBrush(
                    (Color)Application.Current.FindResource("AccentColor"),
                    (Color)Application.Current.FindResource("Accent2Color"), 0),
            };
            nav.Children.Add(logo);

            bool first = true;
            foreach (var name in _tabs.Keys)
            {
                var rb = new RadioButton
                {
                    Content = name, GroupName = "nav",
                    Style = (Style)Application.Current.FindResource("NavButton"),
                    IsChecked = first,
                };
                var key = name;
                rb.Checked += (_, _) => _content.Content = _tabs[key]();
                nav.Children.Add(rb);
                first = false;
            }
            side.Child = nav;
            Grid.SetColumn(side, 0);

            Grid.SetColumn(_content, 1);
            root.Children.Add(side);
            root.Children.Add(_content);
            Content = root;

            _content.Content = _tabs["Dashboard"]();
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
