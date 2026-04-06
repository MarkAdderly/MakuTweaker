using MakuTweakerNew.Properties;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MakuTweakerNew
{
    public partial class WindowsComponents : Page
    {
        private readonly MainWindow mw = (MainWindow)Application.Current.MainWindow;

        public WindowsComponents()
        {
            InitializeComponent();
            string ed = WinHelper.GetWindowsEdition();
            if (ed.Contains("Core") || ed.Contains("SingleLanguage") || ed.Contains("CountrySpecific") || ed.Contains("CoreN"))
            {
                gpedit.Visibility = lgp.Visibility = Visibility.Visible;
            }
            checkReg();
            LoadLang();
        }

        private void Run(string file, string args)
        {
            try
            {
                Process.Start(new ProcessStartInfo(file, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        private void checkReg()
        {
            using var gKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR");
            dvr.IsEnabled = gKey?.GetValue("AllowGameDVR")?.ToString() != "0";

            using var pKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\powershell\1\ShellIds\Microsoft.powershell");
            pwsh.IsEnabled = pKey?.GetValue("ExecutionPolicy")?.ToString() != "RemoteSigned";
        }

        private void LoadLang()
        {
            var lang = Settings.Default.lang ?? "en";
            var m = MainWindow.Localization.LoadLocalization(lang, "compon")["main"];
            var b = MainWindow.Localization.LoadLocalization(lang, "base")["def"];
            var tt = MainWindow.Localization.LoadLocalization(lang, "tooltips")["main"];

            label.Text = m["label"];
            directplay.Text = m["directplay"];
            framework.Text = m["framework"];
            photoviewer.Text = m["photoviewer"];
            powershellscr.Text = m["powershellscr"];
            xboxdvr.Text = m["xboxdvr"];
            forcedis.Text = m["forcedis"];
            winsxs.Text = m["winsxs"];
            gpedit.Text = m["gpedit"];

            dp.Content = dnet.Content = m["install"];
            sxs.Content = m["reset"];
            lgp.Content = pv.Content = pwsh.Content = m["enable"];
            dvr.Content = hypervdis.Content = b["apply"];

            sys_tooltip_photo.Content = tt["photow"];
            sys_tooltip_powershell.Content = tt["powershell"];
            sys_tooltip_xbox.Content = tt["xbox"];
            sys_tooltip_hyperv.Content = tt["hyperv"];
            sys_tooltip_directplay.Content = tt["directplay"];
        }

        private void pwsh_Click(object sender, RoutedEventArgs e)
        {
            Run("powershell", "-Command Set-ExecutionPolicy RemoteSigned -Force");
            pwsh.IsEnabled = false;
        }

        private void dp_Click(object sender, RoutedEventArgs e)
        {
            Run("cmd.exe", "/C dism /online /Enable-Feature /FeatureName:DirectPlay /All");
            dp.IsEnabled = false;
        }

        private void dnet_Click(object sender, RoutedEventArgs e)
        {
            Run("powershell.exe", "/C Add-WindowsCapability -Online -Name NetFx3~~~~");
            dnet.IsEnabled = false;
        }

        private void sxs_Click(object sender, RoutedEventArgs e)
        {
            Run("cmd.exe", "/C dism /Online /Cleanup-Image /StartComponentCleanup /ResetBase");
            mw.RebootNotify(3);
            sxs.IsEnabled = false;
        }

        private void lgp_Click(object sender, RoutedEventArgs e)
        {
            var sr = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "sr")["status"];
            string script = "@echo off\r\npushd \"%~dp0\"\r\ndir /b %SystemRoot%\\servicing\\Packages\\Microsoft-Windows-GroupPolicy-ClientExtensions-Package~3*.mum >List.txt\r\ndir /b %SystemRoot%\\servicing\\Packages\\Microsoft-Windows-GroupPolicy-ClientTools-Package~3*.mum >>List.txt\r\nfor /f %%i in ('findstr /i . List.txt 2^>nul') do dism /online /norestart /add-package:\"%SystemRoot%\\servicing\\Packages\\%%i\"";
            string path = Path.Combine(Path.GetTempPath(), "lgp.bat");
            File.WriteAllText(path, script);

            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + path + "\"")
                {
                    UseShellExecute = true
                })?.WaitForExit();
            }
            catch { }

            mw.ChSt(sr["sr8"]);
            lgp.IsEnabled = false;
        }

        private void pv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Registry.SetValue(@"HKEY_CLASSES_ROOT\Applications\photoviewer.dll\shell\open", "MuiVerb", "@photoviewer.dll,-3043");
                Registry.SetValue(@"HKEY_CLASSES_ROOT\Applications\photoviewer.dll\shell\open\command", "", @"%SystemRoot%\System32\rundll32.exe ""%ProgramFiles%\Windows Photo Viewer\PhotoViewer.dll"", ImageViewer_Fullscreen %1");

                string assoc = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations";
                string val = "PhotoViewer.FileAssoc.Tiff";
                Registry.SetValue(assoc, ".bmp", val);
                Registry.SetValue(assoc, ".gif", val);
                Registry.SetValue(assoc, ".jpeg", val);
                Registry.SetValue(assoc, ".jpg", val);
                Registry.SetValue(assoc, ".png", val);
                pv.IsEnabled = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void dvr_Click(object sender, RoutedEventArgs e)
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0, RegistryValueKind.DWord);
            dvr.IsEnabled = false;
        }

        private void hypervdis_Click(object sender, RoutedEventArgs e)
        {
            Run("cmd.exe", "/c \"bcdedit /set hypervisorlaunchtype off\"");
            hypervdis.IsEnabled = false;
        }
    }
}