using MakuTweakerNew.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MakuTweakerNew
{
    public partial class PCI : Page
    {
        private dynamic _pci = null!;
        private bool isCompactMode = false;
        private MainWindow mw = (MainWindow)Application.Current.MainWindow;
        private List<GpuInfo> _gpus = new();
        private List<StorageInfo> _storageDevices = new();
        private List<RamStickInfo> _ramSticks = new();
        private DateTime lastCompactToggle = DateTime.MinValue;

        public PCI()
        {
            Environment.SetEnvironmentVariable("LHM_NO_RING0", "1");
            InitializeComponent();
            PreviewKeyDown += PCI_PreviewKeyDown;
            LoadLang();
            ShowRamInfo();
            ShowCpuInfo();
            ShowCpuExtraInfo();
            ShowMotherboardInfo();
            LoadGpuList();
            LoadStorageList();
            ShowComputerInfo();
            ShowSecurityInfo();
            LoadRamSticks();
        }

        private void FadeOut(UIElement element)
        {
            if (element.Visibility != Visibility.Visible || element.Opacity < 1) return;

            DoubleAnimation fade = new(1, 0, TimeSpan.FromMilliseconds(250)) { FillBehavior = FillBehavior.Stop };
            fade.Completed += (s, e) => { element.Visibility = Visibility.Hidden; element.Opacity = 1; };
            element.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void PCI_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5 || (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S))
            {
                SaveDataToTxt();
                FadeOut(buttontooltip);
                e.Handled = true;
            }
            else if (e.Key == Key.F3 && (DateTime.Now - lastCompactToggle).TotalSeconds > 1)
            {
                lastCompactToggle = DateTime.Now;
                isCompactMode = !isCompactMode;
                ApplyCompactMode();
                e.Handled = true;
            }
        }

        private void ApplyCompactMode()
        {
            var pci = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "pci");
            var visibility = isCompactMode ? Visibility.Collapsed : Visibility.Visible;

            UIElement[] details =
            {
                labelcpu, labelRAM, video, ssdLabel, ramslabel, MOTHERBOARD, btitle, benchmarkSection,
                videoComboBox, ramStickComboBox, ssdComboBox, ramStickSection, biosDateRow, ddrl, ddre,
                freql, freq, cpucorel, cpucore, threadsl, threads, corespeedl, corespeed, l3cashl, l3cash,
                vraml, vram, bmodel, pcModel, ssdnLabel, ssdnValue, cpuCoreRow, cpuThreadRow, cpuSpeedRow,
                cpuCacheRow, gpuVramRow, ramTypeRow, ramFreqRow, ssdNameRow
            };

            foreach (var el in details) el.Visibility = visibility;

            if (isCompactMode)
            {
                bmanu.Text = pci["main"]["full_model"];
                cpul.Text = pci["main"]["full_cpu"];
                videol.Text = pci["main"]["full_gpu"];
                raml.Text = pci["main"]["full_ram"];
                mbnamel.Text = pci["main"]["full_motherboard"];
                ssdcLabel.Text = pci["main"]["full_usbssd"];

                rama.Text = $"{rama.Text} // {ddre.Text} // {freq.Text}";
                cpue.Text = $"{cpue.Text} // {cpucore.Text} // {threads.Text}";
                pcManufacturer.Text = $"{pcManufacturer.Text} {pcModel.Text}";
                biosver.Text = $"{biosver.Text} // {biosdate.Text}".Replace(" // N/A", "");

                if (_gpus.Any())
                {
                    var g = _gpus.OrderByDescending(x => x.VRamBytes).First();
                    videon.Text = $"{g.Name} // {g.VRamFormatted}";
                }

                if (_storageDevices.Any())
                {
                    ulong total = (ulong)_storageDevices.Sum(d => (long)d.CapacityBytes);
                    var parts = _storageDevices.Select(d => $"{(string.IsNullOrWhiteSpace(d.Type) ? "" : d.Type + " ")}{d.CapacityFormatted}");
                    ssdcValue.Text = $"{total / 1099511627776.0:0.##} TB ({string.Join(" + ", parts)})";
                }
                FadeOut(buttontooltip);
            }
            else
            {
                LoadLang();
                ShowCpuInfo();
                ShowRamInfo();
                ShowComputerInfo();
                LoadGpuList();
                LoadStorageList();
                ShowMotherboardInfo();
            }
        }

        private async Task RunBenchmarkAsync(bool multi)
        {
            var controls = new Control[] { singleBench, multiBench, lookresults, mw.Category, ssdComboBox, videoComboBox, ramStickComboBox };
            foreach (var c in controls) c.IsEnabled = false;

            var pci = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "pci");
            benchmarkResultText.Text = pci["main"][multi ? "running_multicore" : "running"];

            var result = await Task.Run(() =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                long totalOps = 0;
                int threads = multi ? Environment.ProcessorCount : 1;

                Parallel.For(0, threads, i =>
                {
                    double a = 1.000001, b = 1.000002;
                    long x = 1234567, local = 0;
                    var rnd = new Random(i * 37 + Environment.TickCount);
                    while (sw.ElapsedMilliseconds < 10000)
                    {
                        for (int k = 0; k < 200000; k++)
                        {
                            a = Math.Sin(a) * Math.Cos(b) + Math.Sqrt(Math.Abs(a + b));
                            b = a * 0.999999 + b * 0.000001 + rnd.NextDouble();
                            x = (x * 1664525 + 1013904223) & 0xFFFFFFFF;
                            local += 3;
                        }
                    }
                    System.Threading.Interlocked.Add(ref totalOps, local);
                });

                return (totalOps / sw.Elapsed.TotalSeconds) / 100000.0;
            });

            benchmarkResultText.Text = $"{pci["main"][multi ? "test1multi" : "test1"]}\n{pci["main"]["test2"]} {result:N0} {pci["main"]["test3"]}";
            foreach (var c in controls) c.IsEnabled = true;
        }

        private async void singleBench_Click(object sender, RoutedEventArgs e) => await RunBenchmarkAsync(false);
        private async void multiBench_Click(object sender, RoutedEventArgs e) => await RunBenchmarkAsync(true);

        private void LoadLang()
        {
            _pci = MainWindow.Localization.LoadLocalization(Settings.Default.lang ?? "en", "pci");
            var m = _pci["main"];

            label.Text = m["label"];
            var mappings = new (TextBlock tb, string key)[]
            {
                (labelcpu, "processorlabel"), (cpul, "processorname"), (cpucorel, "processorcores"), (threadsl, "processorthr"),
                (corespeedl, "processorfreq"), (l3cashl, "processorcache"), (labelRAM, "ramlabel"), (raml, "ramtotal"),
                (ddrl, "ramddr"), (freql, "ramfreq"), (MOTHERBOARD, "mblabel"), (mbnamel, "mbname"), (biosverl, "mbver"),
                (biosdatel, "mbdate"), (video, "vlabel"), (videol, "vname"), (vraml, "vmem"), (ssdLabel, "ssdl"),
                (ssdnLabel, "sname"), (ssdcLabel, "smem"), (benchmarkLabel, "benchtitle"), (btitle, "branding"),
                (bmanu, "manu"), (bmodel, "modeln"), (tpml, "tpmtitle"), (ramslabel, "ramsticktitle"), (ramsmanu, "manu"),
                (capacram, "capac"), (partnuml, "partnum")
            };

            foreach (var (tb, key) in mappings) tb.Text = m[key];

            singleBench.Content = m["benchbutton"];
            multiBench.Content = m["benchbutton2"];
            lookresults.Content = m["lookresulbutton"];
            benchmarkResultText.Text = m["benchtip"] + "\n";
            pci_tooltip.Content = m["tooltip"];
        }

        private void ShowCpuInfo()
        {
            try
            {
                using var s = new ManagementObjectSearcher("select Name, NumberOfCores, NumberOfLogicalProcessors from Win32_Processor");
                foreach (var i in s.Get())
                {
                    cpue.Text = i["Name"]?.ToString()?.Trim() ?? "Unknown";
                    cpucore.Text = i["NumberOfCores"]?.ToString();
                    threads.Text = i["NumberOfLogicalProcessors"]?.ToString();
                }
            }
            catch { cpue.Text = "Error reading CPU"; }
        }

        private void ShowCpuExtraInfo()
        {
            try
            {
                using var s = new ManagementObjectSearcher("select MaxClockSpeed, L3CacheSize from Win32_Processor");
                foreach (var i in s.Get())
                {
                    corespeed.Text = $"{Math.Round(Convert.ToInt32(i["MaxClockSpeed"]) / 1000.0, 2)} GHz";
                    l3cash.Text = $"{Math.Round(Convert.ToInt32(i["L3CacheSize"]) / 1024.0, 1)} MB";
                }
            }
            catch (Exception ex) { corespeed.Text = ex.Message; }
        }

        private void ShowRamInfo()
        {
            try
            {
                ulong total = 0; int type = 0, speed = 0;
                using var s = new ManagementObjectSearcher("SELECT Capacity, MemoryType, SMBIOSMemoryType, Speed FROM Win32_PhysicalMemory");
                foreach (ManagementObject i in s.Get())
                {
                    total += (ulong)(i["Capacity"] ?? 0UL);
                    speed = Math.Max(speed, Convert.ToInt32(i["Speed"] ?? 0));
                    int t = Convert.ToInt32(i["SMBIOSMemoryType"] ?? i["MemoryType"] ?? 0);
                    if (type == 0) type = t;
                }

                string mType = type switch { 20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", 31 => "DDR5", 30 => "LPDDR4", 32 => "LPDDR5", _ => "N/A" };
                rama.Text = $"{Math.Round(total / 1073741824.0)} GB";
                ddre.Text = mType;
                freq.Text = speed > 0 ? $"{speed} MHz" : "N/A";
            }
            catch { rama.Text = ddre.Text = freq.Text = "N/A"; }
        }

        private void ShowMotherboardInfo()
        {
            try
            {
                using var s1 = new ManagementObjectSearcher("SELECT Product, Manufacturer FROM Win32_BaseBoard");
                foreach (var i in s1.Get()) mbname.Text = WrapAfterWords($"{i["Manufacturer"]} {i["Product"]}");

                using var s2 = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
                foreach (var i in s2.Get())
                {
                    biosver.Text = i["SMBIOSBIOSVersion"]?.ToString();
                    string d = i["ReleaseDate"]?.ToString() ?? "";
                    biosdate.Text = d.Length >= 8 ? $"{d[6..8]}.{d[4..6]}.{d[0..4]}" : "Unknown";
                }
            }
            catch { mbname.Text = "Error"; }
        }

        private void LoadStorageList()
        {
            _storageDevices = StorageHelper.GetAllStorageDevices().OrderByDescending(d => d.CapacityBytes).ToList();
            ssdComboBox.Items.Clear();
            if (!_storageDevices.Any()) { ssdnValue.Text = ssdcValue.Text = "N/A"; return; }
            for (int i = 0; i < _storageDevices.Count; i++) ssdComboBox.Items.Add($"{i + 1}. {_storageDevices[i].Name}");
            ssdComboBox.SelectedIndex = 0;
        }

        private void LoadGpuList()
        {
            _gpus = GpuHelper.GetAllGpus().OrderByDescending(g => g.VRamBytes).ToList();
            videoComboBox.Items.Clear();
            if (!_gpus.Any()) { videon.Text = vram.Text = "N/A"; return; }
            for (int i = 0; i < _gpus.Count; i++) videoComboBox.Items.Add($"{i + 1}. {_gpus[i].Name}");
            videoComboBox.SelectedIndex = 0;
        }

        private void SSDComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ssdComboBox.SelectedIndex < 0) return;
            var s = _storageDevices[ssdComboBox.SelectedIndex];
            ssdnValue.Text = s.Name; ssdcValue.Text = s.CapacityFormatted;
        }

        private void VideoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (videoComboBox.SelectedIndex < 0) return;
            var g = _gpus[videoComboBox.SelectedIndex];
            videon.Text = g.Name; vram.Text = g.VRamFormatted;
        }

        private void ShowComputerInfo()
        {
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
                var i = s.Get().Cast<ManagementObject>().FirstOrDefault();
                string m = i?["Manufacturer"]?.ToString() ?? "", mod = i?["Model"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(m) || mod.Contains("To Be Filled", StringComparison.OrdinalIgnoreCase)) throw new Exception();
                pcManufacturer.Text = m; pcModel.Text = mod;
                computerSection.Visibility = Visibility.Visible;
                labelcpu.Margin = new Thickness(0, 20, 0, 0);
            }
            catch { computerSection.Visibility = Visibility.Collapsed; labelcpu.Margin = new Thickness(0); }
        }

        private void ShowSecurityInfo()
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\cimv2\security\microsofttpm");
                scope.Connect();
                using var s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT IsEnabled_InitialValue FROM Win32_Tpm"));
                foreach (var i in s.Get()) tpmStatus.Text = Convert.ToBoolean(i["IsEnabled_InitialValue"]) ? _pci["main"]["tpmy"] : _pci["main"]["tpmn"];
            }
            catch { tpmStatus.Text = "N/A"; }
        }

        private void LoadRamSticks()
        {
            try
            {
                _ramSticks.Clear();
                using var s = new ManagementObjectSearcher("SELECT Manufacturer, Capacity, Speed, PartNumber FROM Win32_PhysicalMemory");
                foreach (var i in s.Get()) _ramSticks.Add(new RamStickInfo { Manufacturer = i["Manufacturer"]?.ToString()?.Trim() ?? "N/A", CapacityBytes = (ulong)i["Capacity"], Speed = Convert.ToInt32(i["Speed"]), PartNumber = i["PartNumber"]?.ToString()?.Trim() ?? "N/A" });
                ramStickComboBox.Items.Clear();
                for (int i = 0; i < _ramSticks.Count; i++) ramStickComboBox.Items.Add($"{i + 1}. {_ramSticks[i].CapacityFormatted} — {_ramSticks[i].Manufacturer}");
                if (_ramSticks.Any()) ramStickComboBox.SelectedIndex = 0;
            }
            catch { ramStickManufacturer.Text = "Error"; }
        }

        private void ramStickComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ramStickComboBox.SelectedIndex < 0) return;
            var s = _ramSticks[ramStickComboBox.SelectedIndex];
            ramStickManufacturer.Text = s.Manufacturer; ramStickCapacity.Text = s.CapacityFormatted; ramStickPart.Text = s.PartNumber;
        }

        private void SaveDataToTxt()
        {
            var p = _pci["main"];
            var dialog = new SaveFileDialog { Filter = "TXT File| *.txt", FileName = "MakuTweaker System Info.txt" };
            if (dialog.ShowDialog() != true) return;

            StringBuilder sb = new();
            sb.AppendLine($"MakuTweaker // {DateTime.Now}\n\n=== {p["branding"]} ===\n{p["manu"]} {pcManufacturer.Text}\n{p["modeln"]} {pcModel.Text}\n");
            sb.AppendLine($"=== {p["processorlabel"]} ===\n{p["processorname"]} {cpue.Text}\n{p["processorcores"]} {cpucore.Text}\n{p["processorfreq"]} {corespeed.Text}\n");
            sb.AppendLine($"=== {p["ramlabel"]} ===\n{p["ramtotal"]} {rama.Text}\n{p["ramddr"]} {ddre.Text}\n{p["ramfreq"]} {freq.Text}\n");
            sb.AppendLine($"=== {p["mblabel"]} ===\n{p["mbname"]} {mbname.Text}\n{p["mbver"]} {biosver.Text}\n=== {p["tpmtitle"]} ===\n{tpmStatus.Text}\n");

            File.WriteAllText(dialog.FileName, sb.ToString());
            MessageBox.Show("Saved successfully!", "MakuTweaker", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LookResults_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://adderly.top/makubench") { UseShellExecute = true });
        private string WrapAfterWords(string t, int n = 5) => string.Join(Environment.NewLine, t.Split(' ').Select((w, i) => new { w, i }).GroupBy(x => x.i / n).Select(g => string.Join(" ", g.Select(x => x.w))));
    }

    public class GpuInfo
    {
        public string Name { get; set; } = "";
        public ulong VRamBytes { get; set; }
        public string VRamFormatted => VRamBytes == 0 ? "N/A" : $"{VRamBytes / 1073741824.0:0.##} GB";
    }

    public static class GpuHelper
    {
        public static List<GpuInfo> GetAllGpus()
        {
            var list = new List<GpuInfo>();
            try
            {
                using var f = Vortice.DXGI.DXGI.CreateDXGIFactory1<Vortice.DXGI.IDXGIFactory1>();
                uint i = 0;
                while (f.EnumAdapters1(i++, out var a).Success)
                {
                    var d = a.Description1;
                    if (d.Description.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)) continue;
                    list.Add(new GpuInfo { Name = d.Description.Trim(), VRamBytes = d.DedicatedVideoMemory });
                }
            }
            catch { }
            return list;
        }
    }

    public class StorageInfo
    {
        public string Name { get; set; } = "";
        public ulong CapacityBytes { get; set; }
        public string Type { get; set; } = "";
        public string CapacityFormatted => CapacityBytes == 0 ? "N/A" : (CapacityBytes < 1099511627776 ? $"{CapacityBytes / 1073741824.0:0.#} GB" : $"{CapacityBytes / 1099511627776.0:0.##} TB");
    }

    public class RamStickInfo
    {
        public string Manufacturer { get; set; } = "";
        public ulong CapacityBytes { get; set; }
        public int Speed { get; set; }
        public string PartNumber { get; set; } = "";
        public string CapacityFormatted => $"{CapacityBytes / 1073741824.0:0.#} GB";
    }

    public static class StorageHelper
    {
        public static List<StorageInfo> GetAllStorageDevices()
        {
            var list = new List<StorageInfo>();
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Caption, Size FROM Win32_DiskDrive");
                foreach (var i in s.Get())
                {
                    string n = i["Caption"]?.ToString() ?? "Unknown";
                    if (n.Contains("Virtual") || n.Contains("iSCSI")) continue;
                    string type = n.ToLower() switch { _ when n.Contains("nvme") => "NVMe", _ when n.Contains("ssd") => "SSD", _ when n.Contains("usb") => "USB", _ => "HDD" };
                    list.Add(new StorageInfo { Name = n, CapacityBytes = (ulong)(i["Size"] ?? 0UL), Type = type });
                }
            }
            catch { }
            return list;
        }
    }
}