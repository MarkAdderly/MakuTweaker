using Microsoft.Win32;

namespace MakuTweakerNew
{
    internal static class WinHelper
    {
        public static int GetWindowsBuild()
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            object? value = key?.GetValue("CurrentBuild");
            if (value != null && int.TryParse(value.ToString(), out int build))
                return build;
            return 19045;
        }

        public static string GetWindowsEdition()
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("EditionID")?.ToString() ?? string.Empty;
        }
    }
}
