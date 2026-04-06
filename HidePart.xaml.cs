using MakuTweakerNew.Properties;
using ModernWpf.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MakuTweakerNew
{
    public partial class HidePart : ContentDialog
    {
        public TaskCompletionSource<decimal> TaskCompletionSource { get; } = new();

        public HidePart()
        {
            InitializeComponent();
            var lang = Settings.Default.lang ?? "en";
            var expl = MainWindow.Localization.LoadLocalization(lang, "expl")["status"];

            CloseButtonText = expl["hide"];
            PrimaryButtonText = expl["cc"];

            textBlock.TextAlignment = TextAlignment.Left;
            textBlock.MaxWidth = 500;
            textBlock.Inlines.Add(new Run(expl["hdInfo1"]) { FontSize = 18, FontFamily = new FontFamily("Segoe UI Semilight") });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run(expl["hdInfo2"]) { FontSize = 18, FontFamily = new FontFamily("Segoe UI Semibold") });
        }

        private void CloseDialog(decimal result)
        {
            TaskCompletionSource.SetResult(result);
            Hide();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
            => CloseDialog(-1);

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            long mask = 0;
            var checks = new (CheckBox cb, int bit)[]
            {
                (a, 0), (d, 3), (e, 4), (f, 5), (g, 6), (h, 7), (this.i, 8), (j, 9), (k, 10),
                (l, 11), (m, 12), (n, 13), (o, 14), (p, 15), (q, 16), (r, 17), (s, 18),
                (t, 19), (u, 20), (v, 21), (w, 22), (x, 23), (y, 24), (z, 25)
            };

            foreach (var item in checks)
                if (item.cb.IsChecked == true)
                    mask |= 1L << item.bit;

            CloseDialog((decimal)mask);
        }
    }
}