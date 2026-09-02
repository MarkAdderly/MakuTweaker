using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MakuTweakerNew
{
    /// <summary>
    /// Патчер отступов проводника Windows 11 в режиме списка.
    /// Перенос логики ExplorerListPaddingPatcher.js на C#.
    /// </summary>
    public static class ExplorerListPaddingPatcher
    {
        private static readonly string SystemFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SystemResources",
            "ExplorerFrame.dll.mun");

        private static readonly string BackupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup");

        private const int MaxBackupCount = 3;
        private const int MinSupportedBuild = 22000; // Windows 11 21H2

        public class PatchRule
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string OldValue { get; set; } = "";
            public string NewValue { get; set; } = "";
            public string Affects { get; set; } = "";
        }

        public class PatchResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public int AppliedCount { get; set; }
        }

        private class PEInfo
        {
            public int PEOffset { get; set; }
            public int SectionCount { get; set; }
            public int OptionalHeaderSize { get; set; }
            public bool Is64Bit { get; set; }
        }

        private class UIFileResource
        {
            public int Type { get; set; }
            public int Id { get; set; }
            public int Offset { get; set; }
            public int Size { get; set; }
            public string Signature { get; set; } = "";
        }

        // Патчи ровно те же, что в JS-версии, значения проверены вручную
        private static readonly List<PatchRule> PatchRules = new List<PatchRule>
        {
            new PatchRule
            {
                Name = "item.list padding (6rp → 0rp)",
                Description = "Убирает левый отступ 6rp у элементов списка",
                OldValue = "rect(6rp,0,0,0)",
                NewValue = "rect(0rp,0,0,0)",
                Affects = "item.list"
            },
            new PatchRule
            {
                Name = "item.list size (28rp → 0rp)",
                Description = "Убирает зарезервированную ширину 28rp (область чекбокса/иконки)",
                OldValue = "size(28rp,0rp)",
                NewValue = "size(00rp,0rp)",
                Affects = "item.list"
            },
            new PatchRule
            {
                Name = "collection.smallicons padding (14rp → 7rp)",
                Description = "Убирает левый отступ 14rp в режиме мелких значков",
                OldValue = "rect(14rp,0,0,0)",
                NewValue = "rect(07rp,0,0,0)",
                Affects = "collection.smallicons"
            },
            new PatchRule
            {
                Name = "checkbox visibleminsize width (28rp → 0rp)",
                Description = "Уменьшает минимальную ширину чекбокса с 28rp до 0rp",
                OldValue = "size(28rp,20rp)",
                NewValue = "size(00rp,20rp)",
                Affects = "item.row.checkbox"
            },
            new PatchRule
            {
                Name = "collection.list.insidegroup padding (2rp left → 0rp)",
                Description = "Уменьшает левый отступ внутри группы в списке",
                OldValue = "rect(2rp,2rp,6rp,0rp)",
                NewValue = "rect(0rp,2rp,0rp,0rp)",
                Affects = "collection.list.insidegroup"
            },
            new PatchRule
            {
                Name = "SeparatorPadding left (4rp → 0rp)",
                Description = "Уменьшает левый отступ разделителя групп в списке",
                OldValue = "rect(4rp,0rp,9rp,0rp)",
                NewValue = "rect(0rp,0rp,0rp,0rp)",
                Affects = "collection.list.groups separator"
            }
        };

        // ============================================================
        // Публичные методы
        // ============================================================

        /// <summary>
        /// Проверяет, что система — Windows 11 21H2 или новее
        /// </summary>
        public static bool IsWindows11Supported()
        {
            try
            {
                using (var registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    object? buildValue = registryKey?.GetValue("CurrentBuild");
                    if (buildValue != null && int.TryParse(buildValue.ToString(), out int buildNumber))
                    {
                        return buildNumber >= MinSupportedBuild;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Проверяет, запущен ли процесс с правами администратора
        /// </summary>
        public static bool IsRunningAsAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// Есть ли хотя бы один бэкап для отката
        /// </summary>
        public static bool HasBackups()
        {
            return GetLatestBackup() != null;
        }

        /// <summary>
        /// Путь к папке, где твикер хранит бэкапы системного файла
        /// </summary>
        public static string BackupFolderPath => BackupFolder;

        /// <summary>
        /// Проверяет, применён ли патч к системному файлу
        /// </summary>
        public static async Task<bool> IsPatchedAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(SystemFilePath))
                    {
                        return false;
                    }

                    byte[] fileData = File.ReadAllBytes(SystemFilePath);
                    UIFileResource? targetUIFile = FindTargetUIFile(fileData);
                    if (targetUIFile == null)
                    {
                        return false;
                    }

                    byte[] originalPattern = Encoding.Unicode.GetBytes(PatchRules[0].OldValue);
                    byte[] patchedPattern = Encoding.Unicode.GetBytes(PatchRules[0].NewValue);

                    int originalCount = CountOccurrences(fileData, originalPattern, targetUIFile.Offset, targetUIFile.Size);
                    int patchedCount = CountOccurrences(fileData, patchedPattern, targetUIFile.Offset, targetUIFile.Size);

                    return originalCount == 0 && patchedCount >= 1;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Применяет патч: бэкап, остановка проводника, замена байтов, перезапуск проводника
        /// </summary>
        public static async Task<PatchResult> ApplyPatchAsync()
        {
            return await Task.Run(() =>
            {
                var result = new PatchResult();

                try
                {
                    if (!IsRunningAsAdmin())
                    {
                        result.Message = "Недостаточно прав: запусти MakuTweaker от имени администратора";
                        return result;
                    }

                    if (!IsWindows11Supported())
                    {
                        result.Message = "Требуется Windows 11 21H2 или новее";
                        return result;
                    }

                    if (!File.Exists(SystemFilePath))
                    {
                        result.Message = "Системный файл не найден: " + SystemFilePath;
                        return result;
                    }

                    byte[] fileData = File.ReadAllBytes(SystemFilePath);

                    if (!ValidatePE(fileData))
                    {
                        result.Message = "Файл не является валидным PE-файлом";
                        return result;
                    }

                    UIFileResource? targetUIFile = FindTargetUIFile(fileData);
                    if (targetUIFile == null)
                    {
                        result.Message = "Целевой UIFILE ресурс не найден";
                        return result;
                    }

                    // Отбираем только патчи с уникальным вхождением в целевом UIFILE
                    var applicableRules = new List<PatchRule>();
                    foreach (PatchRule rule in PatchRules)
                    {
                        byte[] oldBytes = Encoding.Unicode.GetBytes(rule.OldValue);
                        byte[] newBytes = Encoding.Unicode.GetBytes(rule.NewValue);

                        if (oldBytes.Length != newBytes.Length)
                        {
                            continue;
                        }

                        int occurrenceCount = CountOccurrences(fileData, oldBytes, targetUIFile.Offset, targetUIFile.Size);
                        if (occurrenceCount == 1)
                        {
                            applicableRules.Add(rule);
                        }
                    }

                    if (applicableRules.Count == 0)
                    {
                        result.Message = "Нет применимых патчей (файл уже пропатчен или версия Windows не поддерживается)";
                        return result;
                    }

                    CreateBackup();

                    try
                    {
                        TakeOwnership();

                        // Берём права и патчим в памяти при живом проводнике —
                        // мёртвым он остаётся только на время записи файла
                        KillExplorerProcesses();

                        int appliedCount = 0;
                        foreach (PatchRule rule in applicableRules)
                        {
                            byte[] oldBytes = Encoding.Unicode.GetBytes(rule.OldValue);
                            byte[] newBytes = Encoding.Unicode.GetBytes(rule.NewValue);

                            int patchOffset = FindPatternOffset(fileData, oldBytes, targetUIFile.Offset, targetUIFile.Size);
                            if (patchOffset >= 0)
                            {
                                Array.Copy(newBytes, 0, fileData, patchOffset, newBytes.Length);
                                appliedCount++;
                            }
                        }

                        WriteSystemFile(fileData);
                        NormalizePermissions();

                        result.Success = true;
                        result.AppliedCount = appliedCount;
                        result.Message = "Патч применён, изменено значений: " + appliedCount;
                    }
                    finally
                    {
                        RestartExplorer();
                    }
                }
                catch (Exception exception)
                {
                    result.Message = "Ошибка: " + exception.Message;
                }

                return result;
            });
        }

        /// <summary>
        /// Откат: восстанавливает системный файл из последнего бэкапа
        /// </summary>
        public static async Task<PatchResult> RevertPatchAsync()
        {
            return await Task.Run(() =>
            {
                var result = new PatchResult();

                try
                {
                    if (!IsRunningAsAdmin())
                    {
                        result.Message = "Недостаточно прав: запусти MakuTweaker от имени администратора";
                        return result;
                    }

                    string? backupFile = GetLatestBackup();
                    if (backupFile == null)
                    {
                        result.Message = "Бэкап не найден, откат невозможен";
                        return result;
                    }

                    TakeOwnership();
                    StopExplorer();

                    try
                    {
                        RestoreFromBackup(backupFile);
                        NormalizePermissions();

                        result.Success = true;
                        result.Message = "Оригинальный файл восстановлен из бэкапа";
                    }
                    finally
                    {
                        RestartExplorer();
                    }
                }
                catch (Exception exception)
                {
                    result.Message = "Ошибка: " + exception.Message;
                }

                return result;
            });
        }

        // ============================================================
        // PE-парсинг (перенос из JS)
        // ============================================================

        private static bool ValidatePE(byte[] fileData)
        {
            if (fileData.Length < 64) return false;
            if (fileData[0] != 0x4D || fileData[1] != 0x5A) return false; // "MZ"

            int peOffset = BitConverter.ToInt32(fileData, 0x3C);
            if (peOffset + 4 > fileData.Length) return false;
            if (fileData[peOffset] != 0x50 || fileData[peOffset + 1] != 0x45 ||
                fileData[peOffset + 2] != 0x00 || fileData[peOffset + 3] != 0x00)
            {
                return false; // "PE\0\0"
            }

            return true;
        }

        private static PEInfo GetPEInfo(byte[] fileData)
        {
            int peOffset = BitConverter.ToInt32(fileData, 0x3C);
            int sectionCount = BitConverter.ToInt16(fileData, peOffset + 6);
            int optionalHeaderSize = BitConverter.ToInt16(fileData, peOffset + 20);
            int magic = BitConverter.ToInt16(fileData, peOffset + 24);

            return new PEInfo
            {
                PEOffset = peOffset,
                SectionCount = sectionCount,
                OptionalHeaderSize = optionalHeaderSize,
                Is64Bit = magic == 0x20B
            };
        }

        private static int RvaToFileOffset(byte[] fileData, PEInfo peInfo, int rva)
        {
            int sectionStart = peInfo.PEOffset + 24 + peInfo.OptionalHeaderSize;
            for (int sectionIndex = 0; sectionIndex < peInfo.SectionCount; sectionIndex++)
            {
                int sectionOffset = sectionStart + sectionIndex * 40;
                int sectionRva = BitConverter.ToInt32(fileData, sectionOffset + 12);
                int sectionVirtualSize = BitConverter.ToInt32(fileData, sectionOffset + 8);
                int sectionRawPointer = BitConverter.ToInt32(fileData, sectionOffset + 20);

                if (rva >= sectionRva && rva < sectionRva + sectionVirtualSize)
                {
                    return rva - sectionRva + sectionRawPointer;
                }
            }
            return -1;
        }

        private static int FindSectionRva(byte[] fileData, PEInfo peInfo, int rva)
        {
            int sectionStart = peInfo.PEOffset + 24 + peInfo.OptionalHeaderSize;
            for (int sectionIndex = 0; sectionIndex < peInfo.SectionCount; sectionIndex++)
            {
                int sectionOffset = sectionStart + sectionIndex * 40;
                int sectionRva = BitConverter.ToInt32(fileData, sectionOffset + 12);
                int sectionVirtualSize = BitConverter.ToInt32(fileData, sectionOffset + 8);

                if (rva >= sectionRva && rva < sectionRva + sectionVirtualSize)
                {
                    return sectionRva;
                }
            }
            return 0;
        }

        private static List<UIFileResource> ScanUIFileResources(byte[] fileData, PEInfo peInfo)
        {
            var resources = new List<UIFileResource>();

            int dataDirectoryStart = peInfo.Is64Bit
                ? peInfo.PEOffset + 24 + 112
                : peInfo.PEOffset + 24 + 96;

            int resourceRva = BitConverter.ToInt32(fileData, dataDirectoryStart + 16);
            if (resourceRva == 0)
            {
                return resources;
            }

            int resourceFileOffset = RvaToFileOffset(fileData, peInfo, resourceRva);
            if (resourceFileOffset < 0)
            {
                return resources;
            }

            int resourceSectionRva = FindSectionRva(fileData, peInfo, resourceRva);

            void ParseDirectory(int baseOffset, int level, int typeId)
            {
                int absoluteOffset = resourceFileOffset + baseOffset;
                if (absoluteOffset + 16 > fileData.Length)
                {
                    return;
                }

                int namedEntryCount = BitConverter.ToInt16(fileData, absoluteOffset + 12);
                int idEntryCount = BitConverter.ToInt16(fileData, absoluteOffset + 14);
                int totalEntryCount = namedEntryCount + idEntryCount;

                for (int entryIndex = 0; entryIndex < totalEntryCount; entryIndex++)
                {
                    int entryOffset = absoluteOffset + 16 + entryIndex * 8;
                    if (entryOffset + 8 > fileData.Length)
                    {
                        break;
                    }

                    int nameOrId = BitConverter.ToInt32(fileData, entryOffset);
                    int offsetToData = BitConverter.ToInt32(fileData, entryOffset + 4);
                    bool isSubdirectory = ((offsetToData >> 31) & 1) == 1;
                    int offsetValue = offsetToData & 0x7FFFFFFF;

                    if (isSubdirectory)
                    {
                        int nextTypeId = level > 0 ? typeId : nameOrId;
                        ParseDirectory(offsetValue, level + 1, nextTypeId);
                    }
                    else
                    {
                        int dataEntryOffset = resourceFileOffset + offsetValue;
                        if (dataEntryOffset + 16 <= fileData.Length)
                        {
                            int dataRva = BitConverter.ToInt32(fileData, dataEntryOffset);
                            int dataSize = BitConverter.ToInt32(fileData, dataEntryOffset + 4);
                            int dataFileOffset = dataRva - resourceSectionRva + resourceFileOffset;

                            if (dataFileOffset >= 0 && dataFileOffset + dataSize <= fileData.Length)
                            {
                                string signature = Encoding.ASCII.GetString(fileData, dataFileOffset, Math.Min(4, dataSize));

                                resources.Add(new UIFileResource
                                {
                                    Type = typeId,
                                    Id = level >= 1 ? nameOrId : 0,
                                    Offset = dataFileOffset,
                                    Size = dataSize,
                                    Signature = signature
                                });
                            }
                        }
                    }
                }
            }

            ParseDirectory(0, 0, 0);
            return resources;
        }

        /// <summary>
        /// Ищет целевой UIFILE: сначала по ID 40960, иначе крупнейший DUIB-ресурс
        /// </summary>
        private static UIFileResource? FindTargetUIFile(byte[] fileData)
        {
            if (!ValidatePE(fileData))
            {
                return null;
            }

            PEInfo peInfo = GetPEInfo(fileData);
            List<UIFileResource> resources = ScanUIFileResources(fileData, peInfo);

            List<UIFileResource> duibResources = resources
                .Where(resource => resource.Signature.StartsWith("duib"))
                .ToList();

            if (duibResources.Count == 0)
            {
                return null;
            }

            UIFileResource? byId = duibResources.FirstOrDefault(resource => resource.Id == 40960);
            if (byId != null)
            {
                return byId;
            }

            return duibResources.OrderByDescending(resource => resource.Size).First();
        }

        // ============================================================
        // Поиск и подсчёт вхождений паттернов
        // ============================================================

        private static int CountOccurrences(byte[] fileData, byte[] pattern, int regionOffset, int regionSize)
        {
            int count = 0;
            int regionEnd = regionOffset + regionSize;

            for (int searchIndex = regionOffset; searchIndex <= regionEnd - pattern.Length; searchIndex++)
            {
                if (MatchesAt(fileData, pattern, searchIndex))
                {
                    count++;
                }
            }

            return count;
        }

        private static int FindPatternOffset(byte[] fileData, byte[] pattern, int regionOffset, int regionSize)
        {
            int regionEnd = regionOffset + regionSize;

            for (int searchIndex = regionOffset; searchIndex <= regionEnd - pattern.Length; searchIndex++)
            {
                if (MatchesAt(fileData, pattern, searchIndex))
                {
                    return searchIndex;
                }
            }

            return -1;
        }

        private static bool MatchesAt(byte[] fileData, byte[] pattern, int position)
        {
            for (int byteIndex = 0; byteIndex < pattern.Length; byteIndex++)
            {
                if (fileData[position + byteIndex] != pattern[byteIndex])
                {
                    return false;
                }
            }

            return true;
        }

        // ============================================================
        // Бэкапы
        // ============================================================

        private static void CreateBackup()
        {
            if (!Directory.Exists(BackupFolder))
            {
                Directory.CreateDirectory(BackupFolder);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFile = Path.Combine(BackupFolder, "ExplorerFrame.dll.mun_" + timestamp + ".bak");
            File.Copy(SystemFilePath, backupFile);

            // Держим не больше MaxBackCount бэкапов, старые удаляем
            List<string> oldBackups = Directory.GetFiles(BackupFolder, "ExplorerFrame.dll.mun_*.bak")
                .OrderByDescending(path => File.GetCreationTime(path))
                .Skip(MaxBackupCount)
                .ToList();

            foreach (string oldBackup in oldBackups)
            {
                try
                {
                    File.Delete(oldBackup);
                }
                catch
                {
                }
            }
        }

        private static string? GetLatestBackup()
        {
            if (!Directory.Exists(BackupFolder))
            {
                return null;
            }

            return Directory.GetFiles(BackupFolder, "ExplorerFrame.dll.mun_*.bak")
                .OrderByDescending(path => File.GetCreationTime(path))
                .FirstOrDefault();
        }

        // ============================================================
        // Работа с проводником и правами
        // ============================================================

        private static void StopExplorer()
        {
            KillExplorerProcesses();
            Thread.Sleep(1000);
        }

        private static void KillExplorerProcesses()
        {
            string[] processNames = { "explorer", "ShellExperienceHost", "SearchHost", "StartMenuExperienceHost", "TextInputHost" };

            foreach (string processName in processNames)
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void RestartExplorer()
        {
            // Даём оболочке шанс запуститься самой — она подхватит новый файл.
            // Запускаем вручную только если Windows не сделала это сама:
            // «второй» explorer открывается лишним окном.
            Thread.Sleep(1500);

            if (Process.GetProcessesByName("explorer").Length == 0)
            {
                try
                {
                    Process.Start("explorer.exe");
                }
                catch
                {
                }
            }

            Thread.Sleep(1000);
        }

        /// <summary>
        /// Записывает системный файл. Если автозапустившаяся оболочка успела
        /// занять файл, прибиваем её и пробуем снова.
        /// </summary>
        private static void WriteSystemFile(byte[] fileData)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    File.WriteAllBytes(SystemFilePath, fileData);
                    return;
                }
                catch (IOException)
                {
                    KillExplorerProcesses();
                    Thread.Sleep(700);
                }
            }

            File.WriteAllBytes(SystemFilePath, fileData);
        }

        /// <summary>
        /// Восстанавливает файл из бэкапа с той же логикой ретраев при блокировке
        /// </summary>
        private static void RestoreFromBackup(string backupFile)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    File.Copy(backupFile, SystemFilePath, true);
                    return;
                }
                catch (IOException)
                {
                    KillExplorerProcesses();
                    Thread.Sleep(700);
                }
            }

            File.Copy(backupFile, SystemFilePath, true);
        }

        private static void TakeOwnership()
        {
            string currentUser = WindowsIdentity.GetCurrent().Name;

            RunCommand("takeown", "/f \"" + SystemFilePath + "\"");
            RunCommand("icacls", "\"" + SystemFilePath + "\" /inheritance:e");
            RunCommand("icacls", "\"" + SystemFilePath + "\" /grant \"" + currentUser + ":(F)\"");
            RunCommand("icacls", "\"" + SystemFilePath + "\" /grant \"*S-1-5-32-544:(F)\"");
            RunCommand("icacls", "\"" + SystemFilePath + "\" /grant \"*S-1-5-18:(F)\"");

            File.SetAttributes(SystemFilePath, File.GetAttributes(SystemFilePath) & ~FileAttributes.ReadOnly);
        }

        private static void NormalizePermissions()
        {
            string currentUser = WindowsIdentity.GetCurrent().Name;

            RunCommand("takeown", "/f \"" + SystemFilePath + "\"");
            RunCommand("icacls", "\"" + SystemFilePath + "\" /inheritance:e");
            RunCommand("icacls", "\"" + SystemFilePath + "\" /grant \"" + currentUser + ":(F)\"");
            RunCommand("icacls", "\"" + SystemFilePath + "\" /grant \"*S-1-5-32-544:(F)\"");
            RunCommand("icacls", "\"" + SystemFilePath + "\" /grant \"*S-1-5-18:(F)\"");

            File.SetAttributes(SystemFilePath, File.GetAttributes(SystemFilePath) & ~FileAttributes.ReadOnly);
        }

        private static void RunCommand(string fileName, string arguments)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = fileName;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    process.WaitForExit(10000);
                }
            }
            catch
            {
            }
        }
    }
}
