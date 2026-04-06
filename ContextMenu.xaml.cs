using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MakuTweakerNew.Properties;
using Microsoft.Win32;

namespace MakuTweakerNew
{
    //Это старейшая страница в коде MakuTweaker. Я давно планирую отказаться от вкладки "Контекстное меню"...
    //потому что это самая невостребованная вкладка.
    //Здесь очень страшный код, потому что он со времён моих начинаний в C#.
    //Скорее всего - в MakuTweaker 5.4, как раз эта вкладка и весь этот код будет удалён, и заменён на что то новое.

    //This is the oldest page in the MakuTweaker source code.
    //I have long planned to retire the "Context Menu" tab,
    //as it is the least-used tab. The code here is truly dreadful,
    //dating back to my very beginnings with C#.
    //Most likely, in MakuTweaker 5.4, this specific tab—along with all this code—will be removed and replaced with something new.


    public partial class ContextMenu : Page
    {
        private MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
        bool isLoaded = false;
        public ContextMenu()
        {
            InitializeComponent();
            checkReg();
            LoadLang();
            if (checkWinVer() < 22000)
            {
                t15.Visibility = Visibility.Collapsed;
                t13.Visibility = Visibility.Collapsed;
            }
            isLoaded = true;
        }

        private int checkWinVer() => WinHelper.GetWindowsBuild(); // мне лень переводить напрямую

