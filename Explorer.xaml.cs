using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using MakuTweakerNew.Properties;
using Microsoft.Win32;

namespace MakuTweakerNew
{
    public partial class Explorer : Page
    {
        private MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
        bool isLoaded = false;
        public Explorer()
        {
            InitializeComponent();
            checkReg();
            nonremovable.Visibility = checkWinVer() >= 22621 ? Visibility.Collapsed : Visibility.Visible;

            LoadLang(Settings.Default.lang);
            isLoaded = true;
        }

        private void fix_Click(object sender, RoutedEventArgs e)
        {
            var lang = Settings.Default.lang ?? "en";
            var expl = MainWindow.Localization.LoadLocalization(lang, "expl");

            try
            {
                const string guid = "{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}";
                Registry.LocalMachine.DeleteSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{guid}", false);
                Registry.LocalMachine.DeleteSubKey($@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{guid}", false);
            }
            catch { } // just ignore, right?

            mw.ChSt(expl["status"]["e8"]);
            FadeOut(fixlabel, 300);
            FadeOut(fix, 300);
            fixlabel.IsEnabled = fix.IsEnabled = false;
        }

        private void FadeOut(UIElement element, double durationSeconds)
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(durationSeconds),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(OpacityProperty, fadeOutAnimation);
        }

        private int checkWinVer() => WinHelper.GetWindowsBuild(); // мне лень переводить напрямую

        private void LoadLang(string lang)
        {
            var langCode = Settings.Default.lang ?? "en";
            var expl = MainWindow.Localization.LoadLocalization(langCode, "expl")["main"];
            var bDef = MainWindow.Localization.LoadLocalization(langCode, "base")["def"];

            var toggles = new (ModernWpf.Controls.ToggleSwitch t, string key)[]
            {
                (nonremovable, "nonremovable"),
                (hidden, "hidden"),
                (ext, "ext"),
                (pchome, "pchome"),
                (gallery, "gallery"),
                (showpc, "showpc"),
                (shortcut, "shortcut")
            };

            foreach (var (t, key) in toggles)
            {
                t.Header = expl[key];
                t.OnContent = bDef["on"];
                t.OffContent = bDef["off"];
            }

            lab.Text = expl["label"];
            fixlabel.Text = expl["fixlabel"];
            fix.Content = expl["e8b"];
            driveslabel.Text = expl["driveslabel"];
            hide.Content = expl["choose"];
            showall.Content = expl["showall"];
        }

        private void checkReg()
        {
            var (CU, LM) = (Registry.CurrentUser, Registry.LocalMachine);

            bool Exists(RegistryKey r, string p, string n, object v) => r.OpenSubKey(p)?.GetValue(n)?.Equals(v) ?? false;
            bool NoKey(RegistryKey r, string p) => r.OpenSubKey(p) == null;

            string adv = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            string ns = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\";
            string wns = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\";

            string[] guids =
            {
                "{A0953C92-50DC-43bf-BE83-3742FED03C9C}", "{f86fa3ab-70d2-4fc7-9c99-fcbf05467f3a}",
                "{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}", "{d3162b92-9365-467a-956b-92703aca08af}",
                "{374DE290-123F-4565-9164-39C4925E467B}", "{088e3905-0323-4b02-9826-5d99428e115f}",
                "{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}", "{24ad3ad4-a569-4530-98e1-ab02f9417aa8}",
                "{1CF1260C-4DD0-4ebb-811F-33C572699FDE}", "{3dfdf296-dbec-4fb4-81d1-6a3438bcf4de}",
                "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", "{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}"
            };

            nonremovable.IsOn = guids.Any(g => NoKey(LM, ns + g) || NoKey(LM, wns + g));

            hidden.IsOn = Exists(CU, adv, "Hidden", 1);
            ext.IsOn = Exists(CU, adv, "HideFileExt", 0);
            pchome.IsOn = Exists(CU, adv, "LaunchTo", 1);
            gallery.IsOn = Exists(CU, @"SOFTWARE\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}", "System.IsPinnedToNameSpaceTree", 0);
            showpc.IsOn = Exists(CU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 0);
            shortcut.IsOn = Exists(CU, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates", "ShortcutNameTemplate", "%s.lnk");

            string f1 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}";
            string f2 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}";

            if (NoKey(LM, f1) || NoKey(LM, f2))
                fixlabel.Visibility = fix.Visibility = Visibility.Collapsed;

            var nd = CU.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer")?.GetValue("NoDrives");
            showall.IsEnabled = nd != null && nd.ToString() != "0";
        }

        private async void hide_Click(object sender, RoutedEventArgs e)
        {
            HidePart dialog = new HidePart();
            var result = await dialog.ShowAsync();
            decimal resulty = await dialog.TaskCompletionSource.Task;
            if (resulty != -1)
            {
                Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer").SetValue("NoDrives", resulty, RegistryValueKind.DWord);
                mw.RebootNotify(2);
            }
            showall.IsEnabled = true;
        }

        private void showall_Click(object sender, RoutedEventArgs e)
        {
            Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer").SetValue("NoDrives", 0, RegistryValueKind.DWord);
            mw.RebootNotify(2);
            showall.IsEnabled = false;
        }

        private void nonremovable_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            string[] guids =
            {
                "{A0953C92-50DC-43bf-BE83-3742FED03C9C}", "{f86fa3ab-70d2-4fc7-9c99-fcbf05467f3a}",
                "{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}", "{d3162b92-9365-467a-956b-92703aca08af}",
                "{374DE290-123F-4565-9164-39C4925E467B}", "{088e3905-0323-4b02-9826-5d99428e115f}",
                "{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}", "{24ad3ad4-a569-4530-98e1-ab02f9417aa8}",
                "{1CF1260C-4DD0-4ebb-811F-33C572699FDE}", "{3dfdf296-dbec-4fb4-81d1-6a3438bcf4de}",
                "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", "{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}"
            };

            string[] paths =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace",
                @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace"
            };

            try
            {
                foreach (var path in paths)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(path, true);
                    if (key == null) continue;

                    foreach (var guid in guids)
                    {
                        if (nonremovable.IsOn)
                            key.DeleteSubKey(guid, false);
                        else
                            key.CreateSubKey(guid);
                    }
                }
            }
            catch { }
        }

        private void hidden_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            int v = hidden.IsOn ? 1 : 0;
            Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced").SetValue("Hidden", v);
        }

        private void ext_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            int v = ext.IsOn ? 0 : 1;
            Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced").SetValue("HideFileExt", v);
        }

        private void pchome_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            int v = pchome.IsOn ? 1 : 2;
            Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced").SetValue("LaunchTo", v);
        }

        private void gallery_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            int v = gallery.IsOn ? 0 : 1;
            Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}").SetValue("System.IsPinnedToNameSpaceTree", v);
        }

        private void showpc_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            int v = showpc.IsOn ? 0 : 1;
            Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel").SetValue("{20D04FE0-3AEA-1069-A2D8-08002B30309D}", v);
        }

        private void shortcut_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                if (shortcut.IsOn)
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates", "ShortcutNameTemplate", "%s.lnk");
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates", false);
                }
            }
            catch { }
        }
    }
}

