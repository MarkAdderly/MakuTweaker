using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MakuTweakerNew.Properties;
using MicaWPF.Controls;
using Windows.UI.Composition.Desktop;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;

namespace MakuTweakerNew
{
    public partial class SAT : Page
    {
        public SAT()
        {
            InitializeComponent();
            LoadLang();
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var satl = MainWindow.Localization.LoadLocalization(languageCode, "sat");
            int number;
            int.TryParse(mins.Text, out number);
            hours.Text = satl["main"]["minho"] + Math.Round((double)number / 60, 2);
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void time_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mins.Text = Math.Round(time.Value * 5).ToString();
        }

        private void mins_TextChanged(object sender, TextChangedEventArgs e)
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var satl = MainWindow.Localization.LoadLocalization(languageCode, "sat");
            int number;
            int.TryParse(mins.Text, out number);
            hours.Text = satl["main"]["minho"] + Math.Round((double)number / 60, 2);
        }

        private void mins_GotFocus(object sender, RoutedEventArgs e)
        {
            mins.Dispatcher.InvokeAsync(() =>
            {
                mins.SelectAll();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }



        private void tenM_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(@"C:\Windows\System32\shutdown.exe", " -s -t 600");
        }

        private void threeM_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(@"C:\Windows\System32\shutdown.exe", " -s -t 1800");
        }

        private void oneH_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(@"C:\Windows\System32\shutdown.exe", " -s -t 3600");
        }

        private void twoH_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(@"C:\Windows\System32\shutdown.exe", " -s -t 7200");
        }

        private void fourH_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(@"C:\Windows\System32\shutdown.exe", " -s -t 14400");
        }

        private void sixH_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(@"C:\Windows\System32\shutdown.exe", " -s -t 21600");
        }

        private void shut_Click(object sender, RoutedEventArgs e)
        {
            double a, b;
            a = Convert.ToDouble(mins.Text);
            b = Convert.ToDouble(60);
            Process.Start(@"C:\Windows\System32\shutdown.exe", " -s -t " + Convert.ToString(a * b));
        }

        private void cancel_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(@"C:\Windows\System32\shutdown.exe", " -a");
        }

        private void LoadLang()
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            var satl = MainWindow.Localization.LoadLocalization(languageCode, "sat");

            label.Text = satl["main"]["label"];
            sat.Text = satl["main"]["info"];
            hours.Text = satl["main"]["minho"];
            os.Text = satl["main"]["os"];
            oned.Text = satl["main"]["oned"];
            tenM.Content = satl["main"]["tenM"];
            thirtyM.Content = satl["main"]["thirtyM"];
            oneH.Content = satl["main"]["oneH"];
            twoH.Content = satl["main"]["twoH"];
            fourH.Content = satl["main"]["fourH"];
            sixH.Content = satl["main"]["sixH"];
            shut.Content = satl["main"]["b1"];
            cancel.Content = satl["main"]["b2"];
        }
    }
}
