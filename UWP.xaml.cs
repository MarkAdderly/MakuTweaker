using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using ModernWpf.Controls;
using Windows.Management.Deployment;
using MakuTweakerNew.Properties;

namespace MakuTweakerNew
{
    public partial class UWP : System.Windows.Controls.Page
    {
        private readonly MainWindow mw = (MainWindow)Application.Current.MainWindow;
        private int mode;
        private bool _isChecking;
        private bool _progressShown;
        private readonly PackageManager _packageManager = new();

        private ToggleSwitch[] Bloat => new[] { u1, u2, u3, u4, u5, u6, u7, u8, u10, u11, u12, u13, u14, u18, u19, u20, u21, u22, u25, u26, u27, u28 };
        private ToggleSwitch[] Popular => new[] { u15, u16, u17, u9 };
        private ToggleSwitch[] Necessary => new[] { u23, u24 };
        private UIElement[] MainControls => new UIElement[] { b, view };
        private ToggleSwitch[] AllToggles => Bloat.Concat(Popular).Concat(Necessary).ToArray();

        public UWP()
        {
            InitializeComponent();
            LoadLang();
            Loaded += async (_, __) =>
            {
                _isChecking = true;
                var checkTask = CheckInstalledUWPAppsAsync(true);
                if (await Task.WhenAny(Task.Delay(1000), checkTask) != checkTask && _isChecking) ShowProgress(true);
                await checkTask;
                if (_progressShown) ShowProgress(false);
            };
        }

