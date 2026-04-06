using Hardcodet.Wpf.TaskbarNotification;
using MakuTweakerNew.Properties;
using MicaWPF.Controls;
using MicaWPF.Core.Enums;
using MicaWPF.Core.Services;
using Microsoft.Win32;
using ModernWpf;
using ModernWpf.Media.Animation;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace MakuTweakerNew
{
    public partial class MainWindow : MicaWindow
    {
        private NavigationTransitionInfo? _transitionInfo;
        private DispatcherTimer ExpRestart = null!;
        private bool isAnimating = false;

        public static class Localization
        {
            public static Dictionary<string, Dictionary<string, string>> LoadLocalization(string language, string category)
            {
                var locFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "loc");
                var enFile = Path.Combine(locFolder, "en.json");

                if (!File.Exists(enFile))
                {
                    throw new FileNotFoundException($"Cannot find the base en.json localization file.\nPlease reinstall MakuTweaker.");
                }

                var enContent = File.ReadAllText(enFile);
                var enData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>(enContent);

                if (enData == null || !enData.ContainsKey("categories") || !enData["categories"].ContainsKey(category))
                {
                    throw new KeyNotFoundException($"Cannot find a \"{category}\" category in the base en.json localization file.");
                }

                var result = enData["categories"][category];

                if (language == "en") return result;

                var targetFile = Path.Combine(locFolder, $"{language}.json");
                if (!File.Exists(targetFile))
                {
                    Settings.Default.lang = "en";
                    Settings.Default.Save();
                    throw new FileNotFoundException($"Cannot find a {targetFile} localization file.\nPlease reinstall MakuTweaker.\nLanguage has been changed to English.");
                }

                var targetData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, string>>>>>(File.ReadAllText(targetFile));

                if (targetData != null && targetData.ContainsKey("categories") && targetData["categories"].ContainsKey(category))
                {
                    var targetCategory = targetData["categories"][category];
                    foreach (var sub in targetCategory)
                    {
                        if (!result.ContainsKey(sub.Key)) result[sub.Key] = new Dictionary<string, string>();
                        foreach (var trans in sub.Value) result[sub.Key][trans.Key] = trans.Value;
                    }
                }

                return result;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            CheckOsVersion();
            ExpTimer();
            HandleFirstRun();
            LoadLang(Settings.Default.lang);
            _ = CheckForUpd();
        }

        private void CheckOsVersion()
        {
            if (WinHelper.GetWindowsBuild() < 14393)
            {
                var res = System.Windows.Forms.MessageBox.Show("Your version of Windows is not supported. To use MakuTweaker, update your system to Windows 10 1607 or higher. Do you want to download MakuTweaker Legacy Windows Edition?\n\nВаша версия Windows неподдерживается. Для использования MakuTweaker, обновитесь до Windows 10 1607 или выше. Вы хотите скачать MakuTweaker для старых Windows?", "MakuTweaker", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Error);
                if (res == System.Windows.Forms.DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("https://adderly.top/mt") { UseShellExecute = true });
                }
                Application.Current.Shutdown();
            }
        }

        private void HandleFirstRun()
        {
            if (!Settings.Default.firRun)
            {
                if (Enum.TryParse<WindowsTheme>(Settings.Default.theme, out var parsed)) ApplyTheme(parsed);
                else ApplyTheme(MicaWPFServiceUtility.ThemeService.CurrentTheme);
                return;
            }

            string sys = CultureInfo.CurrentCulture.Name.ToLower();
            var (code, id) = sys switch
            {
                _ when sys.StartsWith("uk-") => ("ua", 2),
                _ when sys.StartsWith("ru-") => ("ru", 1),
                _ when sys.StartsWith("cs-") => ("cz", 3),
                _ when sys.StartsWith("de-") => ("de", 4),
                _ when sys.StartsWith("es-") => ("es", 5),
                _ when sys.StartsWith("pl-") => ("pl", 6),
                _ when sys.StartsWith("et-") => ("et", 7),
                _ when sys.StartsWith("zh") => ("zh", 8),
                _ when sys.StartsWith("ja") => ("ja", 9),
                _ when sys.StartsWith("tl") => ("tl", 10),
                _ when sys.StartsWith("en-") => ("en", 0),
                _ => ("en", 0)
            };

            Settings.Default.lang = code;
            Settings.Default.langSI = id;
            var theme = MicaWPFServiceUtility.ThemeService.CurrentTheme;
            Settings.Default.theme = theme == WindowsTheme.Dark ? "Dark" : "Light";
            Settings.Default.firRun = false;
            Settings.Default.Save();
            ApplyTheme(theme);
        }

        private void ApplyTheme(WindowsTheme theme)
        {
            MicaWPFServiceUtility.ThemeService.ChangeTheme(theme);
            bool isDark = theme == WindowsTheme.Dark;
            ThemeManager.Current.ApplicationTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            Foreground = isDark ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Black;
            Separator.Stroke = Foreground;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Category.SelectedIndex == -1) return;

            var pages = new[] {
                typeof(Explorer), typeof(WindowsUpdate), typeof(SysAndRec), typeof(UWP),
                typeof(Personalization), typeof(ContextMenu), typeof(QuickSet), typeof(WindowsComponents),
                typeof(Act), typeof(Perf), typeof(SAT), typeof(ProcessMGR), typeof(PCI)
            };

            _transitionInfo = new EntranceNavigationTransitionInfo();
            MainFrame.Navigate(pages[Category.SelectedIndex], null, _transitionInfo);
            Settings.Default.lastPage = Category.SelectedIndex;
            Settings.Default.Save();
        }

        private void MicaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Category.SelectedIndex = Settings.Default.lastPage;
            if (Enum.TryParse<BackdropType>(Settings.Default.style, out var bd))
                MicaWPFServiceUtility.ThemeService.EnableBackdrop(this, bd);
        }

        public async void ChSt(string st)
        {
            if (isAnimating) return;
            isAnimating = true;
            AnimY(status, 300, 26, 0);
            status.Text = st;
            await Task.Delay(5000);
            AnimY(status, 300, 0, 33);
            isAnimating = false;
        }

        public void LoadLang(string lang)
        {
            try
            {
                var basel = Localization.LoadLocalization(lang, "base");
                var cats = new[] { c1, c2, c3, c4, c5, c6, c7, c8, c9, c10, c11, c12, c13 };
                var keys = new[] { "expl", "wu", "sr", "uwp", "per", "cm", "quick", "compon", "act", "perf", "sat", "procmgr", "pci" };

                for (int i = 0; i < cats.Length; i++) cats[i].Content = basel["catname"][keys[i]];

                rexplorer.Label = basel["lowtabs"]["rexp"];
                settingsButton.Label = basel["lowtabs"]["set"];
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "MakuTweaker Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                System.Windows.Forms.Application.Restart();
                Application.Current.Shutdown();
            }
        }

        private void AnimY(UIElement el, double dur, double from, double to)
        {
            if (el.RenderTransform is not TranslateTransform) el.RenderTransform = new TranslateTransform();
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(dur)) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } };
            ((TranslateTransform)el.RenderTransform).BeginAnimation(TranslateTransform.YProperty, anim);
        }

        public void RebootNotify(int mode)
        {
            var basel = Localization.LoadLocalization(Settings.Default.lang, "base")["def"];
            string message = mode switch { 1 => basel["rebnotify"], 2 => basel["rebnotifyexplorer"], 3 => basel["rebnotifysfc"], _ => "" };

            var trayIcon = new TaskbarIcon { Icon = new Icon(GetResourceStream("MakuTweakerNew.MakuT.ico")), ToolTipText = "MakuTweaker" };
            trayIcon.ShowBalloonTip("MakuTweaker", message, BalloonIcon.Warning);
            Task.Delay(8000).ContinueWith(_ => trayIcon.Dispatcher.Invoke(trayIcon.Dispose));
        }

        private Stream GetResourceStream(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new FileNotFoundException($"Ресурс {name} не найден.");

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            Category.SelectedIndex = -1;
            MainFrame.Navigate(typeof(SettingsAbout), null, _transitionInfo);
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e) => settingsButton.IsEnabled = Category.SelectedIndex != -1;

        private void rexplorer_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("taskkill", "/F /IM explorer.exe") { CreateNoWindow = true })?.WaitForExit();
            ExpRestart.Start();
        }

        private void ExpTimer()
        {
            ExpRestart = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            ExpRestart.Tick += (s, e) => { Process.Start("explorer.exe"); ExpRestart.Stop(); };
        }

        private async Task CheckForUpd()
        {
            try
            {
                using var client = new HttpClient();
                var json = await client.GetStringAsync("https://raw.githubusercontent.com/AdderlyMark/MakuTweaker/refs/heads/main/ver.json");
                var build = (int)JsonConvert.DeserializeObject<dynamic>(json)!.build;
                int current = int.Parse(new StreamReader(GetResourceStream("MakuTweakerNew.BuildNumber.txt")).ReadToEnd().Trim());

                if (build > current)
                {
                    var tray = new TaskbarIcon { Icon = new Icon(GetResourceStream("MakuTweakerNew.MakuT.ico")), ToolTipText = "MakuTweaker" };
                    bool isRu = Settings.Default.lang is "ru" or "ua" or "kz";
                    string msg = isRu ? "Доступно обновление MakuTweaker!\nНажмите на уведомление, чтобы перейти на страницу загрузки." : "An update for MakuTweaker is available!\nClick the notification to go to the download page.";
                    tray.ShowBalloonTip("MakuTweaker", msg, BalloonIcon.Info);
                    tray.TrayBalloonTipClicked += (s, e) => Process.Start(new ProcessStartInfo("https://adderly.top/makutweaker") { UseShellExecute = true });
                    await Task.Delay(8000);
                    tray.Dispose();
                }
            }
            catch { } // ?
        }
    }
}