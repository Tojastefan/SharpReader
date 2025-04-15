using System;
using System.Management;
using System.Windows.Forms;
using System.Runtime.InteropServices;
namespace SharpReader
{
    internal class SystemInfoCollector
    {
        public static string GetSystemInfo()
        {
            try
            {
                // Pobieranie podstawowych informacji o systemie
                string osVersion = GetOSVersion();
                string architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                string userName = Environment.UserName;
                string deviceName = Environment.MachineName;
                string screenResolution = $"{Screen.PrimaryScreen.Bounds.Width}x{Screen.PrimaryScreen.Bounds.Height}";
                string totalRam = GetTotalRAM();
                string appMemoryUsage = $"{(Environment.WorkingSet / (1024 * 1024)):N0} MB"; // Aktualne użycie RAM przez aplikację

                // Tworzenie raportu
                return $"🖥️ Środowisko systemowe:\n" +
                       $"   🔹 System: {osVersion}\n" +
                       $"   🔹 Architektura: {architecture}\n" +
                       $"   🔹 Rozdzielczość: {screenResolution}\n" +
                       $"   🔹 RAM aplikacji: {appMemoryUsage}";
            }
            catch (Exception ex)
            {
                return $"❌ Błąd pobierania informacji o systemie: {ex.Message}";
            }
        }

        private static string GetTotalRAM()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        double totalRamGb = Convert.ToDouble(obj["TotalPhysicalMemory"]) / (1024 * 1024 * 1024);
                        return $"{totalRamGb:F2} GB";
                    }
                }
            }
            catch (Exception)
            {
                return "Nieznane";
            }

            // Jeśli żadna wartość nie została zwrócona w pętli, zwróć domyślną wartość
            return "Nieznane";
        }
        private static string GetOSVersion()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsVersion();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "macOS";
            }
            return "Nieznany system";
        }

        public static string GetWindowsVersion()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["Caption"].ToString(); // Zwraca nazwę systemu np. "Windows 11 Pro"
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Nieznana wersja Windows ({ex.Message})";
            }

            return "Nieznana wersja Windows";
        }
    }
}
