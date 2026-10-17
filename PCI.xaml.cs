using MakuTweakerNew.Properties;
using MicaWPF.Core.Enums;
using MicaWPF.Core.Services;
using Microsoft.Win32;
using NvAPIWrapper.Native.Display.Structures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vortice.DXGI;
using Windows.Devices.Portable;

namespace MakuTweakerNew
{
    public partial class PCI : Page
    {
        private dynamic _pci;
        private dynamic pmgrLang;
        private dynamic monitoringLang;
        private string UnknownStr => monitoringLang?["main"]?["unknown"]?.ToString() ?? "Unknown";
        MainWindow mw = (MainWindow)Application.Current.MainWindow;
        private List<GpuInfo> _gpus = new List<GpuInfo>();
        private List<StorageInfo> _storageDevices = new List<StorageInfo>();
        private List<RamStickInfo> _ramSticks = new();
        private double _lastSingleScore = 0;
        private double _lastMultiScore = 0;
        private Task _startupBenchTask;
        private CancellationTokenSource _diskBenchCts;
        private List<DisplayInfo> _displays = new List<DisplayInfo>();

        public class DisplayInfo
        {
            public string Model { get; set; } = "N/A";
            public string Resolution { get; set; } = "N/A";
            public string RefreshRate { get; set; } = "N/A";
            public bool IsPrimary { get; set; }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 1;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        private const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 4;
        private const uint EDD_GET_DEVICE_INTERFACE_NAME = 1;

        public PCI()
        {
            Environment.SetEnvironmentVariable("LHM_NO_RING0", "1");
            InitializeComponent();
            this.PreviewKeyDown += PCI_PreviewKeyDown;
            LoadLang();
            Loaded += async (_, __) => await LoadSystemInfoAsync();
        }

        private async Task LoadSystemInfoAsync()
        {
            if (_pci != null && _pci["main"] != null && _pci["main"]["loading"] != null)
            {
                loadingText.Text = _pci["main"]["loading"];
            }
            else
            {
                loadingText.Text = "Loading...";
            }

            await Task.Run(() =>
            {
                ShowRamInfo();
                ShowCpuInfo();
                ShowCpuExtraInfo();
                ShowMotherboardInfo();
                ShowComputerInfo();
                ShowSecurityInfo();
                LoadRamSticks();
                LoadDisplayInfo();
            });

            await LoadGpuListAsync();
            await LoadStorageListAsync();
            await HideProgressSmoothAsync();
            _ = UpdateFreeSpaceInBackgroundAsync();
        }

        private async Task RunRatingSingleBenchmarkAsync()
        {
            var result = await Task.Run(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                double a = 1.000001; double b = 1.000002; long x = 1234567; long ops = 0;
                var rnd = new Random(Environment.TickCount);

                while (stopwatch.ElapsedMilliseconds < 10_000)
                {
                    for (int k = 0; k < 200_000; k++)
                    {
                        a = Math.Sin(a) * Math.Cos(b) + Math.Sqrt(Math.Abs(a + b));
                        b = a * 0.999999 + b * 0.000001 + rnd.NextDouble();
                        x = (x * 1664525 + 1013904223) & 0xFFFFFFFF;
                        ops += 3;
                    }
                }
                stopwatch.Stop();
                return (ops / stopwatch.Elapsed.TotalSeconds) / 100000.0;
            });

            if (_lastSingleScore == 0)
            {
                _lastSingleScore = result;
            }
        }

        private void FadeIn(UIElement element, double durationMilliseconds)
        {
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            element.BeginAnimation(OpacityProperty, fadeInAnimation);
        }

        private async Task HideProgressSmoothAsync()
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            loadingPanel.BeginAnimation(OpacityProperty, fadeOutAnimation);
            await Task.Delay(300);
            loadingPanel.Visibility = Visibility.Collapsed;
            ring.IsActive = false;

            FadeIn(MainScrollViewer, 400);
        }

        void FadeOut(UIElement element)
        {
            if (element.Visibility != Visibility.Visible)
                return;
            if (element.ReadLocalValue(UIElement.OpacityProperty) != DependencyProperty.UnsetValue)
                return;

            DoubleAnimation fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                FillBehavior = FillBehavior.Stop
            };

