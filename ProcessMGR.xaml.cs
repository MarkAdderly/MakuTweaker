using MakuTweakerNew.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MakuTweakerNew
{
    public class ProcessItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MemoryUsage { get; set; } = "0 MB";
        public override string ToString() => $"{Name} ({MemoryUsage})";
    }

    public partial class ProcessMGR : Page
    {
        private DispatcherTimer? _timer;
        private long _threshold = 524288000;
        private readonly bool isLoaded = false;
        private bool helpVisible = false;

        private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
        {
            "dwm", "msedgewebview2", "startmenuexperiencehost", "taskmgr", "explorer", "system", "idle", "dllhost",
            "smss", "csrss", "wininit", "services", "lsass", "winlogon", "svchost", "fontdrvhost", "sihost",
            "shellexperiencehost", "ctfmon", "runtimebroker", "searchindexer", "searchapp", "wpfsurface",
            "searchhost", "phoneexperiencehost", "textinputhost", "nvidia overlay", "vscodium", "lockapp",
            "shellhost", "systemsettings", "crossdeviceresume", "applicationframehost", "searchui", "gamebar",
            "xboxgamebarwidgets", "xboxpcappft", "icloudservices","nvdisplay.container", "widgets",
            "xboxgamebarspotify", "backgroundtaskhost", "perfwatson2", "msbuild", "crossdeviceservice",
            "bioenrollmenthost", "acergaicameraw", "vmtoolsd", "onedrive", "onedrive.sync.service", "igcctray",
            "igcc", "microsoft.cmdpal.ui", "makutweaker", "msedge", "nvcontainer", "sharex", "everything",
            "firefox", "chrome", "discord"
        };

        public ProcessMGR()
        {
            InitializeComponent();
            LoadLang();
            isLoaded = true;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, ev) => RefreshProcessList();
            RefreshProcessList();
            _timer.Start();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e) => _timer?.Stop();

        private void RefreshProcessList()
        {
            int? selectedId = (ProcessListView.SelectedItem as ProcessItem)?.Id;
            bool onlyHung = OnlyNotRespondingCheck.IsChecked == true;

            if (MemoryLimitCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
                _threshold = long.Parse(item.Tag.ToString()!);

            var heavy = Process.GetProcesses()
                .Where(p =>
                {
                    try
                    {
                        if (p.Id <= 4 || p.SessionId == 0) return false;
                        if (Excluded.Contains(p.ProcessName)) return false;
                        if (p.WorkingSet64 <= _threshold) return false;
                        return !onlyHung || !p.Responding;
                    }
                    catch { return false; }
                })
                .OrderByDescending(p => p.WorkingSet64)
                .Select(p => new ProcessItem
                {
                    Id = p.Id,
                    Name = p.ProcessName,
                    MemoryUsage = p.WorkingSet64 >= 1073741824
                        ? $"{p.WorkingSet64 / 1073741824.0:F1} GB"
                        : $"{p.WorkingSet64 / 1048576.0:F1} MB"
                }).ToList();

            ProcessListView.ItemsSource = heavy;
            if (selectedId.HasValue)
                ProcessListView.SelectedItem = heavy.FirstOrDefault(x => x.Id == selectedId);
        }

        private void KillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessListView.SelectedItem is not ProcessItem selected) return;
            try
            {
                foreach (var p in Process.GetProcessesByName(selected.Name))
                    try { p.Kill(); } catch { }
                RefreshProcessList();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            if (isLoaded) RefreshProcessList();
        }

        private void ProcessListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete) KillProcess_Click(sender, e);
        }

        private void LoadLang()
        {
            var lang = Settings.Default.lang ?? "en";
            var m = MainWindow.Localization.LoadLocalization(lang, "pmgr")["main"];
            var t = MainWindow.Localization.LoadLocalization(lang, "tooltips")["main"];

            mgr_tooltip.Content = t["MakuTweakerProcessMGR1"];
            label.Text = m["label"];
            KillBtn.Content = m["endprocess"];
            OnlyNotRespondingCheck.Content = m["onlyfrozen"];

            string[] keys = { "from50mb", "from100mb", "from300mb", "from500mb", "from1000mb", "from2000mb" };
            for (int i = 0; i < keys.Length; i++)
                if (MemoryLimitCombo.Items[i] is ComboBoxItem item) item.Content = m[keys[i]];

            if (ProcessListView.Resources["ItemContextMenu"] is ItemsControl menu && menu.Items.Count >= 2)
            {
                ((MenuItem)menu.Items[0]).Header = m["endprocess"];
                ((MenuItem)menu.Items[1]).Header = m["location"];
            }
        }

        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessListView.SelectedItem is not ProcessItem s) return;
            try
            {
                string? path = Process.GetProcessById(s.Id).MainModule?.FileName;
                if (!string.IsNullOrEmpty(path)) Process.Start("explorer.exe", $"/select, \"{path}\"");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }

        private async void InfoBtn_Click(object sender, RoutedEventArgs e)
        {
            buttontooltip.IsEnabled = false;
            helpVisible = !helpVisible;

            if (helpVisible)
            {
                HelpText.Text = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "tooltips")["main"]["MakuTweakerProcessMGR"];
                Animate(true);
                buttontooltip.Content = "←";
            }
            else
            {
                Animate(false);
                buttontooltip.Content = "?";
            }

            await Task.Delay(200);
            buttontooltip.IsEnabled = true;
        }

        private void Animate(bool show)
        {
            double h = ContentHost.ActualHeight;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            void Start(IAnimatable obj, DependencyProperty prop, double to, double? from = null)
            {
                var a = new DoubleAnimation { To = to, Duration = TimeSpan.FromSeconds(0.25), EasingFunction = ease };
                if (from.HasValue) a.From = from.Value;
                obj.BeginAnimation(prop, a);
            }

            if (show)
            {
                HelpContent.Visibility = Visibility.Visible;
                MainContent.IsHitTestVisible = false;
                ControlPanel.Visibility = Visibility.Collapsed;

                Start(MainTransform, TranslateTransform.YProperty, -h);
                Start(MainContent, OpacityProperty, 0);
                Start(HelpTransform, TranslateTransform.YProperty, 0, h);
            }
            else
            {
                MainContent.IsHitTestVisible = true;
                ControlPanel.Visibility = Visibility.Visible;

                Start(MainTransform, TranslateTransform.YProperty, 0);
                var anim = new DoubleAnimation(h, TimeSpan.FromSeconds(0.25)) { EasingFunction = ease };
                anim.Completed += (s, e) => { HelpContent.Visibility = Visibility.Collapsed; Start(MainContent, OpacityProperty, 1, 0); };
                HelpTransform.BeginAnimation(TranslateTransform.YProperty, anim);
            }
        }
    }
}