        private void Fade(UIElement el, double to, double ms)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new QuadraticEase { EasingMode = to > 0 ? EasingMode.EaseIn : EasingMode.EaseOut }
            };
            if (to > 0) el.Visibility = Visibility.Visible;
            else anim.Completed += (s, e) => el.Visibility = Visibility.Collapsed;
            el.BeginAnimation(OpacityProperty, anim);
        }

        private void ShowProgress(bool show)
        {
            _progressShown = show;
            p.Visibility = t.Visibility = show ? Visibility.Visible : Visibility.Hidden;
            p.IsIndeterminate = show;
            Fade(p, show ? 1 : 0, 400);
            Fade(t, show ? 1 : 0, 400);
        }

        private async Task CheckInstalledUWPAppsAsync(bool anim)
        {
            var lang = Settings.Default.lang ?? "en";
            var uwpStrings = MainWindow.Localization.LoadLocalization(lang, "uwp")["main"];
            t.Text = uwpStrings["chk"];
            p.Value = 0;

            var apps = GetAppData();
            var installed = await Task.Run(() => _packageManager.FindPackagesForUser(string.Empty).Select(x => x.Id.Name).ToHashSet());

            foreach (var app in apps)
            {
                bool isInstalled = app.Packages.Any(pkg => installed.Contains(pkg));
                app.Toggle.IsEnabled = isInstalled;
                if (!Popular.Contains(app.Toggle)) app.Toggle.IsOn = isInstalled;
                t.Text = $"{uwpStrings["chk"]}{app.Packages[0]}";
                p.Value++;
            }

            t.Text = uwpStrings["comp"];
            if (anim)
            {
                AnimateGroup(Bloat, true);
                AnimateGroup(MainControls, true);
            }
            b.IsEnabled = true;
            _isChecking = false;
        }

        private (ToggleSwitch Toggle, string[] Packages, string? Name)[] GetAppData() => new[]
        {
            (u1,  ["Microsoft.MixedReality.Portal"], null),
            (u2,  ["Microsoft.MicrosoftSolitaireCollection"], null),
            (u3,  ["Microsoft.Messaging"], null),
            (u4,  ["Microsoft.549981C3F5F10"], null),
            (u5,  ["Microsoft.GetHelp"], null),
            (u6,  ["Microsoft.WindowsFeedbackHub"], null),
            (u7,  ["Microsoft.Windows.DevHome"], null),
            (u8,  ["Microsoft.MSPaint", "Microsoft.3DBuilder", "Microsoft.Microsoft3DViewer"], null),
            (u9,  ["Microsoft.YourPhone"], null),
            (u10, ["Microsoft.WindowsMaps"], null),
            (u11, ["Microsoft.PowerAutomateDesktop"], null),
            (u12, ["Clipchamp.Clipchamp"], null),
            (u13, ["microsoft.windowscommunicationsapps"], null),
            (u14, ["Microsoft.Office.OneNote"], null),
            (u15, ["Microsoft.ZuneMusic"], null),
            (u16, ["Microsoft.ZuneVideo"], null),
            (u17, ["Microsoft.WindowsCamera"], null),
            (u18, ["Microsoft.BingNews"], null),
            (u19, ["Microsoft.BingWeather"], null),
            (u20, ["Microsoft.MicrosoftStickyNotes"], null),
            (u21, ["Microsoft.Getstarted"], null),
            (u22, ["Microsoft.WindowsSoundRecorder"], null),
            (u23, new[] { "Microsoft.WindowsStore" }, "Microsoft Store"),
            (u24, new[] { "Microsoft.XboxApp", "Microsoft.GamingApp", "Microsoft.Xbox.TCUI", "Microsoft.XboxSpeechToTextOverlay", "Microsoft.XboxGameCallableUI" }, "Xbox"),
            (u25, ["Microsoft.People", "Microsoft.WindowsPeopleExperienceHost"], null),
            (u26, ["Microsoft.SkypeApp"], null),
            (u27, ["Microsoft.WindowsAlarms"], null),
            (u28, ["Microsoft.OutlookForWindows"], null)
        };

        private async void b_Click(object sender, RoutedEventArgs e)
        {
            var lang = Settings.Default.lang ?? "en";
            var uwpStrings = MainWindow.Localization.LoadLocalization(lang, "uwp");
            var targetShortNames = new List<string>();

            foreach (var app in GetAppData())
            {
                if (app.Toggle.IsOn)
                {
                    if (app.Name != null)
                    {
                        var diag = new ILOVEMAKUTWEAKERDialog(app.Name);
                        await diag.ShowAsync();
                        if (await diag.TaskCompletionSource.Task == 0) return;
                    }
                    targetShortNames.AddRange(app.Packages);
                }
            }

            if (targetShortNames.Count == 0) { mw.ChSt(uwpStrings["status"]["noapps"]); return; }

            ShowProgress(true);
            p.IsIndeterminate = false;
            p.Maximum = targetShortNames.Count;
            p.Value = 0;
            mw.Category.IsEnabled = mw.ABCB.IsEnabled = false;

            AnimateGroup(AllToggles, false);
            AnimateGroup(MainControls, false);

            var allInstalled = await Task.Run(() => _packageManager.FindPackagesForUser(string.Empty).ToList());

            foreach (var shortName in targetShortNames)
            {
                t.Text = $"{uwpStrings["status"]["started"]} {p.Value}/{targetShortNames.Count}";
                var foundPackages = allInstalled.Where(x => x.Id.Name.Equals(shortName, StringComparison.OrdinalIgnoreCase));

                foreach (var pkg in foundPackages)
                {
                    try
                    {
                        await _packageManager.RemovePackageAsync(pkg.Id.FullName).AsTask();
                    }
                    catch { /* ... */ }
                }
                p.Value++;
            }

            SystemSounds.Asterisk.Play();
            mw.ChSt(uwpStrings["status"]["complete"]);
            mw.Category.IsEnabled = mw.ABCB.IsEnabled = true;
            foreach (var ts in AllToggles) ts.IsOn = ts.IsEnabled = false;

            view.SelectedIndex = 0;
            _ = CheckInstalledUWPAppsAsync(true);
        }

        private void AnimateGroup(IEnumerable<UIElement> group, bool show)
        {
            foreach (var el in group) Fade(el, show ? 1 : 0, 300);
        }

        private async void view_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            view.IsEnabled = false;
            if (view.SelectedIndex == 0) { AnimateGroup(Popular, false); AnimateGroup(Necessary, false); }
            else if (view.SelectedIndex == 1) { AnimateGroup(Popular, true); AnimateGroup(Necessary, false); }
            else { AnimateGroup(Popular, true); AnimateGroup(Necessary, true); }
            mode = view.SelectedIndex;
            await Task.Delay(300);
            view.IsEnabled = true;
        }

        private void LoadLang()
        {
            var lang = Settings.Default.lang ?? "en";
            var uwp = MainWindow.Localization.LoadLocalization(lang, "uwp")["main"];
            label.Text = uwp["label"];
            info1.Text = uwp["info1"];
            info2.Text = uwp["info2"];
            mode1.Content = uwp["mode1"];
            mode2.Content = uwp["mode2"];
            mode3.Content = uwp["mode3"];
            b.Content = uwp["b"];
            t.Text = uwp["chk"];

            var map = new (ToggleSwitch s, string k)[] {
                (u3, "u3"), (u5, "u5"), (u6, "u6"),
                (u9, "u9"), (u10, "u10"), (u13, "u13"),
                (u15, "u15"), (u16, "u16"), (u17, "u17"),
                (u18, "u18"), (u19, "u19"), (u20, "u20"),
                (u22, "u22"), (u27, "u27")
            };

            foreach (var item in map)
            {
                item.s.OnContent = item.s.OffContent = uwp[item.k];
            }
        }
    }
}