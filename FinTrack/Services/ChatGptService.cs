using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FinTrack.Properties;

namespace FinTrack.Services
{
    public class ChatGptService
    {
        private readonly HttpClient _http;

        public ChatGptService()
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri("https://api.openai.com/")
            };
            var key = Settings.Default.OpenAiApiKey;  // Ваш ключ из Settings.settings
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", key);
        }

        /// <summary>
        /// Отправляет prompt в чат-модель и возвращает либо ответ, либо текст ошибки.
        /// </summary>
        public async Task<string> GetChatCompletionAsync(string prompt)
        {
            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant." },
                    new { role = "user",   content = prompt }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                using var resp = await _http.PostAsync("v1/chat/completions", content);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    // Вернём ошибку прямо в UI
                    return $"OpenAI error {(int)resp.StatusCode}: {body}";
                }

                using var doc = JsonDocument.Parse(body);
                var answer = doc.RootElement
                                .GetProperty("choices")[0]
                                .GetProperty("message")
                                .GetProperty("content")
                                .GetString();
                return answer?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"Request failed: {ex.Message}";
            }
        }
    }
}
