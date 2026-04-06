using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MakuTweakerNew.Properties;
using ModernWpf.Controls;

namespace MakuTweakerNew
{
    public partial class ILOVEMAKUTWEAKERDialog : ContentDialog
    {
        public TaskCompletionSource<int> TaskCompletionSource { get; } = new();

        public ILOVEMAKUTWEAKERDialog(string app)
        {
            InitializeComponent();
            var lang = Settings.Default.lang ?? "en";
            var uwp = MainWindow.Localization.LoadLocalization(lang, "uwp")["main"];

            CloseButtonText = uwp["suredialogNS"];
            textBlock.TextAlignment = TextAlignment.Left;

            var segoe = new FontFamily("Segoe UI");
            var segoeBold = new FontFamily("Segoe UI Semibold");

            textBlock.Inlines.Add(new Run($"{uwp["suredialogT1"]} {app} {uwp["suredialogT2"]}\n") { FontSize = 14, FontFamily = segoe });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run($"{uwp["suredialogT3"]}\n") { FontSize = 14, FontFamily = segoe });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run(uwp["suredialogT4"]) { FontSize = 18, FontFamily = segoeBold });
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PrimaryButtonText = ILOVEMAKUTWEAKER.Text == "ILOVEMAKUTWEAKER" ? "OK" : string.Empty;
        }

        private void CloseDialog(int result)
        {
            TaskCompletionSource.SetResult(result);
            Hide();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
            => CloseDialog(1);

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
            => CloseDialog(0);
    }
}