using MakuTweakerNew.Properties;
using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MakuTweakerNew
{
    public partial class Perf : Page
    {
        private const string SUB_PROCESSOR = "54533251-82be-4824-96c1-47b60b740d00";
        private const string THROTTLE_MAX = "bc5038f7-23e0-4960-96da-33abaf5935ec";
        private MainWindow mw = (MainWindow)Application.Current.MainWindow;

        public Perf()
        {
            InitializeComponent();
            LoadLang();
            Loaded += Perf_Loaded;
        }

        private (int ExitCode, string Output, string Error) RunPowerCfg(string args)
        {
            var psi = new ProcessStartInfo("powercfg", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return (-1, "", "");

            string outStr = p.StandardOutput.ReadToEnd();
            string errStr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            return (p.ExitCode, outStr, errStr);
        }

        private void LoadLang()
        {
            var m = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "perfor")["main"];
            label.Text = m["label"];
            apply.Content = m["applyb"];
            minpercent.Content = m["minb"];
            maxpercent.Content = m["maxb"];
            infolabel.Text = m["info"];
        }

        private void ApplyThrottle(int percent)
        {
            if (percent < 1 || percent > 100) return;

            string scheme = GetActiveScheme();
            var r1 = RunPowerCfg($"/setdcvalueindex {scheme} SUB_PROCESSOR PROCTHROTTLEMAX {percent}");
            var r2 = RunPowerCfg($"/setacvalueindex {scheme} SUB_PROCESSOR PROCTHROTTLEMAX {percent}");
            var r3 = RunPowerCfg($"/setactive {scheme}");

            if (r1.ExitCode == 0 && r2.ExitCode == 0 && r3.ExitCode == 0)
            {
                percentslider.Value = percent / 10.0;
                ShowThrottleNotification(percent);
            }
            else
            {
                MessageBox.Show($"{r1.Error}\n{r2.Error}\n{r3.Error}", "MakuTweaker Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void apply_Click(object sender, RoutedEventArgs e) => ApplyThrottle((int)percentslider.Value * 10);

        private void Perf_Loaded(object sender, RoutedEventArgs e)
        {
            RunPowerCfg("-attributes SUB_PROCESSOR PROCTHROTTLEMAX -ATTRIB_HIDE");
            int percent = GetCurrentThrottlePercent();
            percentslider.Value = (percent >= 1 && percent <= 100) ? percent / 10.0 : 10;
        }

        private string GetActiveScheme()
        {
            var res = RunPowerCfg("/getactivescheme");
            var match = Regex.Match(res.Output, @"GUID:\s+([a-fA-F0-9\-]+)");
            return match.Success ? match.Groups[1].Value : "SCHEME_CURRENT";
        }

        private int GetCurrentThrottlePercent()
        {
            var res = RunPowerCfg($"/query {GetActiveScheme()} {SUB_PROCESSOR} {THROTTLE_MAX}");
            var matches = Regex.Matches(res.Output, @"0x([0-9A-Fa-f]+)");
            if (matches.Count == 0) return -1;

            return Convert.ToInt32(matches[^1].Groups[1].Value, 16);
        }

        private void minpercent_Click(object sender, RoutedEventArgs e) => ApplyThrottle(10);

        private void maxpercent_Click(object sender, RoutedEventArgs e) => ApplyThrottle(100);

        private void ShowThrottleNotification(int percent)
        {
            var m = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "perfor")["main"];
            var tray = new TaskbarIcon
            {
                Icon = new Icon(mw.GetType().Assembly.GetManifestResourceStream("MakuTweakerNew.MakuT.ico")!),
                ToolTipText = "MakuTweaker"
            };

            tray.ShowBalloonTip("MakuTweaker", $"{m["flyout"]}{percent}%", BalloonIcon.Info);
            Task.Delay(8000).ContinueWith(_ => tray.Dispatcher.Invoke(tray.Dispose));
        }
    }
}