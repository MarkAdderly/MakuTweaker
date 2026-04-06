using MakuTweakerNew.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ModernWpf.Controls;

using Page = System.Windows.Controls.Page;

namespace MakuTweakerNew
{
    public partial class QuickSet : Page
    {
        private readonly MainWindow mw = (MainWindow)Application.Current.MainWindow;
        private bool togglesState = true;
        private string uncheckText = string.Empty;
        private string checkAllText = string.Empty;

        public QuickSet()
        {
            InitializeComponent();
            LoadLang();
            HideAlreadyAppliedTweaks();

            if (WinHelper.GetWindowsBuild() < 22621)
            {
                quick_oldcont.Visibility = quick_endtask.Visibility = Visibility.Collapsed;
            }
        }

        private void CheckIfTweaksFinished()
        {
            bool anyVisible = GetAllToggles(ToggleContainer).Any(t => t.Visibility == Visibility.Visible && (t.Parent as FrameworkElement)?.Visibility != Visibility.Collapsed);
            var lang = Settings.Default.lang ?? "en";
            var quick = MainWindow.Localization.LoadLocalization(lang, "quick")["main"];

            if (!anyVisible)
            {
                info.Text = quick["infodone"];
                start.Visibility = uncheck.Visibility = Visibility.Collapsed;
            }
            else
            {
                info.Text = quick["info"];
                start.Visibility = uncheck.Visibility = Visibility.Visible;
            }
        }

        private bool CheckReg(RegistryKey root, string path, string name, object expected)
        {
            using var key = root.OpenSubKey(path);
            return key?.GetValue(name)?.ToString() == expected?.ToString();
        }

        private void AnimateHide(FrameworkElement? el)
        {
            if (el == null) return;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            fade.Completed += (s, e) =>
            {
                el.Visibility = Visibility.Collapsed;
                el.Opacity = 1;
                CheckIfTweaksFinished();
            };
            el.BeginAnimation(OpacityProperty, fade);
        }

