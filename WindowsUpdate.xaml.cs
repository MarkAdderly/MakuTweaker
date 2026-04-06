using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MakuTweakerNew.Properties;
using Microsoft.Win32;

namespace MakuTweakerNew
{
    public partial class WindowsUpdate : System.Windows.Controls.Page
    {
        private bool isLoaded = false;
        private readonly MainWindow mw = (MainWindow)Application.Current.MainWindow;
        private readonly string[] versions = { "1607", "1709", "1809", "1909", "2004", "20H2", "21H2", "22H2", "23H2", "24H2", "25H2" };

        public WindowsUpdate()
        {
            InitializeComponent();
            checkReg();

            int build = WinHelper.GetWindowsBuild();

            if (wu4.SelectedIndex == -1)
            {
                wu4.SelectedIndex = build switch
                {
                    >= 26200 => 10,
                    >= 26100 => 9,
                    >= 22631 => 8,
                    >= 22621 or 19045 => 7,
                    >= 22000 or 19044 => 6,
                    19042 => 5,
                    19041 => 4,
                    18363 => 3,
                    17763 => 2,
                    16299 => 1,
                    _ => 0
                };
            }

            var rules = new (Func<int, bool> Cond, UIElement El)[]
            {
                (b => b > 14393, u1607), (b => b > 16299, u1709), (b => b > 17763, u1809),
                (b => b > 18363, u1909), (b => b > 19041, u2004), (b => b > 19042, u20H2),
                (b => (b > 19044 && b < 22000) || b > 22000, u21H2),
                (b => (b > 19045 && b < 22621) || b > 22621, u22H2),
                (b => b > 22631, u23H2), (b => b > 26100, u24H2)
            };

            foreach (var r in rules)
            {
                if (r.Cond(build))
                {
                    r.El.Visibility = Visibility.Collapsed;
                    r.El.IsEnabled = false;
                }
            }

            LoadLang("");
            isLoaded = true;
        }

        private void Run(string file, string args)
        {
            try
            {
                Process.Start(new ProcessStartInfo(file, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                })?.WaitForExit();
            }
            catch { }
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            string wuPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
            string auPath = wuPath + @"\AU";
            string svc = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\";

            if (wu1.IsOn)
            {
                Registry.SetValue(wuPath, "DoNotConnectToWindowsUpdateInternetLocations", 1, RegistryValueKind.DWord);
                Registry.SetValue(wuPath, "DisableWindowsUpdateAccess", 1, RegistryValueKind.DWord);
                Registry.SetValue(wuPath, "DisableDualScan", 1, RegistryValueKind.DWord);
                Registry.SetValue(auPath, "NoAutoUpdate", 1, RegistryValueKind.DWord);

                Registry.SetValue(svc + "wuauserv", "Start", 4, RegistryValueKind.DWord);
                Registry.SetValue(svc + "UsoSvc", "Start", 4, RegistryValueKind.DWord);
                Registry.SetValue(svc + "WaaSMedicSvc", "Start", 4, RegistryValueKind.DWord);

                Run("schtasks", "/change /tn \"\\Microsoft\\Windows\\WindowsUpdate\\Scheduled Start\" /disable");
                Run("schtasks", "/change /tn \"\\Microsoft\\Windows\\UpdateOrchestrator\\Universal Orchestrator Start\" /disable");

                string hosts = "echo 127.0.0.1 index.wp.microsoft.com >> %windir%\\system32\\drivers\\etc\\hosts && " +
                               "echo 127.0.0.1 update.microsoft.com >> %windir%\\system32\\drivers\\etc\\hosts && " +
                               "echo 127.0.0.1 slscr.update.microsoft.com >> %windir%\\system32\\drivers\\etc\\hosts && " +
                               "echo 127.0.0.1 fe2.update.microsoft.com >> %windir%\\system32\\drivers\\etc\\hosts";
                Run("cmd.exe", $"/c \"{hosts}\"");

                Run("taskkill", "/f /im wuauclt.exe");
                Run("taskkill", "/f /im updatenotificationmgr.exe");
                Run("net", "stop wuauserv /y");
                Run("net", "stop bits /y");
                Run("net", "stop UsoSvc /y");
            }
            else
            {
                Registry.SetValue(auPath, "NoAutoUpdate", 0, RegistryValueKind.DWord);
                Registry.SetValue(wuPath, "DoNotConnectToWindowsUpdateInternetLocations", 0, RegistryValueKind.DWord);
                Registry.SetValue(wuPath, "DisableWindowsUpdateAccess", 0, RegistryValueKind.DWord);
                Registry.SetValue(wuPath, "DisableDualScan", 0, RegistryValueKind.DWord);

                Registry.SetValue(svc + "wuauserv", "Start", 3, RegistryValueKind.DWord);
                Registry.SetValue(svc + "UsoSvc", "Start", 2, RegistryValueKind.DWord);
                Registry.SetValue(svc + "WaaSMedicSvc", "Start", 3, RegistryValueKind.DWord);

                Run("net", "start UsoSvc");
                Run("schtasks", "/change /tn \"\\Microsoft\\Windows\\WindowsUpdate\\Scheduled Start\" /enable");
                Run("schtasks", "/change /tn \"\\Microsoft\\Windows\\UpdateOrchestrator\\Universal Orchestrator Start\" /enable");
                Run(
                    "powershell.exe",
                    "-Command \"(Get-Content $env:windir\\system32\\drivers\\etc\\hosts) | Where-Object { $_ -notmatch 'microsoft.com' } | Set-Content $env:windir\\system32\\drivers\\etc\\hosts\""
                );
            }
            Run("cmd.exe", "/c ipconfig /flushdns");
            mw.RebootNotify(1);
        }

