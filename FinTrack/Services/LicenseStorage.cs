using System;
using System.IO;
using System.Text.Json;

namespace FinTrack.Services
{
    public static class LicenseStorage
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack");

        public static string FilePath => _filePath;


        private static readonly string _filePath = Path.Combine(AppDataPath, "installstate.json");

        public class LicenseData
        {
            public string Login { get; set; } = "";
            public string Key { get; set; } = "";
        }

        public static void Save(string login, string licenseKey)
        {
            try
            {
                if (!Directory.Exists(AppDataPath))
                    Directory.CreateDirectory(AppDataPath);

                var data = new LicenseData
                {
                    Login = login,
                    Key = licenseKey
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Ошибка при сохранении лицензии: " + ex.Message);
            }
        }

        public static (string login, string key)? Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Console.WriteLine("📁 Файл лицензии не найден.");
                    return null;
                }

                var json = File.ReadAllText(FilePath);
                var data = JsonSerializer.Deserialize<LicenseData>(json);

                if (!string.IsNullOrWhiteSpace(data?.Login) && !string.IsNullOrWhiteSpace(data?.Key))
                {
                    Console.WriteLine($"✅ Login: {data.Login}, Key: {data.Key}");
                    return (data.Login, data.Key);
                }

                Console.WriteLine("⚠️ Пустые поля login или key.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка Load(): {ex.Message}");
            }

            return null;
        }

        public static bool Exists() => File.Exists(FilePath);

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Ошибка при удалении лицензии: " + ex.Message);
            }
        }

        public static string GetPath() => FilePath;
    }
}
