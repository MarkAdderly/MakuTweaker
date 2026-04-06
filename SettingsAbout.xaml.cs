using MakuTweakerNew.Properties;
using MicaWPF.Core.Enums;
using MicaWPF.Core.Services;
using Microsoft.Win32;
using ModernWpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace MakuTweakerNew
{
    public partial class SettingsAbout : System.Windows.Controls.Page
    {
        private readonly MainWindow mw = (MainWindow)Application.Current.MainWindow;
        private bool isLoaded = false;

        private readonly string[] _languages = { "en", "ru", "ua", "cz", "de", "es", "pl", "et", "zh", "ja", "tl" };

        private readonly Dictionary<string, (string Label, string Name)> _translators = new()
        {
            ["cz"] = ("Pomohl s lokalizací:", "qCLairvoyant"),
            ["de"] = ("Hilfe bei der Lokalisierung:", "Scorazio"),
            ["pl"] = ("Pomoc w lokalizaci:", "dfa_jk"),
            ["et"] = ("Aitas lokaliseerimisega:", "KirTeanEesti")
        };

        public SettingsAbout()
        {
            InitializeComponent();

            credN.Text = "Mark Adderly\nNikitori\nNikitori, Massgrave";
            lang.SelectedIndex = Settings.Default.langSI;

            if (WinHelper.GetWindowsBuild() < 22000)
            {
                style.Visibility = styleL.Visibility = Visibility.Collapsed;
            }

            theme.SelectedIndex = MicaWPFServiceUtility.ThemeService.CurrentTheme == WindowsTheme.Dark ? 1 : 0;

            style.SelectedIndex = Settings.Default.style switch
            {
                "Tabbed" => 1,
                "Acrylic" => 2,
                "None" => 3,
                _ => 0
            };

            relang();
            UpdateLocalizationCredits();
            isLoaded = true;
        }

        private void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        private void Button_Click(object sender, RoutedEventArgs e) => OpenUrl("https://adderly.top");
        private void Image_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenUrl("https://boosty.to/adderly");
        private void Image_MouseLeftButtonUp_2(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenUrl("https://t.me/adderly324");
        private void Image_MouseLeftButtonUp_3(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenUrl("https://youtube.com/@MakuAdarii");

        private void theme_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;

            bool isDark = theme.SelectedIndex == 1;
            Settings.Default.theme = isDark ? "Dark" : "Light";

            MicaWPFServiceUtility.ThemeService.ChangeTheme(isDark ? WindowsTheme.Dark : WindowsTheme.Light);
            ThemeManager.Current.ApplicationTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

            Brush color = isDark ? Brushes.White : Brushes.Black;
            mw.Foreground = mw.Separator.Stroke = color;

            Settings.Default.Save();
        }

        private void lang_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;

            if (lang.SelectedIndex >= 0 && lang.SelectedIndex < _languages.Length)
            {
                Settings.Default.lang = _languages[lang.SelectedIndex];
            }

            Settings.Default.langSI = lang.SelectedIndex;
            Settings.Default.Save();

            mw.LoadLang(Settings.Default.lang);
            relang();
            UpdateLocalizationCredits();
        }

        private void style_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;

            var (type, name) = style.SelectedIndex switch
            {
                1 => (BackdropType.Tabbed, "Tabbed"),
                2 => (BackdropType.Acrylic, "Acrylic"),
                3 => (BackdropType.None, "None"),
                _ => (BackdropType.Mica, "Mica")
            };

            MicaWPFServiceUtility.ThemeService.EnableBackdrop(mw, type);
            Settings.Default.style = name;
            Settings.Default.Save();
        }

        private void relang()
        {
            var code = Settings.Default.lang ?? "en";
            var ab = MainWindow.Localization.LoadLocalization(code, "ab")["main"];
            var b = MainWindow.Localization.LoadLocalization(code, "base")["def"];

            credL.Text = ab["credL"];
            label.Text = ab["label"];
            web.Content = ab["atop"];
            langL.Text = ab["lang"];
            styleL.Text = ab["st"];
            themeL.Text = ab["th"];
            l.Content = " " + ab["l"];
            d.Content = " " + ab["d"];
            off.Content = " " + b["off"];
        }

        private void UpdateLocalizationCredits()
        {
            if (_translators.TryGetValue(Settings.Default.lang ?? "en", out var credits))
            {
                credLang.Visibility = credLangtext.Visibility = Visibility.Visible;
                credLang.Text = credits.Label;
                credLangtext.Text = credits.Name;
            }
            else
            {
                credLang.Visibility = credLangtext.Visibility = Visibility.Collapsed;
            }
        }
    }
}