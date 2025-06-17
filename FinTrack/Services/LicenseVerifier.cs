using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace FinTrack.Services
{
    public static class LicenseVerifier
    {
        private const string Endpoint = "https://script.google.com/macros/s/AKfycbxWIKpoIBsUmJu-SZLVt-_029aiFagNsSiUgGaXI5Zn6zIj9a46DUurz5wrbyGj7CYp/exec";

        public static async Task<string> VerifyAsync(string login, string key, string hwid)
        {
            try
            {
                //MessageBox.Show("📡 Создаём HttpClient");

                using var httpClient = new HttpClient();

                //MessageBox.Show($"📤 Формируем JSON\nLogin: {login}\nKey: {key}\nHWID: {hwid}");

                var payload = new Dictionary<string, string>
                {
                    ["login"] = login,
                    ["key"] = key,
                    ["hwid"] = hwid
                };

                var json = JsonSerializer.Serialize(payload);
                //MessageBox.Show($"📦 JSON:\n{json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                //MessageBox.Show("📨 Отправляем POST-запрос...");

                var response = await httpClient.PostAsync(Endpoint, content);
                //MessageBox.Show($"✅ Ответ получен: {(int)response.StatusCode} {response.ReasonPhrase}");

                var responseText = await response.Content.ReadAsStringAsync();
                //MessageBox.Show($"📬 Ответ от сервера:\n{responseText}");

                return responseText.Trim();
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"❌ Ошибка при проверке лицензии:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return "ERROR";
            }
        }
    }
}