            fade.Completed += (s, e) =>
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                element.Visibility = Visibility.Hidden;
            };

            element.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void PCI_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                SaveDataToTxt();
            }
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.S)
            {
                SaveDataToTxt();
                e.Handled = true;
            }
        }

        private async Task RunBenchmarkAsync(bool runMultithreadedByDefault)
        {
            singleBench.IsEnabled = false;
            multiBench.IsEnabled = false;
            lookresults.IsEnabled = false;
            mw.NavigationView_Root.IsEnabled = false;
            ssdComboBox.IsEnabled = false;
            videoComboBox.IsEnabled = false;
            ramStickComboBox.IsEnabled = false;

            const int benchmarkDurationMilliseconds = 10_000;
            var pci = MainWindow.Localization.LoadLocalization(Properties.Settings.Default.lang ?? "en", "pci");
            bool isMultithreaded = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || runMultithreadedByDefault;

            benchmarkResultText.Text = isMultithreaded
                ? $"{pci["main"]["running_multicore"]}"
                : $"{pci["main"]["running"]}";

            var result = await Task.Run(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                long totalOps = 0;

                if (isMultithreaded)
                {
                    int threads = Environment.ProcessorCount;
                    long[] threadOps = new long[threads];

                    Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
                    {
                        double a = 1.000001 + i * 0.00001;
                        double b = 1.000002 + i * 0.00002;
                        long x = 1234567 + i;
                        long localOps = 0;
                        var rnd = new Random(i * 37 + Environment.TickCount);

                        while (stopwatch.ElapsedMilliseconds < benchmarkDurationMilliseconds)
                        {
                            for (int k = 0; k < 200_000; k++)
                            {
                                a = Math.Sin(a) * Math.Cos(b) + Math.Sqrt(Math.Abs(a + b));
                                b = a * 0.999999 + b * 0.000001 + rnd.NextDouble();
                                x = (x * 1664525 + 1013904223) & 0xFFFFFFFF;
                                localOps += 3;
                            }
                        }

                        threadOps[i] = localOps;
                    });

                    totalOps = threadOps.Sum();
                }
                else
                {
                    double a = 1.000001;
                    double b = 1.000002;
                    long x = 1234567;
                    long ops = 0;
                    var rnd = new Random(Environment.TickCount);

                    while (stopwatch.ElapsedMilliseconds < benchmarkDurationMilliseconds)
                    {
                        for (int k = 0; k < 200_000; k++)
                        {
                            a = Math.Sin(a) * Math.Cos(b) + Math.Sqrt(Math.Abs(a + b));
                            b = a * 0.999999 + b * 0.000001 + rnd.NextDouble();
                            x = (x * 1664525 + 1013904223) & 0xFFFFFFFF;
                            ops += 3;
                        }
                    }

                    totalOps = ops;
                }

                stopwatch.Stop();

                double seconds = stopwatch.Elapsed.TotalSeconds;
                double score = (totalOps / seconds) / 100000.0;

                return (score, stopwatch.ElapsedMilliseconds);
            });

            string scoreText = $"{result.score:N0}";

            benchmarkResultText.Text = isMultithreaded
                ? $"{pci["main"]["test1multi"]} {pci["main"]["test2"]} {scoreText} {pci["main"]["test3"]}"
                : $"{pci["main"]["test1"]} {pci["main"]["test2"]} {scoreText} {pci["main"]["test3"]}";

            string benchType = isMultithreaded ? "multi" : "single";
            
            if (isMultithreaded)
                _lastMultiScore = result.score;
            else
                _lastSingleScore = result.score;

            if (_lastSingleScore > 0 && _lastMultiScore > 0)
            {
                _lastSingleScore = 0;
                _lastMultiScore = 0;
            }

            singleBench.IsEnabled = true;
            multiBench.IsEnabled = true;
            lookresults.IsEnabled = true;
            mw.NavigationView_Root.IsEnabled = true;
            ssdComboBox.IsEnabled = true;
            videoComboBox.IsEnabled = true;
            ramStickComboBox.IsEnabled = true;
        }

        private async void singleBench_Click(object sender, RoutedEventArgs e)
        {
            await RunBenchmarkAsync(false);
        }

        private async void multiBench_Click(object sender, RoutedEventArgs e)
        {
            await RunBenchmarkAsync(true);
        }

        private void LoadLang()
        {
            var languageCode = Properties.Settings.Default.lang ?? "en";
            _pci = MainWindow.Localization.LoadLocalization(languageCode, "pci");
            monitoringLang = MainWindow.Localization.LoadLocalization(Properties.Settings.Default.lang, "monitoring");

            label.Text = _pci["main"]["label"];

            summaryCpuCard.Header = _pci["main"]["processorlabel"];
            summaryRamCard.Header = _pci["main"]["ramlabel"];
            summaryGpuCard.Header = _pci["main"]["vlabel"];
            summaryVramCard.Header = _pci["main"]["allvram"];
            bmodel.Header = _pci["main"]["modeln"];
            summaryStorageCard.Header = _pci["main"]["allstorage"];

            rateCpuLabel.Text = _pci["main"]["processorlabel"];
            rateRamLabel.Text = _pci["main"]["ramlabel"];
            rateGpuLabel.Text = _pci["main"]["vlabel"];
            rateStorageLabel.Text = _pci["main"]["ssdl"];

            cpuSection.Header = _pci["main"]["processorlabel"];
            cpul.Header = _pci["main"]["processorname"];
            cpucorel.Header = _pci["main"]["processorcores"];
            corespeedl.Header = _pci["main"]["processorfreq"];
            l3cashl.Header = _pci["main"]["processorcache"];

            motherboardSectionEx.Header = _pci["main"]["mblabel"];
            mbnamel.Header = _pci["main"]["mbname"];
            biosverl.Header = _pci["main"]["mbver"];
            biosdatel.Header = _pci["main"]["mbdate"];

            gpuSectionEx.Header = _pci["main"]["vlabel"];
            videol.Header = _pci["main"]["vname"];
            vraml.Header = _pci["main"]["vmem"];
            gpudriverl.Header = _pci["main"]["driverversion"];
            gpudriverdatel.Header = _pci["main"]["driverdate"];

            benchmarkLabel.Text = _pci["main"]["benchtitle"];
            singleBench.Content = _pci["main"]["benchbutton"];
            multiBench.Content = _pci["main"]["benchbutton2"];
            benchmarkResultText.Text = _pci["main"]["benchtip"];
            lookresults.Content = _pci["main"]["lookresulbutton"];
            tpml.Header = _pci["main"]["tpmtitle"];

            ramStickSection.Header = _pci["main"]["ramsticktitle"];
            ramsmanu.Header = _pci["main"]["manu"];
            capacram.Header = _pci["main"]["capac"];
            partnuml.Header = _pci["main"]["partnum"];
            ramstickspeedl.Header = _pci["main"]["ramstickspeed"];

            ssdnl.Header = _pci["main"]["sname"];
            ssdcl.Header = _pci["main"]["capac"];
            ssdfreel.Header = _pci["main"]["freespace"];
            ssdtypel.Header = _pci["main"]["disktype"];
            testDiskBtn.Content = _pci["main"]["testdisk"];
            storageSection.Header = _pci["main"]["ssdl"];

            displaySection.Header = _pci["main"]["displtitle"];
            resolutionl.Header = _pci["main"]["resolution"];
            refreshratel.Header = _pci["main"]["refreshrate"];
            monitormodell.Header = _pci["main"]["monitormodel"];

            save_tooltip.Content = _pci["main"]["save"];
            copy_tooltip.Content = _pci["main"]["clipboard"];
            rateTotalLabel.Text = _pci["main"]["ratingrate"];
            ratinglabel.Text = _pci["main"]["ratingtitle"];
            ratetext.Text = _pci["main"]["ratingtitle"];
        }

        private void ShowTpmStatus(bool enabled)
        {
            Dispatcher.Invoke(() =>
            {
                if (_pci == null)
                {
                    tpmStatus.Text = enabled ? "Enabled" : "Disabled";
                    return;
                }

                tpmStatus.Text = enabled
                    ? _pci["main"]["tpmy"]
                    : _pci["main"]["tpmn"];
            });
        }

        private void ShowCpuInfo()
        {
            try
            {
                string cpuName = "";
                int coreCount = 0;
                int threadCount = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, ThreadCount FROM Win32_Processor"))
                using (var results = searcher.Get())
                {
                    foreach (var item in results)
                    {
                        cpuName = item["Name"]?.ToString()?.Trim() ?? cpuName;
                        coreCount += Convert.ToInt32(item["NumberOfCores"] ?? 0);

                        if (item["ThreadCount"] != null)
                        {
                            threadCount += Convert.ToInt32(item["ThreadCount"]);
                        }
                        item.Dispose();
                    }
                }

                if (threadCount == 0 || threadCount == coreCount)
                {
                    using (var searcherCS = new ManagementObjectSearcher("SELECT NumberOfLogicalProcessors FROM Win32_ComputerSystem"))
                    using (var resultsCS = searcherCS.Get())
                    {
                        foreach (var item in resultsCS)
                        {
                            if (item["NumberOfLogicalProcessors"] != null)
                            {
                                int csThreads = Convert.ToInt32(item["NumberOfLogicalProcessors"]);
                                if (csThreads > threadCount)
                                {
                                    threadCount = csThreads;
                                }
                            }
                            item.Dispose();
                        }
                    }
                }

                if (threadCount == 0)
                {
                    threadCount = Environment.ProcessorCount;
                }

                Dispatcher.Invoke(() =>
                {
                    cpue.Text = cpuName;
                    summaryCpuText.Text = cpuName;
                    cpucore.Text = coreCount.ToString();
                    threads.Text = threadCount.ToString();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    cpue.Text = UnknownStr;
                    cpucore.Text = UnknownStr;
                    threads.Text = UnknownStr;
                });
            }
        }

        private void ShowCpuExtraInfo()
        {
            try
            {
                int maxClockSpeed = 0;
                int l3Cache = 0;

                using (var searcher = new ManagementObjectSearcher("select MaxClockSpeed, L3CacheSize from Win32_Processor"))
                {
                    foreach (var item in searcher.Get())
                    {
                        maxClockSpeed = Convert.ToInt32(item["MaxClockSpeed"]);
                        l3Cache += Convert.ToInt32(item["L3CacheSize"]);
                    }
                }

                double l3MB = Math.Round(l3Cache / 1024.0, 1);
                double maxGHz = Math.Round(maxClockSpeed / 1000.0, 2);

                Dispatcher.Invoke(() =>
                {
                    corespeed.Text = $"{maxGHz} GHz";
                    l3cash.Text = $"{l3MB} MB";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    corespeed.Text = $"{ex.Message}";
                    l3cash.Text = UnknownStr;
                });
            }
        }
        private void ShowRamInfo()
        {
            try
            {
                ulong totalBytes = 0;
                int memoryTypeCode = 0;
                int averageSpeed = 0;
                int stickCount = 0;

                using (var searcher = new ManagementObjectSearcher("SELECT Capacity, MemoryType, SMBIOSMemoryType, Speed FROM Win32_PhysicalMemory"))
                {
                    using (var results = searcher.Get())
                    {
                        foreach (ManagementObject item in results)
                        {
                            try
                            {
                                if (item["Capacity"] != null)
                                    totalBytes += (ulong)item["Capacity"];

                                int smbios = item["SMBIOSMemoryType"] != null ? Convert.ToInt32(item["SMBIOSMemoryType"]) : 0;
                                int legacy = item["MemoryType"] != null ? Convert.ToInt32(item["MemoryType"]) : 0;

                                if (item["Speed"] != null)
                                {
                                    averageSpeed += Convert.ToInt32(item["Speed"]);
                                    stickCount++;
                                }

                                int detectedType = (smbios > 2) ? smbios : (legacy > 2 ? legacy : 0);
                                if (memoryTypeCode == 0 && detectedType != 0)
                                    memoryTypeCode = detectedType;
                            }
                            finally
                            {
                                item.Dispose();
                            }
                        }
                    }
                }

                if (totalBytes == 0)
                {
                    return;
                }

                double totalGB = totalBytes / (1024.0 * 1024 * 1024);
                string memoryType = UnknownStr;

                if (memoryTypeCode > 0)
                {
                    memoryType = memoryTypeCode switch
                    {
                        20 => "DDR",
                        21 => "DDR2",
                        22 => "DDR2 FB-DIMM",
                        24 => "DDR3",
                        26 => "DDR4",
                        27 => "LPDDR",
                        28 => "LPDDR2",
                        29 => "LPDDR3",
                        30 => "LPDDR4",
                        32 => "HBM",
                        33 => "HBM2",
                        34 => "DDR5",
                        35 => "LPDDR5",
                        36 => "HBM3",
                        _ => UnknownStr
                    };
                }

                if (memoryType == UnknownStr && stickCount > 0)
                {
                    int speed = averageSpeed / stickCount;

                    if (speed >= 4800) memoryType = "DDR5";
                    else if (speed >= 2133) memoryType = "DDR4";
                    else if (speed >= 800) memoryType = "DDR3";
                    else if (speed >= 400) memoryType = "DDR2";
                    else if (speed > 0) memoryType = "DDR";
                }

                Dispatcher.Invoke(() =>
                {
                    summaryRamText.Text = $"{Math.Round(totalGB)} GB / {memoryType}";
                });
            }
            catch
            {
            }
        }

        private void ShowMotherboardInfo()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Product, Manufacturer FROM Win32_BaseBoard"))
                {
                    foreach (var item in searcher.Get())
                    {
                        string product = item["Product"]?.ToString() ?? "";
                        string manufacturer = item["Manufacturer"]?.ToString() ?? "";

                        string fullName = $"{manufacturer} {product}";
                        Dispatcher.Invoke(() => mbname.Text = fullName);
                    }
                }

                using (var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                {
                    foreach (var item in searcher.Get())
                    {
                        string biosVersion = item["SMBIOSBIOSVersion"]?.ToString() ?? "";

                        string biosDateRaw = item["ReleaseDate"]?.ToString() ?? "";
                        string biosDateFormatted = "";
                        if (!string.IsNullOrEmpty(biosDateRaw) && biosDateRaw.Length >= 8)
                        {
                            string year = biosDateRaw.Substring(0, 4);
                            string month = biosDateRaw.Substring(4, 2);
                            string day = biosDateRaw.Substring(6, 2);
                            biosDateFormatted = $"{day}.{month}.{year}";
                        }

                        Dispatcher.Invoke(() =>
                        {
                            biosver.Text = biosVersion;
                            biosdate.Text = biosDateFormatted;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    mbname.Text = $"{ex.Message}";
                    biosver.Text = UnknownStr;
                    biosdate.Text = UnknownStr;
                });
            }
        }

        private async Task LoadStorageListAsync()
        {
            try
            {
                _storageDevices = await Task.Run(() => StorageHelper.GetAllStorageDevicesFast(UnknownStr)
                    .OrderByDescending(d => d.CapacityBytes)
                    .ToList());

                ssdComboBox.Items.Clear();
                if (_storageDevices.Count == 0)
                {
                    ssdnValue.Text = UnknownStr;
                    ssdcValue.Text = UnknownStr;
                    ssdfreeValue.Text = UnknownStr;
                    ssdComboBoxCard.Visibility = Visibility.Collapsed;
                    summaryStorageText.Text = UnknownStr;
                    return;
                }

                ulong totalStorageBytes = 0;
                foreach (var d in _storageDevices)
                {
                    totalStorageBytes += d.CapacityBytes;
                }

                double totalGB = totalStorageBytes / (1024.0 * 1024 * 1024);
                string totalFormatted = totalGB > 1000 ? $"{Math.Round(totalGB / 1024.0, 2)} TB" : $"{Math.Round(totalGB)} GB";

                string freeLabel = _pci["main"]["freespace"];
                summaryStorageProgress.Visibility = Visibility.Visible;

                summaryStorageText.Text = $"{totalFormatted}";

                if (_storageDevices.Count <= 1)
                    ssdComboBoxCard.Visibility = Visibility.Collapsed;
                else
                    ssdComboBoxCard.Visibility = Visibility.Visible;

                for (int i = 0; i < _storageDevices.Count; i++)
                {
                    string displayName = !string.IsNullOrWhiteSpace(_storageDevices[i].Name)
                        ? _storageDevices[i].Name
                        : $"Drive #{i + 1}";
                    ssdComboBox.Items.Add($"{i + 1}. {displayName}");
                }

                ssdComboBox.SelectedIndex = 0;
                UpdateStorageInfo(0);
            }
            catch (Exception ex)
            {
                ssdnValue.Text = UnknownStr;
                ssdcValue.Text = UnknownStr;
                ssdComboBoxCard.Visibility = Visibility.Collapsed;
            }
        }
        private async Task UpdateFreeSpaceInBackgroundAsync()
        {
            await Task.Run(() =>
            {
                foreach (var drive in _storageDevices)
                {
                    StorageHelper.CalculateFreeSpace(drive);
                }
            });

            if (_storageDevices.Count > 0)
            {
                ulong totalStorageBytes = 0;
                ulong totalFreeBytes = 0;
                foreach (var d in _storageDevices)
                {
                    totalStorageBytes += d.CapacityBytes;
                    totalFreeBytes += d.FreeSpaceBytes;
                }

                double totalGB = totalStorageBytes / (1024.0 * 1024 * 1024);
                double freeGB = totalFreeBytes / (1024.0 * 1024 * 1024);

                string totalFormatted = totalGB > 1000 ? $"{Math.Round(totalGB / 1024.0, 2)} TB" : $"{Math.Round(totalGB)} GB";
                string freeFormatted = freeGB > 1000 ? $"{Math.Round(freeGB / 1024.0, 2)} TB" : $"{Math.Round(freeGB)} GB";
                string freeLabel = _pci["main"]["freespace"]?.ToString() ?? "Свободно:";
                summaryStorageProgress.Visibility = Visibility.Collapsed;
                summaryStorageText.Text = $"{totalFormatted} ({freeLabel} {freeFormatted})";
                if (ssdComboBox.SelectedIndex >= 0)
                {
                    UpdateStorageInfo(ssdComboBox.SelectedIndex);
                }
            }
        }

        private void SSDComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ssdComboBox.SelectedIndex >= 0)
            {
                UpdateStorageInfo(ssdComboBox.SelectedIndex);
            }
        }

        private void UpdateStorageInfo(int index)
        {
            if (index < 0 || index >= _storageDevices.Count) return;

            var storage = _storageDevices[index];
            ssdnValue.Text = storage.Name;
            ssdcValue.Text = storage.CapacityFormatted;
            ssdtypeValue.Text = storage.DiskType;
            if (storage.IsFreeSpaceCalculated)
            {
                ssdFreeProgress.Visibility = Visibility.Collapsed;
                ssdfreeValue.Text = storage.FreeSpaceFormatted;
            }
            else
            {
                ssdFreeProgress.Visibility = Visibility.Visible;
                ssdfreeValue.Text = "";
            }
        }

        private async Task LoadGpuListAsync()
        {
            try
            {
                string unknown = UnknownStr;
                _gpus = await Task.Run(() => GpuHelper.GetAllGpus(unknown)
                .OrderByDescending(g => g.VRamBytes)
                    .ToList());

                videoComboBox.Items.Clear();
                if (_gpus.Count == 0)
                {
                    videon.Text = UnknownStr;
                    vram.Text = UnknownStr;
                    videoComboBoxCard.Visibility = Visibility.Collapsed;
                    return;
                }

                if (_gpus.Count <= 1)
                    videoComboBoxCard.Visibility = Visibility.Collapsed;
                else
                    videoComboBoxCard.Visibility = Visibility.Visible;

                for (int i = 0; i < _gpus.Count; i++)
                {
                    string displayName = !string.IsNullOrWhiteSpace(_gpus[i].Name)
                        ? _gpus[i].Name
                        : $"GPU #{i + 1}";
                    videoComboBox.Items.Add($"{i + 1}. {displayName}");
                }

                int maxIndex = _gpus
                    .Select((gpu, index) => new { gpu, index })
                    .OrderByDescending(x => x.gpu.VRamBytes)
                    .First().index;

                videoComboBox.SelectedIndex = maxIndex;
                summaryGpuText.Text = _gpus[maxIndex].Name;

                ulong totalVram = (ulong)_gpus.Sum(g => (long)g.VRamBytes);
                summaryVramText.Text = GpuInfo.FormatBytes(totalVram);
                UpdateGpuInfo(maxIndex);
            }
            catch (Exception ex)
            {
                videon.Text = UnknownStr;
                vram.Text = UnknownStr;
                videoComboBoxCard.Visibility = Visibility.Collapsed;
            }
        }

        private void VideoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (videoComboBox.SelectedIndex >= 0)
            {
                UpdateGpuInfo(videoComboBox.SelectedIndex);
            }
        }

        private void UpdateGpuInfo(int index)
        {
            if (index < 0 || index >= _gpus.Count) return;

            var gpu = _gpus[index];
            videon.Text = gpu.Name;
            vram.Text = gpu.VRamFormatted;
            gpudriver.Text = gpu.DriverVersion;
            gpudriverdate.Text = gpu.DriverDate;
        }

        private void LookResults_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://adderly.top/makubench",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show($"Default Browser Error.", "MakuTweaker", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowComputerInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer, Model FROM Win32_ComputerSystem");

                var item = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

                string manufacturer = item?["Manufacturer"]?.ToString();
                string model = item?["Model"]?.ToString();

                string[] invalidModels = { "System Product Name", "To Be Filled By O.E.M." };

                bool invalid = string.IsNullOrWhiteSpace(manufacturer) ||
                               string.IsNullOrWhiteSpace(model) ||
                               manufacturer.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                               model.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                               invalidModels.Any(x => model.Equals(x, StringComparison.OrdinalIgnoreCase));

                Dispatcher.Invoke(() =>
                {
                    if (!invalid)
                    {
                        bmodel.Header = _pci["main"]["modeln"];
                        pcModel.Text = $"{manufacturer} {model}";
                    }
                    else
                    {
                        bmodel.Header = _pci["main"]["mblabel"];
                        pcModel.Text = mbname.Text;
                    }
                });
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    bmodel.Header = _pci["main"]["mblabel"];
                    pcModel.Text = mbname.Text;
                });
            }
        }

        private void ShowSecurityInfo()
        {
            ShowTpm();
        }

        private void ShowTpm()
        {
            bool tpmFoundAndEnabled = false;

            try
            {
                var scope = new ManagementScope(
                    @"\\.\root\cimv2\security\microsofttpm");
                scope.Connect();

                using var searcher = new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery("SELECT IsEnabled_InitialValue FROM Win32_Tpm"));

                foreach (var item in searcher.Get())
                {
                    tpmFoundAndEnabled = Convert.ToBoolean(item["IsEnabled_InitialValue"] ?? false);
                    break;
                }
            }
            catch
            {
            }
            ShowTpmStatus(tpmFoundAndEnabled);
        }

        private void LoadRamSticks()
        {
            try
            {
                var tempSticks = new List<RamStickInfo>();

                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Capacity, Speed, PartNumber FROM Win32_PhysicalMemory"))
                using (var results = searcher.Get())
                {
                    foreach (var item in results)
                    {
                        tempSticks.Add(new RamStickInfo
                        {
                            Manufacturer = item["Manufacturer"]?.ToString()?.Trim() ?? UnknownStr,
                            CapacityBytes = Convert.ToUInt64(item["Capacity"] ?? 0),
                            Speed = Convert.ToInt32(item["Speed"] ?? 0),
                            PartNumber = item["PartNumber"]?.ToString()?.Trim() ?? UnknownStr
                        });
                        item.Dispose();
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    _ramSticks = tempSticks;
                    ramStickComboBox.Items.Clear();

                    if (_ramSticks.Count <= 1)
                        ramStickComboBoxCard.Visibility = Visibility.Collapsed;
                    else
                        ramStickComboBoxCard.Visibility = Visibility.Visible;

                    int i = 1;
                    foreach (var stick in _ramSticks)
                    {
                        ramStickComboBox.Items.Add($"{i}. {stick.CapacityFormatted} — {stick.Manufacturer}");
                        i++;
                    }

                    if (_ramSticks.Count > 0)
                    {
                        ramStickComboBox.SelectedIndex = 0;
                        UpdateRamStickInfo(0);
                    }
                    else
                    {
                        ramStickComboBoxCard.Visibility = Visibility.Collapsed;
                    }
                });
            }
            catch
            {
                Dispatcher.Invoke(() => ramStickManufacturer.Text = "Error");
                ramStickComboBoxCard.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateRamStickInfo(int index)
        {
            if (index < 0 || index >= _ramSticks.Count) return;

            var stick = _ramSticks[index];

            ramStickManufacturer.Text = stick.Manufacturer;
            ramStickCapacity.Text = stick.CapacityFormatted;
            ramStickPart.Text = stick.PartNumber;
            ramStickSpeed.Text = stick.Speed > 0 ? $"{stick.Speed} Mhz" : UnknownStr;
        }

        private void SaveDataToTxt()
        {
            try
            {
                var pci = MainWindow.Localization.LoadLocalization(
                    Properties.Settings.Default.lang ?? "en", "pci");

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "TXT File| *.txt",
                    Title = "MakuTweaker",
                    FileName = "MakuTweaker System Info.txt"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("MakuTweaker 5.8.5 // MarkAdderly");
                    sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.AppendLine();

                    sb.AppendLine($"{bmodel.Header}: {pcModel.Text}");
                    sb.AppendLine($"{pci["main"]["allstorage"]}: {summaryStorageText.Text}");
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["processorlabel"]} ===");
                    sb.AppendLine($"{pci["main"]["processorname"]} {cpue.Text}");
                    sb.AppendLine($"{pci["main"]["processorcores"]} {cpucore.Text} / {threads.Text}");
                    sb.AppendLine($"{pci["main"]["processorfreq"]} {corespeed.Text}");
                    sb.AppendLine($"{pci["main"]["processorcache"]} {l3cash.Text}");
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["ramlabel"]} ===");
                    sb.AppendLine($"{pci["main"]["ramtotal"]} {summaryRamText.Text}");
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["mblabel"]} ===");
                    sb.AppendLine($"{pci["main"]["mbname"]} {mbname.Text}");
                    sb.AppendLine($"{pci["main"]["mbver"]} {biosver.Text}");
                    sb.AppendLine($"{pci["main"]["mbdate"]} {biosdate.Text}");
                    sb.AppendLine($"{pci["main"]["tpmtitle"]} {tpmStatus.Text}");
                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["ramsticktitle"]} ===");

                    if (_ramSticks.Count == 0)
                    {
                        sb.AppendLine(UnknownStr);
                    }
                    else
                    {
                        for (int i = 0; i < _ramSticks.Count; i++)
                        {
                            var stick = _ramSticks[i];

                            sb.AppendLine($"[{i + 1}]");
                            sb.AppendLine($"{pci["main"]["manu"]} {stick.Manufacturer}");
                            sb.AppendLine($"{pci["main"]["capac"]} {stick.CapacityFormatted}");
                            sb.AppendLine($"{pci["main"]["partnum"]} {stick.PartNumber}");
                            sb.AppendLine($"{pci["main"]["ramstickspeed"]} {(stick.Speed > 0 ? $"{stick.Speed} Mhz" : UnknownStr)}");
                            sb.AppendLine();
                        }
                    }

                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["vlabel"]} ===");

                    if (_gpus.Count == 0)
                    {
                         sb.AppendLine(UnknownStr);
                    }
                    else
                    {
                        for (int i = 0; i < _gpus.Count; i++)
                        {
                            var gpu = _gpus[i];

                            sb.AppendLine($"[{i + 1}] {gpu.Name}");
                            sb.AppendLine($"{pci["main"]["vmem"]} {gpu.VRamFormatted}");
                            sb.AppendLine($"{pci["main"]["driverversion"]} {gpu.DriverVersion}");
                            sb.AppendLine($"{pci["main"]["driverdate"]} {gpu.DriverDate}");
                            sb.AppendLine();
                        }
                    }

                    sb.AppendLine();

                    sb.AppendLine($"=== {pci["main"]["ssdl"]} ===");

                    if (_storageDevices.Count == 0)
                    {
                        sb.AppendLine(UnknownStr);
                    }
                    else
                    {
                        for (int i = 0; i < _storageDevices.Count; i++)
                        {
                            var storage = _storageDevices[i];

                            sb.AppendLine($"[{i + 1}] {storage.Name}");
                            sb.AppendLine($"{pci["main"]["smem"]} {storage.CapacityFormatted}");
                            sb.AppendLine($"{pci["main"]["disktype"]} {storage.DiskType}");
                            sb.AppendLine();
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine($"=== {pci["main"]["displtitle"]} ===");
                    sb.AppendLine($"{pci["main"]["resolution"]} {resolution.Text}");
                    sb.AppendLine($"{pci["main"]["refreshrate"]} {refreshrate.Text}");
                    sb.AppendLine($"{pci["main"]["monitormodel"]} {monitormodel.Text}");
                    sb.AppendLine();


                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);

                    iNKORE.UI.WPF.Modern.Controls.MessageBox.Show("System information saved successfully!\nСистемная информация была успешно сохранена!", "MakuTweaker", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show($"{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ramStickComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ramStickComboBox.SelectedIndex >= 0)
                UpdateRamStickInfo(ramStickComboBox.SelectedIndex);
        }

        string WrapAfterWords(string text, int wordsPerLine = 5)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var words = text.Split(' ');
            var lines = new List<string>();

            for (int i = 0; i < words.Length; i += wordsPerLine)
            {
                lines.Add(string.Join(" ", words.Skip(i).Take(wordsPerLine)));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void saveBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveDataToTxt();
        }

        private async void copyBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var primaryGpu = _gpus.FirstOrDefault();
                string gpuName = primaryGpu != null ? primaryGpu.Name : UnknownStr;
                string gpuVram = primaryGpu != null ? primaryGpu.VRamFormatted : UnknownStr;
                StringBuilder sb = new StringBuilder();

                sb.AppendLine($"{_pci["main"]["processorlabel"]}: {cpue.Text} ({cpucore.Text} / {threads.Text})");
                sb.AppendLine($"{_pci["main"]["ramlabel"]}: {summaryRamText.Text}");
                sb.AppendLine($"{_pci["main"]["vlabel"]}: {gpuName} ({gpuVram})");
                sb.AppendLine($"{bmodel.Header}: {pcModel.Text}");

                Clipboard.SetText(sb.ToString());
                await TriggerCheckmarkAsync(copyBtn, copyIcon);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async Task TriggerCheckmarkAsync(System.Windows.Controls.Button button, iNKORE.UI.WPF.Modern.Controls.SymbolIcon icon)
        {
            var originalSymbol = icon.Symbol;
            icon.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Accept;
            button.IsEnabled = false;
            await Task.Delay(5000);
            icon.Symbol = originalSymbol;
            button.IsEnabled = true;
        }

        private async Task<double> MeasureDiskSpeedAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    string tempFile = Path.Combine(Path.GetTempPath(), "MakuTweaker_Disk_Bench.tmp");
                    int bufferSize = 1024 * 1024 * 32;
                    int chunks = 8;
                    byte[] data = new byte[bufferSize];
                    new Random().NextBytes(data);

                    Stopwatch sw = new Stopwatch();
                    const FileOptions NoBuffering = (FileOptions)0x20000000;

                    using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough | NoBuffering))
                    {
                        for (int i = 0; i < chunks; i++)
                            fs.Write(data, 0, data.Length);
                    }

                    sw.Start();
                    using (FileStream fs = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan | NoBuffering))
                    {
                        int bytesRead;
                        while ((bytesRead = fs.Read(data, 0, data.Length)) > 0) { }
                    }
                    sw.Stop();

                    File.Delete(tempFile);
                    double readTime = sw.Elapsed.TotalSeconds;
                    double readSpeedMBps = 256.0 / readTime;
                    return readSpeedMBps;
                }
                catch
                {
                    return 0.0;
                }
            });
        }

        private async void RateBtn_Click(object sender, RoutedEventArgs e)
        {
            double ramScore = 0;
            string ramDetails = "";
            try
            {
                string[] ramParts = summaryRamText.Text.Split(new[] { " / " }, StringSplitOptions.None);
                double ramGb = double.Parse(ramParts[0].Replace(" GB", "").Trim());
                string ramType = ramParts.Length > 1 ? ramParts[1].Trim() : "DDR4";

                ramDetails = $"{ramGb} GB / {ramType}";

                double scoreType = ramType.Contains("DDR5") ? 5.0 :
                                   ramType.Contains("DDR4") ? 4.0 :
                                   ramType.Contains("DDR3") ? 3.0 :
                                   ramType.Contains("DDR2") ? 2.0 : 1.0;

                double scoreCap = Math.Clamp(ramGb * (5.0 / 32.0), 0, 5.0);
                ramScore = scoreType + scoreCap;
            }
            catch { ramScore = 0.0; }

            rateRamScore.Text = $"{ramScore:F1} / 10";
            rateRamDetails.Text = ramDetails;

            double gpuScore = 0;
            string gpuDetails = "";
            try
            {
                ulong totalVramBytes = (ulong)_gpus.Sum(g => (long)g.VRamBytes);
                double vramGb = totalVramBytes / (1024.0 * 1024 * 1024);
                gpuScore = Math.Clamp(vramGb * (10.0 / 16.0), 0, 10.0);

                string gpuName = _gpus.OrderByDescending(g => g.VRamBytes).FirstOrDefault()?.Name ?? UnknownStr;
                gpuDetails = $"{gpuName} / {summaryVramText.Text}";
            }
            catch { gpuScore = 0.0; }

            rateGpuScore.Text = $"{gpuScore:F1} / 10";
            rateGpuDetails.Text = gpuDetails;

            rateStorageScore.Visibility = Visibility.Collapsed;
            rateStorageRing.Visibility = Visibility.Visible;
            rateStorageDetails.Text = "";

            rateCpuScore.Visibility = Visibility.Collapsed;
            rateCpuRing.Visibility = Visibility.Visible;

            rateTotalText.Text = "";
            rateTotalRing.Visibility = Visibility.Visible;

            var mainTrans = (TranslateTransform)MainScrollViewer.RenderTransform;
            var ratingTrans = (TranslateTransform)RatingScrollViewer.RenderTransform;
            RatingScrollViewer.Visibility = Visibility.Visible;
            TimeSpan duration = TimeSpan.FromMilliseconds(450);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            DoubleAnimation mainSlide = new DoubleAnimation(0, -150, duration) { EasingFunction = ease };
            DoubleAnimation mainFade = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
            mainFade.Completed += (s, ev) => MainScrollViewer.Visibility = Visibility.Collapsed;
            MainScrollViewer.BeginAnimation(UIElement.OpacityProperty, mainFade);
            mainTrans.BeginAnimation(TranslateTransform.YProperty, mainSlide);

            DoubleAnimation ratingSlide = new DoubleAnimation(150, 0, duration) { EasingFunction = ease };
            DoubleAnimation ratingFade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
            RatingScrollViewer.BeginAnimation(UIElement.OpacityProperty, ratingFade);
            ratingTrans.BeginAnimation(TranslateTransform.YProperty, ratingSlide);

            if (_lastSingleScore == 0)
            {
                if (_startupBenchTask == null || _startupBenchTask.IsCompleted)
                    _startupBenchTask = RunRatingSingleBenchmarkAsync();
            }

            Task<double> diskBenchTask = MeasureDiskSpeedAsync();

            async Task<double> UpdateStorageAsync()
            {
                double diskSpeedMBps = await diskBenchTask;
                double storageScore = 0;
                string storageDetails = "";
                try
                {
                    double speedScore = Math.Clamp(diskSpeedMBps * (8.0 / 3000.0), 0, 8.0);
                    double typeScore = 0.0;
                    string systemDriveType = "HDD";

                    await Task.Run(() =>
                    {
                        try
                        {
                            string sysDrive = System.IO.Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
                            string sysDiskDeviceId = "";
                            using (var partSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{sysDrive}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                            {
                                foreach (ManagementObject part in partSearcher.Get())
                                {
                                    using (var driveSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                                    {
                                        foreach (ManagementObject drive in driveSearcher.Get()) sysDiskDeviceId = drive["Index"]?.ToString();
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(sysDiskDeviceId))
                            {
                                using (var typeSearcher = new ManagementObjectSearcher($@"root\Microsoft\Windows\Storage", $"SELECT MediaType, BusType FROM MSFT_PhysicalDisk WHERE DeviceId='{sysDiskDeviceId}'"))
                                {
                                    foreach (ManagementObject obj in typeSearcher.Get())
                                    {
                                        int busType = Convert.ToInt32(obj["BusType"]);
                                        int mediaType = Convert.ToInt32(obj["MediaType"]);

                                        if (busType == 17) { typeScore = 2.0; systemDriveType = "NVMe SSD"; }
                                        else if (mediaType == 4) { typeScore = 1.0; systemDriveType = "SSD"; }
                                    }
                                }
                            }
                        }
                        catch { typeScore = 1.0; systemDriveType = ""; }
                    });

                    storageScore = speedScore + typeScore;
                    string systemDiskText = _pci["main"]["systemdisk"];
                    storageDetails = $"{systemDiskText} {systemDriveType}, {Math.Round(diskSpeedMBps)} MB/s";
                }
                catch { storageScore = 0.0; }

                rateStorageScore.Text = $"{storageScore:F1} / 10";
                rateStorageDetails.Text = storageDetails;
                rateStorageRing.Visibility = Visibility.Collapsed;
                rateStorageScore.Visibility = Visibility.Visible;
                return storageScore;
            }

            async Task<double> UpdateCpuAsync()
            {
                if (_startupBenchTask != null) await _startupBenchTask;

                double cpuScore = 0;
                string cpuName = "";
                try
                {
                    int cores = int.TryParse(cpucore.Text, out int c) ? c : 2;
                    int th = int.TryParse(threads.Text, out int t) ? t : 2;
                    double l3 = 0;
                    double.TryParse(l3cash.Text.Replace(" MB", "").Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out l3);

                    double scoreCores = Math.Clamp(cores * (1.0 / 16.0), 0, 1.0);
                    double scoreThreads = Math.Clamp(th * (1.0 / 32.0), 0, 1.0);
                    double scoreL3 = Math.Clamp(l3 * (1.0 / 16.0), 0, 1.0);
                    double maxBenchScore = 1500.0;
                    double scoreBench = Math.Clamp(_lastSingleScore * (7.0 / maxBenchScore), 0, 7.0);

                    cpuScore = scoreCores + scoreThreads + scoreL3 + scoreBench;
                    cpuName = cpue.Text;
                }
                catch { cpuScore = 0.0; }

                rateCpuScore.Text = $"{cpuScore:F1} / 10";
                rateCpuDetails.Text = cpuName;
                rateCpuRing.Visibility = Visibility.Collapsed;
                rateCpuScore.Visibility = Visibility.Visible;

                return cpuScore;
            }

            var storageTask = UpdateStorageAsync();
            var cpuTask = UpdateCpuAsync();
            await Task.WhenAll(storageTask, cpuTask);

            rateTotalRing.Visibility = Visibility.Collapsed;
            double totalScore = (cpuTask.Result + ramScore + gpuScore + storageTask.Result) / 4.0;
            rateTotalText.Text = $"{totalScore:F1} / 10";
        }

        private void BackToMain_Click(object sender, RoutedEventArgs e)
        {
            var mainTrans = (TranslateTransform)MainScrollViewer.RenderTransform;
            var ratingTrans = (TranslateTransform)RatingScrollViewer.RenderTransform;

            MainScrollViewer.Visibility = Visibility.Visible;

            TimeSpan duration = TimeSpan.FromMilliseconds(450);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            DoubleAnimation ratingSlide = new DoubleAnimation(0, 150, duration) { EasingFunction = ease };
            DoubleAnimation ratingFade = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
            ratingFade.Completed += (s, ev) => RatingScrollViewer.Visibility = Visibility.Collapsed;

            RatingScrollViewer.BeginAnimation(UIElement.OpacityProperty, ratingFade);
            ratingTrans.BeginAnimation(TranslateTransform.YProperty, ratingSlide);

            DoubleAnimation mainSlide = new DoubleAnimation(-150, 0, duration) { EasingFunction = ease };
            DoubleAnimation mainFade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };

            MainScrollViewer.BeginAnimation(UIElement.OpacityProperty, mainFade);
            mainTrans.BeginAnimation(TranslateTransform.YProperty, mainSlide);
        }

        private string GetDriveLetterForPhysicalDisk(string deviceId)
        {
            try
            {
                string diskIndex = new string(deviceId.Where(char.IsDigit).ToArray());

                if (string.IsNullOrEmpty(diskIndex)) return null;

                using var searcher = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string antecedent = obj["Antecedent"]?.ToString();
                    string dependent = obj["Dependent"]?.ToString();

                    if (antecedent != null && dependent != null && antecedent.Contains($"Disk #{diskIndex},"))
                    {
                        int start = dependent.IndexOf('"') + 1;
                        int end = dependent.LastIndexOf('"');
                        if (start > 0 && end > start)
                        {
                            return dependent.Substring(start, end - start) + "\\";
                        }
                    }
                }
            }
            catch { }
            return null;
        }


        private async Task<(double write, double read)> MeasureSpecificDiskSpeedAsync(string drivePath, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string sysDrive = Path.GetPathRoot(Environment.SystemDirectory);
                    string tempFile = drivePath.Equals(sysDrive, StringComparison.OrdinalIgnoreCase)
                        ? Path.Combine(Path.GetTempPath(), "MakuTweaker_Advanced_Bench.tmp")
                        : Path.Combine(drivePath, "MakuTweaker_Advanced_Bench.tmp");

                    int bufferSize = 1024 * 1024 * 32;
                    int chunks = 32;
                    byte[] data = new byte[bufferSize];
                    new Random().NextBytes(data);

                    Stopwatch sw = new Stopwatch();
                    const FileOptions NoBuffering = (FileOptions)0x20000000;

                    sw.Start();
                    using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough | NoBuffering))
                    {
                        for (int i = 0; i < chunks; i++)
                        {
                            if (token.IsCancellationRequested) break;
                            fs.Write(data, 0, data.Length);
                        }
                    }
                    sw.Stop();

                    if (token.IsCancellationRequested)
                    {
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                        return (0.0, 0.0);
                    }

                    double writeTime = sw.Elapsed.TotalSeconds;
                    double writeSpeedMBps = 1024.0 / writeTime;

                    sw.Restart();
                    using (FileStream fs = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan | NoBuffering))
                    {
                        int bytesRead;
                        while ((bytesRead = fs.Read(data, 0, data.Length)) > 0)
                        {
                            if (token.IsCancellationRequested) break;
                        }
                    }
                    sw.Stop();
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                    if (token.IsCancellationRequested) return (0.0, 0.0);

                    double readTime = sw.Elapsed.TotalSeconds;
                    double readSpeedMBps = 1024.0 / readTime;
                    return (writeSpeedMBps, readSpeedMBps);
                }
                catch
                {
                    return (0.0, 0.0);
                }
            });
        }

        private async void TestDiskBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ssdComboBox.SelectedIndex < 0 || ssdComboBox.SelectedIndex >= _storageDevices.Count) return;

            var selectedDisk = _storageDevices[ssdComboBox.SelectedIndex];
            string driveLetter = GetDriveLetterForPhysicalDisk(selectedDisk.DeviceID);

            if (string.IsNullOrEmpty(driveLetter))
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show("Could not identify a logical partition for testing. The disk is unpartitioned or has no drive letter.", "MakuTweaker", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (mw.NavigationView_Root.MenuItems != null)
            {
                foreach (var item in mw.NavigationView_Root.MenuItems)
                {
                    if (item is UIElement ui) ui.IsEnabled = false;
                }
            }
            if (mw.NavigationView_Root.FooterMenuItems != null)
            {
                foreach (var item in mw.NavigationView_Root.FooterMenuItems)
                {
                    if (item is UIElement ui) ui.IsEnabled = false;
                }
            }

            var fadeOutAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(250),
                FillBehavior = FillBehavior.HoldEnd
            };
            MainScrollViewer.BeginAnimation(UIElement.OpacityProperty, fadeOutAnim);
            await Task.Delay(250);

            MainScrollViewer.Visibility = Visibility.Collapsed;

            loadingText.Text = _pci?["main"]?["testdiskloading"];
            stopTestBtn.Content = _pci?["main"]?["testdiskstop"];

            stopTestBtn.IsEnabled = true;
            stopTestBtn.Visibility = Visibility.Visible;

            loadingPanel.Opacity = 0;
            loadingPanel.Visibility = Visibility.Visible;
            ring.IsActive = true;
            FadeIn(loadingPanel, 300);

            _diskBenchCts = new CancellationTokenSource();
            var (writeSpeed, readSpeed) = await MeasureSpecificDiskSpeedAsync(driveLetter, _diskBenchCts.Token);

            MainScrollViewer.Visibility = Visibility.Visible;
            await HideProgressSmoothAsync();
            stopTestBtn.Visibility = Visibility.Collapsed;

            if (mw.NavigationView_Root.MenuItems != null)
            {
                foreach (var item in mw.NavigationView_Root.MenuItems)
                {
                    if (item is UIElement ui) ui.IsEnabled = true;
                }
            }
            if (mw.NavigationView_Root.FooterMenuItems != null)
            {
                foreach (var item in mw.NavigationView_Root.FooterMenuItems)
                {
                    if (item is UIElement ui) ui.IsEnabled = true;
                }
            }

            if (_diskBenchCts == null || _diskBenchCts.IsCancellationRequested)
            {
                _diskBenchCts?.Dispose();
                _diskBenchCts = null;
                return;
            }

            _diskBenchCts.Dispose();
            _diskBenchCts = null;

            if (writeSpeed == 0 && readSpeed == 0)
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show("The test could not be run. There may not be enough free space on the disk (1 GB is required).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string readText = monitoringLang?["main"]?["readspeed"];
            string writeText = monitoringLang?["main"]?["writespeed"];

            StringBuilder resultText = new StringBuilder();
            resultText.AppendLine($"{selectedDisk.Name}");
            resultText.AppendLine();
            resultText.AppendLine($"{readText}: {Math.Round(readSpeed)} MB/s");
            resultText.AppendLine($"{writeText}: {Math.Round(writeSpeed)} MB/s");

            iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(resultText.ToString(), "MakuTweaker Benchmark", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void StopTestBtn_Click(object sender, RoutedEventArgs e)
        {
            _diskBenchCts?.Cancel();
            stopTestBtn.IsEnabled = false;
        }

        private void LoadDisplayInfo()
        {
            try
            {
                var tempDisplays = new List<DisplayInfo>();
                var wmiMonitors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string instance = obj["InstanceName"]?.ToString() ?? "";
                        var ufn = obj["UserFriendlyName"] as ushort[];
                        if (ufn != null && ufn.Length > 0)
                        {
                            var sb = new StringBuilder();
                            foreach (ushort c in ufn) if (c != 0) sb.Append((char)c);
                            string modelName = sb.ToString().Trim();
                            if (!string.IsNullOrWhiteSpace(modelName))
                            {
                                wmiMonitors[instance] = modelName;
                            }
                        }
                    }
                }
                catch { }

                DISPLAY_DEVICE adapter = new DISPLAY_DEVICE();
                adapter.cb = Marshal.SizeOf(adapter);

                for (uint id = 0; EnumDisplayDevices(null, id, ref adapter, 0); id++)
                {
                    if ((adapter.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                    {
                        bool isPrimary = (adapter.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;

                        string res = UnknownStr;
                        string hz = UnknownStr;
                        DEVMODE dm = new DEVMODE();
                        dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                        if (EnumDisplaySettings(adapter.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
                        {
                            res = $"{dm.dmPelsWidth} x {dm.dmPelsHeight}";
                            hz = $"{dm.dmDisplayFrequency} Hz";
                        }

                        string model = "";
                        DISPLAY_DEVICE monitor = new DISPLAY_DEVICE();
                        monitor.cb = Marshal.SizeOf(monitor);

                        if (EnumDisplayDevices(adapter.DeviceName, 0, ref monitor, EDD_GET_DEVICE_INTERFACE_NAME))
                        {
                            var parts = monitor.DeviceID.Split('#');
                            if (parts.Length >= 3)
                            {
                                string pnpPart1 = parts[1];
                                string pnpPart2 = parts[2];

                                var matched = wmiMonitors.FirstOrDefault(k => k.Key.Contains(pnpPart1) && k.Key.Contains(pnpPart2));
                                if (!string.IsNullOrWhiteSpace(matched.Value))
                                {
                                    model = matched.Value;
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(model))
                        {
                            model = _pci?["main"]?["dispnoname"]?.ToString() ?? UnknownStr;
                        }

                        tempDisplays.Add(new DisplayInfo
                        {
                            Model = model,
                            Resolution = res,
                            RefreshRate = hz,
                            IsPrimary = isPrimary
                        });
                    }
                }

                tempDisplays = tempDisplays.OrderByDescending(d => d.IsPrimary).ToList();

                Dispatcher.Invoke(() =>
                {
                    _displays = tempDisplays;
                    monitorComboBox.Items.Clear();

                    if (_displays.Count == 0)
                    {
                        resolution.Text = UnknownStr;
                        refreshrate.Text = UnknownStr;
                        monitormodel.Text = UnknownStr;
                        if (monitorComboBoxCard != null) monitorComboBoxCard.Visibility = Visibility.Collapsed;
                        return;
                    }

                    if (monitorComboBoxCard != null)
                    {
                        monitorComboBoxCard.Visibility = _displays.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                    }

                    for (int i = 0; i < _displays.Count; i++)
                    {

                        monitorComboBox.Items.Add($"{i + 1}. {_displays[i].Model}");
                    }

                    monitorComboBox.SelectedIndex = 0;
                    UpdateDisplayInfo(0);
                });
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    resolution.Text = UnknownStr;
                    refreshrate.Text = UnknownStr;
                    monitormodel.Text = UnknownStr;
                    if (monitorComboBoxCard != null) monitorComboBoxCard.Visibility = Visibility.Collapsed;
                });
            }
        }

        private void UpdateDisplayInfo(int index)
        {
            if (index < 0 || index >= _displays.Count) return;

            var disp = _displays[index];
            resolution.Text = disp.Resolution;
            refreshrate.Text = disp.RefreshRate;
            monitormodel.Text = disp.Model;
        }

        private void monitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (monitorComboBox.SelectedIndex >= 0)
            {
                UpdateDisplayInfo(monitorComboBox.SelectedIndex);
            }
        }
    }

    public class GpuInfo
    {
        public string Name { get; set; } = string.Empty;
        public ulong VRamBytes { get; set; }
        public string VRamFormatted => FormatBytes(VRamBytes);
        public string DriverVersion { get; set; } = "N/A";
        public string DriverDate { get; set; } = "N/A";
        public static string FormatBytes(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
    public static class GpuHelper
    {
        public static List<GpuInfo> GetAllGpus(string unknownStr)
        {
            var gpus = new List<GpuInfo>();

            try
            {
                using var factory = Vortice.DXGI.DXGI.CreateDXGIFactory1<Vortice.DXGI.IDXGIFactory1>();
                int i = 0;
                while (true)
                {
                    try
                    {
                        factory.EnumAdapters1((uint)i, out Vortice.DXGI.IDXGIAdapter1? adapter);
                        if (adapter == null)
                            break;

                        using (adapter)
                        {
                            var desc = adapter.Description1;
                            string name = desc.Description?.Trim() ?? "";

                            if (name.Contains("Microsoft Basic Render Driver", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("spacedesk", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Parsec", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Virtual Desktop", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Apollo", StringComparison.OrdinalIgnoreCase))
                            {
                                i++;
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(name) || name.Equals("Null", StringComparison.OrdinalIgnoreCase))
                            {
                                i++;
                                continue;
                            }

                            if ((desc.Flags & Vortice.DXGI.AdapterFlags.Software) != 0)
                            {
                                i++;
                                continue;
                            }

                            if (gpus.Any(g => g.Name == name && g.VRamBytes == desc.DedicatedVideoMemory))
                            {
                                i++;
                                continue;
                            }

                            var (drvVer, drvDate) = GetDriverInfoForAdapter(name, unknownStr);
                            gpus.Add(new GpuInfo
                            {
                                Name = name,
                                VRamBytes = desc.DedicatedVideoMemory,
                                DriverVersion = drvVer,
                                DriverDate = drvDate
                            });
                        }

                        i++;
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                return FallbackToWmi();
            }

            return gpus.Count > 0 ? gpus : FallbackToWmi();
        }

        public static (string version, string date) GetDriverInfoForAdapter(string adapterName, string unknownStr)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString()?.Trim() ?? "";
                    if (string.Equals(name, adapterName, StringComparison.OrdinalIgnoreCase))
                    {
                        string ver = obj["DriverVersion"]?.ToString()?.Trim() ?? unknownStr;
                        string dateRaw = obj["DriverDate"]?.ToString() ?? "";
                        string date = unknownStr;
                        if (!string.IsNullOrEmpty(dateRaw) && dateRaw.Length >= 8)
                        {
                            date = $"{dateRaw.Substring(6, 2)}.{dateRaw.Substring(4, 2)}.{dateRaw.Substring(0, 4)}";
                        }
                        return (ver, date);
                    }
                }
            }
            catch { }
            return (unknownStr, unknownStr);
        }


        private static List<GpuInfo> FallbackToWmi()
        {
            var gpus = new List<GpuInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? "";
                    ulong vram = obj["AdapterRAM"] != null ? Convert.ToUInt64(obj["AdapterRAM"]) : 0;

                    if (name.Contains("spacedesk", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Parsec", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Virtual Desktop", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!gpus.Any(g => g.Name == name && g.VRamBytes == vram))
                    {
                        gpus.Add(new GpuInfo { Name = name, VRamBytes = vram });
                    }
                }
            }
            catch { }
            return gpus;
        }
    }

    public class StorageInfo
    {
        public string DeviceID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ulong CapacityBytes { get; set; }
        public ulong FreeSpaceBytes { get; set; }
        public bool IsFreeSpaceCalculated { get; set; } = false;

        public string DiskType { get; set; } = "";
        public string CapacityFormatted => FormatBytes(CapacityBytes);
        public string FreeSpaceFormatted => IsFreeSpaceCalculated ? FormatBytes(FreeSpaceBytes) : "...";

        private static string FormatBytes(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            if (bytes == 0) return "0 B";
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class RamStickInfo
    {
        public string Manufacturer { get; set; } = "";
        public ulong CapacityBytes { get; set; }
        public int Speed { get; set; }
        public string PartNumber { get; set; } = "";
        public string SpeedFormatted => Speed > 0 ? $"{Speed} Mhz" : "N/A";

        public string CapacityFormatted =>
            $"{Math.Round(CapacityBytes / (1024.0 * 1024 * 1024), 1)} GB";
    }

    public static class StorageHelper
    {
        public static List<StorageInfo> GetAllStorageDevicesFast(string unknownStr)
        {
            var devices = new List<StorageInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT DeviceID, Caption, Size, MediaType FROM Win32_DiskDrive");
                foreach (ManagementObject drive in searcher.Get())
                {
                    string name = drive["Caption"]?.ToString() ?? unknownStr;
                    ulong size = drive["Size"] != null ? Convert.ToUInt64(drive["Size"]) : 0;
                    string deviceId = drive["DeviceID"]?.ToString() ?? "";
                    string mediaType = drive["MediaType"]?.ToString() ?? "";

                    if (size == 0 ||
                        name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("iSCSI", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    devices.Add(new StorageInfo
                    {
                        DeviceID = deviceId,
                        Name = name,
                        CapacityBytes = size,
                        FreeSpaceBytes = 0,
                        IsFreeSpaceCalculated = false,
                        DiskType = DetectDiskType(name, mediaType, deviceId)
                    });
                    drive.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            return devices;
        }
        private static string DetectDiskType(string name, string mediaType, string deviceId)
        {
            try
            {
                string idx = new string(deviceId.Where(char.IsDigit).ToArray());

                if (!string.IsNullOrEmpty(idx))
                {
                    using var ps = new ManagementObjectSearcher(
                        @"root\Microsoft\Windows\Storage",
                        $"SELECT BusType, MediaType FROM MSFT_PhysicalDisk WHERE DeviceId='{idx}'");

                    foreach (ManagementObject obj in ps.Get())
                    {
                        int busType = Convert.ToInt32(obj["BusType"]);
                        int mt = Convert.ToInt32(obj["MediaType"]);

                        // типы шин Windows: RAID = 8, USB = 7, NVMe = 17, SAS = 10, SCSI = 1, IDE = 2/3, SATA = 11, SD = 12/13, Virtual Disk = 14, Storage Spaces = 16
                        switch (busType)
                        {
                            case 8:  return "RAID";
                            case 17: return "NVMe SSD";
                            case 10: return "SAS";
                            case 1:  return "SCSI";

                            case 2:
                            case 3:
                                return mt == 4 ? "IDE SSD" : "IDE HDD";

                            case 11:
                                return mt == 4 ? "SATA SSD" : "SATA HDD";

                            case 7:  return "USB";

                            case 12:
                            case 13:
                                return "SD";

                            case 14: return "Virtual Disk";
                            case 16: return "Storage Spaces";
                        }

                        if (mt == 5) return "NVMe SSD";
                        if (mt == 4) return "SSD";
                        if (mt == 3) return "HDD";
                    }
                }
            }
            catch { }

            // fallback если система не вернула bustype
            string n = $"{name} {mediaType} {deviceId}".ToLowerInvariant();

            if (n.Contains("raid") ||
                n.Contains("adaptec") ||
                n.Contains("aacraid") ||
                n.Contains("megaraid") ||
                n.Contains("smart array") ||
                n.Contains("perc") ||
                n.Contains("lsi") ||
                n.Contains("broadcom") ||
                n.Contains("areca") ||
                n.Contains("3ware"))
                return "RAID";

            if (n.Contains("nvme")) return "NVMe SSD";
            if (n.Contains("sas")) return "SAS";
            if (n.Contains("scsi")) return "SCSI";
            if (n.Contains("ide") || n.Contains("atapi") || n.Contains(" ata")) return "IDE";
            if (n.Contains("sd card") || n.Contains("sdcard") || n.Contains("mmc")) return "SD";
            if (n.Contains("storage spaces") ||
                n.Contains("storage space") ||
                n.Contains("microsoft storage space"))
                return "Storage Spaces";

            if (n.Contains("usb")) return "USB";
            if (n.Contains("ssd") || n.Contains("solid")) return "SSD";

            if (n.Contains("hdd") ||
                n.Contains("hard disk") ||
                (mediaType?.Contains("Fixed hard disk", StringComparison.OrdinalIgnoreCase) ?? false))
                return "HDD";

            return "";
        }

        public static void CalculateFreeSpace(StorageInfo storage)
        {
            try
            {
                string query = $"SELECT * FROM Win32_DiskDrive WHERE DeviceID='{storage.DeviceID.Replace("\\", "\\\\")}'";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject drive in searcher.Get())
                {
                    ulong freeSpace = 0;
                    foreach (ManagementObject partition in drive.GetRelated("Win32_DiskPartition"))
                    {
                        foreach (ManagementObject logical in partition.GetRelated("Win32_LogicalDisk"))
                        {
                            if (logical["FreeSpace"] != null)
                            {
                                freeSpace += Convert.ToUInt64(logical["FreeSpace"]);
                            }
                            logical.Dispose();
                        }
                        partition.Dispose();
                    }
                    storage.FreeSpaceBytes = freeSpace;
                    storage.IsFreeSpaceCalculated = true;
                    drive.Dispose();
                }
            }
            catch { }
        }
    }
}