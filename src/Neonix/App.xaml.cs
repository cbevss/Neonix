using System;
using System.Linq;
using System.Windows;
using Neonix.Services;

namespace Neonix
{
    public partial class App : Application
    {
        public static HardwareService Hardware { get; private set; } = null!;
        public static RgbService Rgb { get; private set; } = null!;
        public static EffectEngine Effects { get; private set; } = null!;
        ScreenService _screen = null!;
        AudioService _audio = null!;
        FanCurveService _fans = null!;
        ProfileWatcher _watcher = null!;
        MainWindow _window = null!;
        System.Windows.Forms.NotifyIcon? _tray;

        public static void RunOnUi(Action a) => Current?.Dispatcher.BeginInvoke(a);

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (_, ex) =>
            {
                MessageBox.Show(ex.Exception.Message, "Neonix error");
                ex.Handled = true;
            };

            SettingsStore.Load();
            AccountService.Load();
            Hardware = new HardwareService();
            Rgb = new RgbService();
            _screen = new ScreenService();
            _audio = new AudioService();
            Effects = new EffectEngine(Rgb, Hardware, _screen, _audio);
            _fans = new FanCurveService(Hardware);
            _watcher = new ProfileWatcher();

            _window = new MainWindow();
            SetupTray();
            if (!e.Args.Contains("--minimized")) _window.Show();

            await Rgb.StartAsync();
        }

        void SetupTray()
        {
            _tray = new System.Windows.Forms.NotifyIcon
            {
                Text = "Neonix",
                Visible = true,
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            };
            _tray.DoubleClick += (_, _) => ShowWindow();
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open Neonix", null, (_, _) => ShowWindow());
            var profiles = new System.Windows.Forms.ToolStripMenuItem("Profile");
            menu.Opening += (_, _) =>
            {
                profiles.DropDownItems.Clear();
                foreach (var p in SettingsStore.VisibleProfiles())
                {
                    var name = p.Name;
                    var item = new System.Windows.Forms.ToolStripMenuItem(name)
                    { Checked = name == SettingsStore.Current.ActiveProfile };
                    item.Click += (_, _) => RunOnUi(() => SettingsStore.SwitchProfile(name));
                    profiles.DropDownItems.Add(item);
                }
            };
            menu.Items.Add(profiles);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => Quit());
            _tray.ContextMenuStrip = menu;
        }

        void ShowWindow()
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        }

        public void Quit()
        {
            SettingsStore.Save();
            _tray?.Dispose();
            _watcher.Dispose();
            _fans.Dispose();
            Effects.Dispose();
            _screen.Dispose();
            _audio.Dispose();
            Rgb.Dispose();
            Hardware.Dispose();
            Shutdown();
        }
    }
}
