using ABI.System;
using MakuTweakerNew.Properties;
using MicaWPF.Core.Enums;
using MicaWPF.Core.Services;
using MicaWPF.Styles;
using Microsoft.Win32;
using ModernWpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;

namespace MakuTweakerNew
{
    public partial class SettingsAbout : Page
    {
        private MainWindow mw = (MainWindow)System.Windows.Application.Current.MainWindow;
        bool isLoaded = false;
        public SettingsAbout()
        {
            InitializeComponent();
            credN.Text = "Mark Adderly\nNikitori\nNikitori, Massgrave";
            lang.SelectedIndex = Settings.Default.langSI;
            relang();
            UpdateLocalizationCredits();
            if (checkWinVer() < 22000)
            {
                style.Visibility = Visibility.Collapsed;
                styleL.Visibility = Visibility.Collapsed;
            }
            var current = MicaWPFServiceUtility.ThemeService.CurrentTheme;
            theme.SelectedIndex = current == WindowsTheme.Dark ? 1 : 0;
            switch (Settings.Default.style)
            {
                case "Mica":
                    style.SelectedIndex = 0; break;
                case "Tabbed":
                    style.SelectedIndex = 1; break;
                case "Acrylic":
                    style.SelectedIndex = 2; break;
                case "None":
                    style.SelectedIndex = 3; break;
            }
            isLoaded = true;
        }
        private int checkWinVer()
        {
            string keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            string valueName = "CurrentBuild";

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                if (key != null)
                {
                    object value = key.GetValue(valueName);

                    if (value != null && int.TryParse(value.ToString(), out int build))
                    {
                        return build;
                    }
                }
            }
            return 19045;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://adderly.top") { UseShellExecute = true });
        }

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://boosty.to/adderly") { UseShellExecute = true });
        }

        private void Image_MouseLeftButtonUp_2(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://t.me/adderly324") { UseShellExecute = true });
        }

        private void Image_MouseLeftButtonUp_3(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://youtube.com/@MakuAdarii") { UseShellExecute = true });
        }

        private void theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoaded)
            {
                switch (theme.SelectedIndex)
                {
                    case 0:
                        Settings.Default.theme = "Light";
                        MicaWPFServiceUtility.ThemeService.ChangeTheme(MicaWPF.Core.Enums.WindowsTheme.Light);
                        ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        mw.Foreground = System.Windows.Media.Brushes.Black;
                        mw.Separator.Stroke = System.Windows.Media.Brushes.Black;
                        break;
                    case 1:
                        Settings.Default.theme = "Dark";
                        MicaWPFServiceUtility.ThemeService.ChangeTheme(MicaWPF.Core.Enums.WindowsTheme.Dark);
                        ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        mw.Foreground = System.Windows.Media.Brushes.White;
                        mw.Separator.Stroke = System.Windows.Media.Brushes.White;
                        break;
                }
                Settings.Default.Save();
            }
        }

                private static readonly string[] Languages =
        {
            "en", "ru", "ua", "cz", "de",
            "es", "pl", "et", "zh", "ja", "tl"
        };
        
        private void lang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded)
                return;
        
            int index = lang.SelectedIndex;
        
            if (index < 0 || index >= Languages.Length)
                return;
        
            string selectedLang = Languages[index];
        
            Settings.Default.lang = selectedLang;
            Settings.Default.langSI = index;
            Settings.Default.Save();
        
            mw.LoadLang(selectedLang);
        
            relang();
            UpdateLocalizationCredits();
        }
        private void relang()
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var ab = MainWindow.Localization.LoadLocalization(languageCode, "ab");
            var b = MainWindow.Localization.LoadLocalization(languageCode, "base");
            credL.Text = ab["main"]["credL"];
            label.Text = ab["main"]["label"];
            web.Content = ab["main"]["atop"];
            langL.Text = ab["main"]["lang"];
            styleL.Text = ab["main"]["st"];
            l.Content = " " + ab["main"]["l"];
            d.Content = " " + ab["main"]["d"];
            themeL.Text = ab["main"]["th"];
            off.Content = " " + b["def"]["off"];
            string buildVersion;
            var assembly = Assembly.GetExecutingAssembly();

        }

        private void UpdateLocalizationCredits()
        {
            string lang = Settings.Default.lang ?? "en";

            if (lang == "cz")
            {
                credLang.Visibility = Visibility.Visible;
                credLangtext.Visibility = Visibility.Visible;

                credLang.Text = "Pomohl s lokalizací:";
                credLangtext.Text = "qCLairvoyant";
            }
            else if (lang == "de")
            {
                credLang.Visibility = Visibility.Visible;
                credLangtext.Visibility = Visibility.Visible;

                credLang.Text = "Hilfe bei der Lokalisierung:";
                credLangtext.Text = "Scorazio";
            }
            else if (lang == "pl")
            {
                credLang.Visibility = Visibility.Visible;
                credLangtext.Visibility = Visibility.Visible;

                credLang.Text = "Pomoc w lokalizacji:";
                credLangtext.Text = "dfa_jk";
            }
            else if (lang == "et")
            {
                credLang.Visibility = Visibility.Visible;
                credLangtext.Visibility = Visibility.Visible;

                credLang.Text = "Aitas lokaliseerimisega:";
                credLangtext.Text = "KirTeanEesti";
            }
            else
            {
                credLang.Visibility = Visibility.Collapsed;
                credLangtext.Visibility = Visibility.Collapsed;
            }
        }

        private void style_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoaded)
            {
                switch (style.SelectedIndex)
                {
                    case 0:
                        MicaWPFServiceUtility.ThemeService.EnableBackdrop(mw, BackdropType.Mica);
                        Settings.Default.style = "Mica";
                        break;
                    case 1:
                        MicaWPFServiceUtility.ThemeService.EnableBackdrop(mw, BackdropType.Tabbed);
                        Settings.Default.style = "Tabbed";
                        break;
                    case 2:
                        MicaWPFServiceUtility.ThemeService.EnableBackdrop(mw, BackdropType.Acrylic);
                        Settings.Default.style = "Acrylic";
                        break;
                    case 3:
                        MicaWPFServiceUtility.ThemeService.EnableBackdrop(mw, BackdropType.None);
                        Settings.Default.style = "None";
                        break;
                }
            }
        }
    }
}
