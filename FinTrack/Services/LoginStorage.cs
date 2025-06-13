using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace FinTrack.Services
{
    public class LoginData
    {
        public string Login { get; set; }
        public string Password { get; set; }
    }

    public static class LoginStorage
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "login.dat");

        public static void Save(LoginData data)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(data);
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(FilePath, protectedBytes);
            }
            catch
            {
                // логируем ошибку при необходимости
            }
        }

        public static LoginData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;

                byte[] protectedBytes = File.ReadAllBytes(FilePath);
                byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plainBytes);

                return JsonSerializer.Deserialize<LoginData>(json);
            }
            catch
            {
                return null; // ошибка расшифровки = treat as not found
            }
        }

        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }
    }
}
