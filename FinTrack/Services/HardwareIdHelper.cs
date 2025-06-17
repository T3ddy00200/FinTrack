using System.Management;

namespace FinTrack.Services
{
    public static class HardwareIdHelper
    {
        public static string GetHardwareId()
        {
            string cpu = GetWMI("Win32_Processor", "ProcessorId");
            string disk = GetWMI("Win32_DiskDrive", "SerialNumber");
            string mac = GetWMI("Win32_NetworkAdapterConfiguration", "MACAddress");

            return $"{cpu}-{disk}-{mac}".Replace(":", "").Replace(" ", "").Trim();
        }

        private static string GetWMI(string className, string property)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {className}");
                foreach (var o in searcher.Get())
                {
                    var value = o[property];
                    if (value != null) return value.ToString();
                }
            }
            catch { }
            return "";
        }
    }
}
