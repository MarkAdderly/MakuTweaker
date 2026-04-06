using MakuTweakerNew.Properties;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace MakuTweakerNew
{
    public partial class Personalization : Page
    {
        bool isLoaded = false;
        MainWindow mw = (MainWindow)Application.Current.MainWindow;
        public Personalization()
        {
            InitializeComponent();
            checkReg();
            LoadLang();
            if (WinHelper.GetWindowsBuild() < 22000)
            {
                endtask.Visibility = Visibility.Collapsed;
            }
            isLoaded = true;
        }

        private void RunCmdCommand(string fileName, string arguments)
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = fileName;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.Start();
                p.WaitForExit();
            }
        }

        private void apN_Click(object sender, RoutedEventArgs e)
        {
            var per = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "per");
            Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates", "RenameNameTemplate", newname.Text);
            mw.ChSt(per["status"]["apN"]);
        }

        private void stN_Click(object sender, RoutedEventArgs e)
        {
            var per = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "per");
            newname.Text = string.Empty;
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates", true);
            key?.DeleteValue("RenameNameTemplate", false);
            mw.ChSt(per["status"]["stN"]);
        }

        private void apC_Click(object sender, RoutedEventArgs e)
        {
            var per = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "per");

            var colors = new[]
            {
                ("51 153 255", "0 102 204"), ("0 100 100", "0 100 100"), ("180 0 180", "110 0 110"),
                ("0 90 30", "0 90 30"), ("100 40 0", "100 40 0"), ("135 0 0", "135 0 0"),
                ("15 0 120", "15 0 120"), ("40 40 40", "40 40 40")
            };

            int i = color.SelectedIndex >= 0 ? color.SelectedIndex : 0;
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", true);
            if (key != null)
            {
                key.SetValue("HightLight", colors[i].Item1);
                key.SetValue("Hilight", colors[i].Item1);
                key.SetValue("HotTrackingColor", colors[i].Item2);
            }

            mw.ChSt(per["status"]["apC"]);
            mw.RebootNotify(1);
        }

        private void LoadLang()
        {
            var lang = Settings.Default.lang ?? "en";
            var per = MainWindow.Localization.LoadLocalization(lang, "per")["main"];
            var basel = MainWindow.Localization.LoadLocalization(lang, "base")["def"];
            var tooltips = MainWindow.Localization.LoadLocalization(lang, "tooltips")["main"];

            label.Text = per["label"];
            defaultnamelabel.Text = per["defaultnamelabel"];
            colorlabel.Text = per["colorlabel"];
            newname.Watermark = per["newname"];
            apN.Content = apC.Content = basel["apply"];
            stN.Content = per["b2"];

            c1.Content = per["c1"]; c2.Content = per["c2"]; c3.Content = per["c3"]; c4.Content = per["c4"];
            c5.Content = per["c5"]; c6.Content = per["c6"]; c7.Content = per["c7"]; c8.Content = per["c8"];

            ModernWpf.Controls.ToggleSwitch[] ts = { smallwindows, blur, transparency, darktheme, verbose, endtask, disablelogo, disableanim };
            string[] keys = ["smallwindows", "blur", "transparency", "darktheme", "verbose", "etask", "disablelogo", "disableanim"];

            for (int i = 0; i < ts.Length; i++)
            {
                ts[i].Header = per[keys[i]];
                ts[i].OffContent = basel["off"];
                ts[i].OnContent = basel["on"];
            }

            sys_tooltip_verbose.Content = tooltips["advanced"];
        }

        private void checkReg()
        {
            var (CU, LM) = (Registry.CurrentUser, Registry.LocalMachine);

            newname.Text = CU.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates")?.GetValue("RenameNameTemplate")?.ToString();
            smallwindows.IsOn = CU.OpenSubKey(@"Control Panel\Desktop\WindowMetrics")?.GetValue("CaptionHeight")?.ToString() == "-270";
            blur.IsOn = LM.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System")?.GetValue("DisableAcrylicBackgroundOnLogon")?.Equals(1) == true;
            transparency.IsOn = CU.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")?.GetValue("EnableTransparency")?.Equals(0) == true;

            using var themeKey = CU.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            darktheme.IsOn = themeKey?.GetValue("AppsUseLightTheme")?.Equals(0) == true && themeKey?.GetValue("SystemUsesLightTheme")?.Equals(0) == true;

            verbose.IsOn = LM.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System")?.GetValue("verbosestatus")?.Equals(1) == true;
            endtask.IsOn = CU.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings")?.GetValue("TaskbarEndTask")?.Equals(1) == true;

            try
            {
                var p = Process.Start(new ProcessStartInfo("bcdedit", "/enum {globalsettings}") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
                string output = p?.StandardOutput.ReadToEnd().ToLower() ?? "";
                disablelogo.IsOn = IsBcd(output, "custom:16000067", "bootlogo", "nobootlogo");
                disableanim.IsOn = IsBcd(output, "custom:16000069", "nobootuxprogress");
            }
            catch { }

            var curColor = CU.OpenSubKey(@"Control Panel\Colors")?.GetValue("HightLight")?.ToString();
            var colorValues = new[] { "51 153 255", "0 100 100", "180 0 180", "0 90 30", "100 40 0", "135 0 0", "15 0 120", "40 40 40" };
            color.SelectedIndex = Math.Max(0, Array.IndexOf(colorValues, curColor));
        }

        private bool IsBcd(string outStr, params string[] keys)
        {
            var line = outStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(l => keys.Any(k => l.Contains(k)));
            if (string.IsNullOrEmpty(line))
                return false;

            var val = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Last().ToLower();
            return !(val.StartsWith("n") || val.StartsWith("н") || val == "0" || val == "false");
        }

        private void endtask_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings").SetValue("TaskbarEndTask", endtask.IsOn ? 1 : 0);
        }

        private void smallwindows_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            string val = smallwindows.IsOn ? "-270" : "-330";
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "CaptionHeight", val);
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "CaptionWidth", val);
            mw.RebootNotify(1);
        }

        private void blur_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System").SetValue("DisableAcrylicBackgroundOnLogon", blur.IsOn ? 1 : 0);
        }

        private void transparency_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize").SetValue("EnableTransparency", transparency.IsOn ? 0 : 1);
        }

        private void darktheme_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize").SetValue("AppsUseLightTheme", darktheme.IsOn ? 0 : 1);
            Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize").SetValue("SystemUsesLightTheme", darktheme.IsOn ? 0 : 1);
            mw.RebootNotify(2);
            System.Windows.Forms.Application.Restart();
            Application.Current.Shutdown();
        }

        private void verbose_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System").SetValue("verbosestatus", verbose.IsOn ? 1 : 0);
        }

        private void disablelogo_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            if (disablelogo.IsOn) RunCmdCommand("bcdedit", "/set \"{globalsettings}\" custom:16000067 true");
            else RunCmdCommand("bcdedit", "/deletevalue \"{globalsettings}\" custom:16000067");
            mw.RebootNotify(1);
        }

        private void disableanim_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            if (disableanim.IsOn) RunCmdCommand("bcdedit", "/set \"{globalsettings}\" custom:16000069 true");
            else RunCmdCommand("bcdedit", "/deletevalue \"{globalsettings}\" custom:16000069");
            mw.RebootNotify(1);
        }
    }
}