        private void ToggleSwitch_Toggled_1(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            int v = wu2.IsOn ? 1 : 0;
            if (wu2.IsOn)
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 0);
            }
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", v, RegistryValueKind.DWord);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (wu4.SelectedIndex >= 0)
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "TargetReleaseVersionInfo", versions[wu4.SelectedIndex]);

            var wul = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "wu");
            mw.ChSt(wul["status"]["wu4"]);
        }

        private void pause_Click(object sender, RoutedEventArgs e)
        {
            string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
            Registry.SetValue(path, "ActiveHoursStart", 9, RegistryValueKind.DWord);
            Registry.SetValue(path, "ActiveHoursEnd", 2, RegistryValueKind.DWord);

            string[] keys = ["PauseFeatureUpdatesStartTime", "PauseQualityUpdatesStartTime", "PauseUpdatesStartTime"];
            foreach (var k in keys)
                Registry.SetValue(path, k, "2015-01-01T00:00:00Z");

            string[] ends = ["PauseUpdatesExpiryTime", "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesEndTime"];
            foreach (var k in ends)
                Registry.SetValue(path, k, "2077-01-01T00:00:00Z");

            pause.IsEnabled = false;
            mw.ChSt(MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "wu")["status"]["wu5"]);
        }

        private void wu6_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded)
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "ShippedWithReserves", wu6.IsOn ? 0 : 1, RegistryValueKind.DWord);
        }

        private void LoadLang(string lang)
        {
            var code = Settings.Default.lang ?? "en";
            var wu = MainWindow.Localization.LoadLocalization(code, "wu")["main"];
            var b = MainWindow.Localization.LoadLocalization(code, "base")["def"];
            var sr = MainWindow.Localization.LoadLocalization(code, "sr")["main"];

            wu1.Header = wu["wu1"]; wu2.Header = wu["wu3"]; wu6.Header = wu["wu6"];
            pausel.Text = wu["wu5"]; blockL.Text = wu["wu2"]; l7.Text = wu["wu4"];
            pause.Content = wu["wu5b"]; block.Content = wu["wu6b"]; wupd.Content = sr["b4"];

            foreach (var s in new[] { wu1, wu2, wu6 })
            {
                s.OffContent = b["off"];
                s.OnContent = b["on"];
            }
        }

        private void checkReg()
        {
            var LM = Registry.LocalMachine;
            wu1.IsOn = LM.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU")?.GetValue("NoAutoUpdate")?.Equals(1) ?? false;
            wu2.IsOn = LM.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate")?.GetValue("ExcludeWUDriversInQualityUpdate")?.Equals(1) ?? false;
            wu6.IsOn = LM.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager")?.GetValue("ShippedWithReserves")?.Equals(0) ?? false;

            string ver = LM.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate")?.GetValue("TargetReleaseVersionInfo")?.ToString() ?? "";
            wu4.SelectedIndex = Array.IndexOf(versions, ver);

            string pt = LM.OpenSubKey(@"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings")?.GetValue("PauseUpdatesExpiryTime")?.ToString() ?? "";
            pause.IsEnabled = !pt.Contains("2077");
        }

        private void wupd_Click(object sender, RoutedEventArgs e)
        {
            Run("cmd.exe", "/c del /f /s /q %windir%\\SoftwareDistribution\\Download\\*");
            wupd.IsEnabled = false;
        }

        private void wu4_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    }
}