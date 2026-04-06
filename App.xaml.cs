using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using MicaWPF.Styles;
using MicaWPF.Core.Enums;
using MakuTweakerNew.Properties;

namespace MakuTweakerNew
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Environment.SetEnvironmentVariable("LHM_NO_RING0", "1");
            base.OnStartup(e);
        }
        private readonly string logFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public App()
        {
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                HandleCrash("Unhandled Task Exception", e.Exception, 3);
                e.SetObserved();
            };
        }


        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            HandleCrash("Unhandled UI Exception", e.Exception, 2);
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleCrash("Unhandled Critical Exception", e.ExceptionObject as Exception, 1);
        }


        private void HandleCrash(string errorType, Exception? ex, int exitCode)
        {
            if (ex == null) return;

            Exception logException = ex.InnerException ?? ex;

            string errorDetails = $"MakuTweaker Mar2026 Crash [{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{errorType}\n\n" +
                                  GetExceptionDetails(logException);

            try
            {
                Directory.CreateDirectory(logFolder);
                string logFilePath = Path.Combine(logFolder, $"makutw-crash_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

                string chatMessage = "If MakuTweaker crashed through no fault of your own, please report this crash in the GitHub Repository: https://github.com/MarkAdderly/MakuTweaker" +
                                     "Если MakuTweaker крашнулся не по вашей вине, то, пожалуйста, сообщите об этом на GitHub репозитории:\nhttps://github.com/MarkAdderly/MakuTweaker";

                errorDetails += "\n\n" + chatMessage;
                File.WriteAllText(logFilePath, errorDetails);

                MessageBox.Show($"Unfortunately, MakuTweaker Has Crashed! :(\n\nError: {logException.Message}\n\nCrash Log Saved To Desktop.",
                                "MakuTweaker Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                MessageBox.Show($"Unfortunately, MakuTweaker Has Crashed! :(\n\nError: {logException.Message}\n\nCrash Log Failed to Save.",
                                "MakuTweaker Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Application.Current.Shutdown(exitCode);
        }

        private string GetExceptionDetails(Exception ex)
        {
            return $"[Message]\n{ex.Message}\n\n" +
                   $"[StackTrace]\n{ex.StackTrace}\n\n" +
                   $"[TargetSite]\n{ex.TargetSite}\n\n" +
                   $"[Data]\n{(ex.Data.Count > 0 ? string.Join(", ", ex.Data.Keys) : "No Data")}\n\n";
        }
    }
}