        private void HideAlreadyAppliedTweaks()
        {
            var (CU, LM) = (Registry.CurrentUser, Registry.LocalMachine);
            var rules = new (Func<bool> Cond, FrameworkElement Target)[]
            {
                (() => CheckReg(CU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden", 1), quick_hidden),
                (() => CheckReg(CU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0), quick_ext),
                (() => CheckReg(CU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo", 1), quick_pchome),
                (() => CheckReg(CU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 0), quick_showpc),
                (() => CheckReg(CU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates", "ShortcutNameTemplate", "%s.lnk"), quick_desktopend),
                (() => CheckReg(CU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton", 0), quick_hidewidget),
                (() => CheckReg(CU, @"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0), quick_removeads),
                (() => CheckReg(CU, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1), quick_bingoff),
                (() => CheckReg(CU, @"Control Panel\Accessibility\StickyKeys", "Flags", "506"), (FrameworkElement)quick_sticky.Parent),
                (() => CheckReg(CU, @"SOFTWARE\Microsoft\Clipboard", "EnableClipboardHistory", 1), quick_clipboard),
                (() => CheckReg(CU, @"Control Panel\Desktop", "MenuShowDelay", "50"), quick_contdelay),
                (() => CheckReg(LM, @"SYSTEM\CurrentControlSet\Control\Session Manager", "AutoChkTimeout", 60), (FrameworkElement)quick_chkdsk.Parent),
                (() => CheckReg(LM, @"SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", 1), (FrameworkElement)quick_bitlockeroff.Parent),
                (() => CheckReg(CU, @"SOFTWARE\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}", "System.IsPinnedToNameSpaceTree", 0), quick_gallery),
                (() => CheckReg(LM, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios", "HypervisorEnforcedCodeIntegrity", 0), (FrameworkElement)quick_coreisol.Parent),
                (() => CheckReg(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0), (FrameworkElement)quick_uac.Parent),
                (() => CheckReg(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "Off"), (FrameworkElement)quick_smartscreen.Parent),
                (() => CheckReg(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0), quick_telemetry),
                (() => CheckReg(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", "TaskbarEndTask", 1), quick_endtask),
                (() => CheckReg(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "verbosestatus", 1), (FrameworkElement)quick_verbose.Parent),
                (() => CU.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}") != null, quick_oldcont),
                (() => CheckReg(LM, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 0), (FrameworkElement)quick_hybern.Parent),
                (() => LM.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}") == null, quick_expfix),
                (() => CheckReg(LM, @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", "PauseUpdatesExpiryTime", "2077-01-01T00:00:00Z"), quick_winupd),
                (() => LM.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages")?.GetSubKeyNames().Any(x => x.Contains("DirectPlay")) == true, (FrameworkElement)quick_directplay.Parent),
                (() => CheckReg(LM, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0), (FrameworkElement)quick_vbs.Parent),
            };

            foreach (var r in rules) if (r.Cond()) r.Target.Visibility = Visibility.Collapsed;
            foreach (var t in GetAllToggles(ToggleContainer)) if (t.Visibility != Visibility.Visible) t.IsOn = false;

            CheckIfTweaksFinished();
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            var tweaks = new (ToggleSwitch T, FrameworkElement H, Action A)[]
            {
                (quick_hidden, quick_hidden, () => Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden", 1)),
                (quick_ext, quick_ext, () => Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0)),
                (quick_pchome, quick_pchome, () => Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo", 1)),
                (quick_showpc, quick_showpc, () => Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 0)),
                (quick_removeads, quick_removeads, () => Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0)),
                (quick_bingoff, quick_bingoff, () => Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1)),
                (quick_clipboard, quick_clipboard, () => Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Clipboard", "EnableClipboardHistory", 1)),
                (quick_contdelay, quick_contdelay, () => Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "MenuShowDelay", "50")),
                (quick_gallery, quick_gallery, () => Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}", "System.IsPinnedToNameSpaceTree", 0)),
                (quick_telemetry, quick_telemetry, () => ApplyTelemetry()),
                (quick_endtask, quick_endtask, () => Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings", "TaskbarEndTask", 1)),
                (quick_oldcont, quick_oldcont, () => ApplyOldMenu()),
                (quick_sticky, (FrameworkElement)quick_sticky.Parent, () => ApplySticky()),
                (quick_chkdsk, (FrameworkElement)quick_chkdsk.Parent, () => Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager", "AutoChkTimeout", 60)),
                (quick_bitlockeroff, (FrameworkElement)quick_bitlockeroff.Parent, () => Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", 1, RegistryValueKind.DWord)),
                (quick_coreisol, (FrameworkElement)quick_coreisol.Parent, () => Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios", "HypervisorEnforcedCodeIntegrity", 0)),
                (quick_uac, (FrameworkElement)quick_uac.Parent, () => ApplyUac()),
                (quick_smartscreen, (FrameworkElement)quick_smartscreen.Parent, () => ApplySmart()),
                (quick_hybern, (FrameworkElement)quick_hybern.Parent, () => RunCmd("powercfg", "/h off")),
                (quick_verbose, (FrameworkElement)quick_verbose.Parent, () => Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "verbosestatus", 1)),
                (quick_directplay, (FrameworkElement)quick_directplay.Parent, () => RunCmd("powershell.exe", "-Command \"& dism /online /Enable-Feature /FeatureName:DirectPlay /All\"")),
                (quick_vbs, (FrameworkElement)quick_vbs.Parent, () => ApplyVbs()),
            };

            foreach (var t in tweaks)
            {
                if (t.T.IsOn && t.T.IsVisible)
                {
                    try { t.A(); AnimateHide(t.H); } catch { }
                }
            }

            if (quick_expfix.IsOn && quick_expfix.IsVisible)
            {
                try
                {
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders", true)?.DeleteSubKey("{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}", false);
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders", true)?.DeleteSubKey("{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}", false);
                    AnimateHide(quick_expfix);
                }
                catch { }
            }

            if (quick_desktopend.IsOn && quick_desktopend.IsVisible)
            {
                try
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 0);
                    Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates", "ShortcutNameTemplate", "%s.lnk");
                    AnimateHide(quick_desktopend);
                }
                catch { }
            }

            if (quick_hidewidget.IsOn && quick_hidewidget.IsVisible)
            {
                try
                {
                    var adv = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
                    Registry.SetValue(adv, "ShowTaskViewButton", 0);
                    Registry.SetValue(adv, "TaskbarDa", 0);
                    Registry.SetValue(adv, "TaskbarMn", 0);
                    AnimateHide(quick_hidewidget);
                }
                catch { }
            }

            if (quick_winupd.IsOn && quick_winupd.IsVisible) { ApplyPause(); AnimateHide(quick_winupd); }
        }

        private void ApplySticky()
        {
            var path = @"HKEY_CURRENT_USER\Control Panel\Accessibility\";
            Registry.SetValue(path + "StickyKeys", "Flags", "506");
            Registry.SetValue(path + "Keyboard Response", "Flags", "122");
            Registry.SetValue(path + "ToggleKeys", "Flags", "58");
        }

        private void ApplyUac()
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Attachments", "SaveZoneInformation", 1, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Associations", "LowRiskFileTypes", ".exe;.msi;.bat;");
        }

        private void ApplySmart()
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableSmartScreen", 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", "Off");
        }

        private void ApplyTelemetry()
        {
            string[] paths = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Policies\DataCollection" };
            foreach (var p in paths)
            {
                Registry.SetValue("HKEY_LOCAL_MACHINE\\" + p, "AllowTelemetry", 0);
                Registry.SetValue("HKEY_LOCAL_MACHINE\\" + p, "MaxTelemetryAllowed", 0);
            }
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "AllowTelemetry", 0);
        }

        private void ApplyVbs()
        {
            string dg = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard";
            Registry.SetValue(dg, "EnableVirtualizationBasedSecurity", 0, RegistryValueKind.DWord);
            Registry.SetValue(dg, "RequirePlatformSecurityFeatures", 0, RegistryValueKind.DWord);
            RunCmd("bcdedit", "/set hypervisorlaunchtype off");
        }

        private void ApplyOldMenu()
        {
            RunCmd("reg.exe", "add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32\" /f /ve");
            Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 0);
        }

        private void ApplyPause()
        {
            string path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
            Registry.SetValue(path, "PauseUpdatesExpiryTime", "2077-01-01T00:00:00Z");
            Registry.SetValue(path, "PauseUpdatesStartTime", "2015-01-01T00:00:00Z");
        }

        private void RunCmd(string file, string args)
        {
            try { Process.Start(new ProcessStartInfo(file, args) { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit(); } catch { }
        }

        private void LoadLang()
        {
            var lang = Settings.Default.lang ?? "en";
            var quick = MainWindow.Localization.LoadLocalization(lang, "quick")["main"];
            var basel = MainWindow.Localization.LoadLocalization(lang, "base")["def"];
            var tooltips = MainWindow.Localization.LoadLocalization(lang, "tooltips")["main"];
            var expl = MainWindow.Localization.LoadLocalization(lang, "expl")["main"];
            var wu = MainWindow.Localization.LoadLocalization(lang, "wu")["main"];
            var sr = MainWindow.Localization.LoadLocalization(lang, "sr")["main"];
            var cm = MainWindow.Localization.LoadLocalization(lang, "cm")["main"];
            var per = MainWindow.Localization.LoadLocalization(lang, "per")["main"];
            var comp = MainWindow.Localization.LoadLocalization(lang, "compon")["main"];

            quick_hidewidget.Header = quick["hidewidget"];
            quick_removeads.Header = quick["removeads"];
            quick_clipboard.Header = quick["clipboard"];
            quick_hidden.Header = expl["hidden"];
            quick_ext.Header = expl["ext"];
            quick_pchome.Header = expl["pchome"];
            quick_gallery.Header = expl["gallery"];
            quick_showpc.Header = expl["showpc"];
            quick_desktopend.Header = expl["shortcut"];
            quick_expfix.Header = expl["fixlabel"];
            quick_winupd.Header = wu["wu5"];
            quick_contdelay.Header = cm["t2"];
            quick_oldcont.Header = cm["t1"];
            quick_verbose.Header = per["verbose"];
            quick_endtask.Header = per["etask"];
            quick_directplay.Header = comp["directplay"];
            quick_bitlockeroff.Header = sr["bitlocker"];
            quick_bingoff.Header = sr["bing"];
            quick_sticky.Header = sr["sticky"];
            quick_chkdsk.Header = sr["chkdsk"];
            quick_coreisol.Header = sr["coreisol"];
            quick_uac.Header = sr["uac"];
            quick_smartscreen.Header = sr["smartscreen"];
            quick_hybern.Header = sr["hybern"];
            quick_vbs.Header = sr["vbs"];
            quick_telemetry.Header = sr["telemetry"];

            label.Text = quick["label"];
            info.Text = quick["info"];
            start.Content = quick["b"];
            uncheckText = quick["uncheck"];
            checkAllText = quick["checkall"];
            uncheck.Content = uncheckText;

            foreach (var t in GetAllToggles(ToggleContainer)) { t.OnContent = basel["on"]; t.OffContent = basel["off"]; }

            sys_tooltip_sticky.Content = tooltips["sticky"];
            sys_tooltip_coreisol.Content = tooltips["coreisol"];
            sys_tooltip_uac.Content = tooltips["duac"];
            sys_tooltip_smartscreen.Content = tooltips["smartscr"];
            sys_tooltip_hyber.Content = tooltips["hybern"];
            sys_tooltip_vbs.Content = tooltips["vbs"];
            sys_tooltip_chkdsk.Content = tooltips["chkdsk"];
            sys_tooltip_bitlocker.Content = tooltips["bitlocker"];
            sys_tooltip_verbose.Content = tooltips["advanced"];
            sys_tooltip_directplay.Content = tooltips["directplay"];
        }

        private IEnumerable<ToggleSwitch> GetAllToggles(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ToggleSwitch t) yield return t;
                foreach (var next in GetAllToggles(child)) yield return next;
            }
        }

        private void uncheck_Click(object sender, RoutedEventArgs e)
        {
            togglesState = !togglesState;
            foreach (var t in GetAllToggles(ToggleContainer)) if (t.Visibility == Visibility.Visible) t.IsOn = togglesState;
            uncheck.Content = togglesState ? uncheckText : checkAllText;
        }
    }
}