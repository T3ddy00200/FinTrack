using System;
using System.IO;

namespace FinTrack.Services
{
    public static class AuditLogger
    {
        // Путь к файлу лога
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "audit.log");

        static AuditLogger()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        }

        /// <summary>
        /// Записать запись в аудиторский лог.
        /// </summary>
        public static void Log(string action)
        {
            try
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {action}";
                File.AppendAllLines(LogPath, new[] { line });
            }
            catch
            {
                // на этот раз молча
            }
        }

        /// <summary>
        /// Считать все записи (используется для UI).
        /// </summary>
        public static string[] ReadAll() =>
            File.Exists(LogPath) ? File.ReadAllLines(LogPath) : Array.Empty<string>();
    }
}
