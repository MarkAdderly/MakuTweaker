using MakuTweakerNew.Properties;
using Microsoft.Win32;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows;

namespace MakuTweakerNew
{
    public partial class SysAndRec : System.Windows.Controls.Page
    {
        private readonly bool isLoaded = false;
        private readonly MainWindow mw = (MainWindow)Application.Current.MainWindow;

        public SysAndRec()
        {
            InitializeComponent();
            checkReg();
            LoadLang();
            isLoaded = true;

            if (!HasBattery())
            {
                batterylabel.Visibility = report.Visibility = Visibility.Collapsed;
            }
        }

        private bool HasBattery()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                using var results = searcher.Get();
                return results.Count > 0;
            }
            catch { return false; }
        }

        private void Run(string file, string args, bool wait = false)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args) { CreateNoWindow = true, UseShellExecute = false });
                if (wait) p?.WaitForExit();
            }
            catch { }
        }

        private string GetOutput(string file, string args)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(file, args) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                return p?.StandardOutput.ReadToEnd().ToLower() ?? "";
            }
            catch { return ""; }
        }

        private void sfc_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("cmd.exe", "/k sfc /scannow");
            mw.RebootNotify(3);
            sfc.IsEnabled = false;
        }

        private void dism_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("cmd.exe", "/k DISM /Online /Cleanup-Image /RestoreHealth");
            mw.RebootNotify(3);
            dism.IsEnabled = false;
        }

        private void temp_Click(object sender, RoutedEventArgs e)
        {
            Run("cmd.exe", "/c del /q /f %temp%");
            temp.IsEnabled = false;
        }

        private void LoadLang()
        {
            var lang = Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(lang, "sr")["main"];
            var bDef = MainWindow.Localization.LoadLocalization(lang, "base")["def"];
            var tt = MainWindow.Localization.LoadLocalization(lang, "tooltips")["main"];

            label.Text = sr["label"];
            sfclabel.Text = sr["sfclabel"];
            dismlabel.Text = sr["dismlabel"];
            templabel.Text = sr["templabel"];
            batterylabel.Text = sr["batterylabel"];
            sfc.Content = dism.Content = sr["b2"];
            temp.Content = sr["b4"];
            report.Content = sr["reportbutton"];

            var toggles = new (ToggleSwitch s, string key)[]
            {
                (oldbootloader, "oldbootloader"), (advancedboot, "advancedboot"), (bitlocker, "bitlocker"),
                (chkdsk, "chkdsk"), (coreisol, "coreisol"), (hybern, "hybern"), (swap, "swap"),
                (sleeptimeout, "sleeptimeout"), (smartscreen, "smartscreen"), (uac, "uac"),
                (sticky, "sticky"), (vbs, "vbs"), (bing, "bing"), (telemetry, "telemetry"), (ttl, "ttl")
            };

            foreach (var (s, key) in toggles)
            {
                s.Header = sr[key];
                s.OffContent = bDef["off"];
                s.OnContent = bDef["on"];
            }

            sys_tooltip_sfc.Content = tt["sfc"];
            sys_tooltip_dism.Content = tt["dism"];
            sys_tooltip_sticky.Content = tt["sticky"];
            sys_tooltip_coreisol.Content = tt["coreisol"];
            sys_tooltip_uac.Content = tt["duac"];
            sys_tooltip_smartscreen.Content = tt["smartscr"];
            sys_tooltip_hyber.Content = tt["hybern"];
            sys_tooltip_vbs.Content = tt["coreisol"];
            sys_tooltip_swap.Content = tt["swap"];
            sys_tooltip_oldbootloader.Content = tt["oldloader"];
            sys_tooltip_advancedboot.Content = tt["additional"];
            sys_tooltip_chkdsk.Content = tt["chkdsk"];
            sys_tooltip_bitlocker.Content = tt["bitlocker"];
            sys_tooltip_bing.Content = tt["bing"];
            sys_tooltip_ttl.Content = tt["ttl"];
        }

        private void checkReg()
        {
            var (CU, LM) = (Registry.CurrentUser, Registry.LocalMachine);
            bool Exists(RegistryKey r, string p, string n, object v) => r.OpenSubKey(p)?.GetValue(n)?.Equals(v) ?? false;

            bitlocker.IsOn = Exists(LM, @"SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", 1);
            chkdsk.IsOn = Exists(LM, @"SYSTEM\CurrentControlSet\Control\Session Manager", "AutoChkTimeout", 60);
            coreisol.IsOn = Exists(LM, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios", "HypervisorEnforcedCodeIntegrity", 0);
            hybern.IsOn = Exists(LM, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 0);
            telemetry.IsOn = Exists(LM, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0);
            vbs.IsOn = Exists(LM, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0);
            bing.IsOn = Exists(CU, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1);
            ttl.IsOn = Exists(LM, @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "DefaultTTL", 65);

            var paging = LM.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management")?.GetValue("PagingFiles") as string[];
            swap.IsOn = paging == null || paging.All(string.IsNullOrWhiteSpace);

            smartscreen.IsOn = Exists(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableSmartScreen", 0) ||
                               Exists(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "Off");

            uac.IsOn = Exists(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0);

            sticky.IsOn = Exists(CU, @"Control Panel\Accessibility\StickyKeys", "Flags", "506") ||
                          Exists(CU, @"Control Panel\Accessibility\ToggleKeys", "Flags", "58") ||
                          Exists(CU, @"Control Panel\Accessibility\Keyboard Response", "Flags", "122");

            string bcdCurrent = GetOutput("bcdedit", "/enum {current}");
            oldbootloader.IsOn = bcdCurrent.Contains("bootmenupolicy") && bcdCurrent.Contains("legacy");
            string bcdGlobal = GetOutput("bcdedit", "/enum {globalsettings}");
            advancedboot.IsOn = Regex.IsMatch(bcdGlobal, @"advancedoptions\s+yes", RegexOptions.IgnoreCase);

            string pVideo = GetOutput("powercfg", "/q SCHEME_CURRENT SUB_VIDEO VIDEOIDLE");
            string pSleep = GetOutput("powercfg", "/q SCHEME_CURRENT SUB_SLEEP STANDBYIDLE");
            sleeptimeout.IsOn = pVideo.Contains("0x00000000") && pSleep.Contains("0x00000000");
        }

        private void report_Click(object sender, RoutedEventArgs e)
        {
            var lang = Settings.Default.lang ?? "en";
            var sr = MainWindow.Localization.LoadLocalization(lang, "sr")["status"];
            var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "HTML (*.html)|*.html", FileName = "battery-report.html" };

            if (sfd.ShowDialog() == true)
            {
                Run("cmd.exe", $"/c powercfg /batteryreport /output \"{sfd.FileName}\"", true);
                mw.ChSt(sr["o1b"]);
            }
        }

        private void ttl_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                using var v4 = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters");
                using var v6 = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\TCPIP6\Parameters");
                if (ttl.IsOn) { v4.SetValue("DefaultTTL", 65); v6.SetValue("DefaultTTL", 65); }
                else { v4.DeleteValue("DefaultTTL", false); v6.DeleteValue("DefaultTTL", false); }
            }
            catch { }
        }

        private void sticky_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            string f1 = sticky.IsOn ? "506" : "510", f2 = sticky.IsOn ? "122" : "126", f3 = sticky.IsOn ? "58" : "62";
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys", "Flags", f1);
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response", "Flags", f2);
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys", "Flags", f3);
            mw.RebootNotify(1);
        }

        private void coreisol_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios", "HypervisorEnforcedCodeIntegrity", coreisol.IsOn ? 0 : 1);
            mw.RebootNotify(1);
        }

        private void uac_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            if (WinHelper.GetWindowsBuild() >= 22621 && uac.IsOn)
            {
                var sr = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "sr")["status"];
                if (System.Windows.Forms.MessageBox.Show(sr["uacwarn"], "MakuTweaker", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No)
                {
                    uac.IsOn = false; return;
                }
            }
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", uac.IsOn ? 0 : 1);
            if (uac.IsOn)
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Attachments", "SaveZoneInformation", 1);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Associations", "LowRiskFileTypes", ".exe;.msi;.bat;");
            }
        }

        private void smartscreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableSmartScreen", smartscreen.IsOn ? 0 : 1);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", smartscreen.IsOn ? "Off" : "Warn");
        }

        private void hybern_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Run("cmd.exe", "/C powercfg /h " + (hybern.IsOn ? "off" : "on"), true);
            mw.RebootNotify(1);
        }

        private void sleeptimeout_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            int m = sleeptimeout.IsOn ? 0 : 10, s = sleeptimeout.IsOn ? 0 : 30;
            Run("powercfg", $"-change -monitor-timeout-ac {m}");
            Run("powercfg", $"-change -monitor-timeout-dc {m / 2}");
            Run("powercfg", $"-change -standby-timeout-ac {s}");
            Run("powercfg", $"-change -standby-timeout-dc {s / 2}");
        }

        private void bing_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded) Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", bing.IsOn ? 1 : 0);
        }

        private void vbs_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            int v = vbs.IsOn ? 0 : 1;
            string dg = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard";
            Registry.SetValue(dg, "EnableVirtualizationBasedSecurity", v);
            Registry.SetValue(dg, "RequirePlatformSecurityFeatures", v == 0 ? 0 : 3);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Lsa", "LsaCfgFlags", v);
            Run("cmd.exe", "/c bcdedit /set hypervisorlaunchtype " + (v == 0 ? "off" : "auto"));
            mw.RebootNotify(1);
        }

        private void oldbootloader_Toggled(object sender, RoutedEventArgs e) => Run("cmd.exe", $"/c bcdedit /set \"{{current}}\" bootmenupolicy {(oldbootloader.IsOn ? "legacy" : "standard")}");

        private void advancedboot_Toggled(object sender, RoutedEventArgs e) => Run("cmd.exe", $"/c bcdedit /set \"{{globalsettings}}\" advancedoptions {(advancedboot.IsOn ? "true" : "false")}");

        private void chkdsk_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded) Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager", "AutoChkTimeout", chkdsk.IsOn ? 60 : 8);
        }

        private void bitlocker_Toggled(object sender, RoutedEventArgs e)
        {
            if (isLoaded) Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", bitlocker.IsOn ? 1 : 0, RegistryValueKind.DWord);
        }

        private void telemetry_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            int v = telemetry.IsOn ? 0 : 1;
            string[] keys = [@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Policies\DataCollection", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection"];
            foreach (var k in keys)
            {
                Registry.SetValue("HKEY_LOCAL_MACHINE\\" + k, "AllowTelemetry", v);
                Registry.SetValue("HKEY_LOCAL_MACHINE\\" + k, "MaxTelemetryAllowed", v);
            }
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "AllowTelemetry", v);
        }

        private void swap_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            string[] val = swap.IsOn ? Array.Empty<string>() : new[] { @"?:\pagefile.sys" };
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "PagingFiles", val, RegistryValueKind.MultiString);
            mw.RebootNotify(1);
        }
    }
}