        private void t1_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "MenuShowDelay", t1.IsOn ? "50" : "400");
            mw.RebootNotify(2);
        }

        private void t3_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
            const string guid = "{9F156763-7844-4DC4-B2B1-901F640F5155}";

            try
            {
                if (t3.IsOn)
                    Registry.LocalMachine.CreateSubKey(keyPath).SetValue(guid, "");
                else
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions", true)
                                        ?.DeleteSubKey("Blocked", false);
            }
            catch { } // just ignore, right?

            mw.RebootNotify(2);
        }

        private void t5_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            const string path = @"AllFilesystemObjects\shellex\ContextMenuHandlers\ModernSharing";
            const string guid = "{e2bf9676-5f8f-435c-97eb-11607a5bedf7}";

            try
            {
                if (t5.IsOn)
                {
                    Registry.ClassesRoot.DeleteSubKey(path, false);
                }
                else
                {
                    Registry.ClassesRoot.CreateSubKey(path).SetValue("", guid);
                }
            }
            catch { } // just ignore, right?
        }

        private void t6_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked");
                if (t6.IsOn)
                {
                    key.SetValue("{596AB062-B4D2-4215-9F74-E9109B0A8153}", "");
                }
                else
                {
                    key.DeleteValue("{596AB062-B4D2-4215-9F74-E9109B0A8153}", false);
                }
            }
            catch { } // just ignore, right?

            mw.RebootNotify(2);
        }

        private void t8_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                string value = t8.IsOn ? "" : "{7BA4C740-9E81-11CF-99D3-00AA004AE837}";
                Registry.ClassesRoot.CreateSubKey(@"AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo").SetValue("", value);
            }
            catch { } // just ignore, right?

            mw.RebootNotify(2);
        }
        private void t10_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                if (t10.IsOn)
                    Registry.ClassesRoot.DeleteSubKey(@"AllFilesystemObjects\shellex\ContextMenuHandlers\CopyAsPathMenu", false);
                else
                    Registry.ClassesRoot.CreateSubKey(@"AllFilesystemObjects\shellex\ContextMenuHandlers\CopyAsPathMenu").SetValue("", "{f3d06e7c-1e45-4a26-847e-f9fcdee59be0}");
            }
            catch { } // just ignore, right?
        }

        private void t11_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                if (t11.IsOn)
                {
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\Folder\shellex\ContextMenuHandlers", true)?.DeleteSubKey("PintoStartScreen", false);
                    Registry.ClassesRoot.CreateSubKey(@"exefile\shellex\ContextMenuHandlers\PintoStartScreen").SetValue("", "");
                }
                else
                {
                    Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Classes\Folder\shellex\ContextMenuHandlers\PintoStartScreen").SetValue("", "{470C0EBD-5D73-4d58-9CED-E91E22E23282}");
                    Registry.ClassesRoot.CreateSubKey(@"exefile\shellex\ContextMenuHandlers\PintoStartScreen").SetValue("", "{470C0EBD-5D73-4d58-9CED-E91E22E23282}");
                }
            }
            catch { } // just ignore, right?
        }

        private void t12_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                if (t12.IsOn)
                    Registry.ClassesRoot.DeleteSubKey(@"*\shellex\ContextMenuHandlers\{90AA3A4E-1CBA-4233-B8BB-535773D48449}", false);
                else
                    Registry.ClassesRoot.CreateSubKey(@"*\shellex\ContextMenuHandlers\{90AA3A4E-1CBA-4233-B8BB-535773D48449}").SetValue("", "Taskband Pin");
            }
            catch { } // just ignore, right?
        }

        private void t13_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                if (t13.IsOn)
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(@"Folder\shell\opennewtab", false);
                }
                else
                {
                    using var key = Registry.ClassesRoot.CreateSubKey(@"Folder\shell\opennewtab");
                    key.SetValue("CommandStateHandler", "{11dbb47c-a525-400b-9e80-a54615a090c0}");
                    key.SetValue("CommandStateSync", "");
                    key.SetValue("LaunchExplorerFlags", 32);
                    key.SetValue("MUIVerb", "@windows.storage.dll,-8519");
                    key.SetValue("MultiSelectModel", "Document");
                    key.SetValue("OnlyInBrowserWindow", "");
                    key.CreateSubKey("command").SetValue("DelegateExecute", "{11dbb47c-a525-400b-9e80-a54615a090c0}");
                }
            }
            catch { } // just ignore, right?
        }

        private void t14_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\NVIDIA Corporation\Global\NvCplApi\Policies", "ContextUIPolicy", t14.IsOn ? 0 : 2);
            }
            catch { } // just ignore, right?
        }

        private void t15_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            string key = @"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
            string args = t15.IsOn ? $"add \"{key}\" /f /ve" : $"delete \"{key}\" /f";

            try
            {
                Process.Start(new ProcessStartInfo("reg.exe", args) { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 0);
            }
            catch { }

            mw.RebootNotify(2);
        }

        private void LoadLang()
        {
            var lang = Properties.Settings.Default.lang ?? "en";
            var cm = MainWindow.Localization.LoadLocalization(lang, "cm")["main"];
            var def = MainWindow.Localization.LoadLocalization(lang, "base")["def"];

            label.Text = cm["label"];

            var items = new (ModernWpf.Controls.ToggleSwitch s, string key)[]
            {
                (t15, "t1"), (t1, "t2"), (t3, "t3"), (t5, "t5"), (t6, "t6"),
                (t8, "t8"), (t10, "t10"), (t11, "t11"), (t12, "t12"), (t13, "t13"), (t14, "t14")
            };

            foreach (var (s, key) in items)
            {
                s.Header = cm[key];
                s.OffContent = def["off"];
                s.OnContent = def["on"];
            }
        }

        private void checkReg()
        {
            var (CU, LM, CR) = (Registry.CurrentUser, Registry.LocalMachine, Registry.ClassesRoot);

            bool Exists(RegistryKey r, string p, string n, object v) => r.OpenSubKey(p)?.GetValue(n)?.Equals(v) ?? false;
            bool NoKey(RegistryKey r, string p) => r.OpenSubKey(p) == null;

            string blocked = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
            string handlers = @"AllFilesystemObjects\shellex\ContextMenuHandlers";

            var checks = new (ModernWpf.Controls.ToggleSwitch t, bool state)[]
            {
                (t1,  Exists(CU, @"Control Panel\Desktop", "MenuShowDelay", "50")),
                (t3,  Exists(LM, blocked, "{9F156763-7844-4DC4-B2B1-901F640F5155}", "")),
                (t5,  NoKey(CR, $@"{handlers}\ModernSharing")),
                (t6,  Exists(LM, blocked, "{596AB062-B4D2-4215-9F74-E9109B0A8153}", "")),
                (t8,  Exists(CR, $@"{handlers}\SendTo", "", "")),
                (t10, NoKey(CR, $@"{handlers}\CopyAsPathMenu")),
                (t11, NoKey(LM, @"SOFTWARE\Classes\Folder\shellex\ContextMenuHandlers\PintoStartScreen") || Exists(CR, @"exefile\shellex\ContextMenuHandlers\PintoStartScreen", "", "")),
                (t12, NoKey(CR, @"*\shellex\ContextMenuHandlers\{90AA3A4E-1CBA-4233-B8BB-535773D48449}")),
                (t13, NoKey(CR, @"Folder\shell\opennewtab")),
                (t14, Exists(CU, @"Software\NVIDIA Corporation\Global\NvCplApi\Policies", "ContextUIPolicy", 0)),
                (t15, Exists(CU, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", ""))
            };

            foreach (var c in checks) c.t.IsOn = c.state;
        }
    }
}
