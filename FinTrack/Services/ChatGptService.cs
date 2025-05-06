using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FinTrack.Models;
using FinTrack.Properties;
using FinTrack.Views;

namespace FinTrack.Services
{
    public class ChatGptService
    {
        private readonly HttpClient _http;
        private readonly string _debtorsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json");

        // Системное сообщение задаёт роль и мульти-язычность
        private const string SystemPrompt = @"
You are the FinTrack assistant.
You have access to the full list of debtors (name, email, and outstanding balance).
Your only task: generate notification email text for all overdue debtors.
Always reply in the same language the user used.";

        public ChatGptService()
        {
            _http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/") };
            var key = Settings.Default.OpenAiApiKey;
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        /// <summary>
        /// Универсальный метод: отправляет любой prompt и возвращает ответ.
        /// </summary>
        public async Task<string> GetChatCompletionAsync(string prompt)
        {
            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user",   content = prompt }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8, "application/json");

            using var resp = await _http.PostAsync("v1/chat/completions", content);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return $"OpenAI error {(int)resp.StatusCode}: {body}";

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString()?
                      .Trim() ?? string.Empty;
        }

        /// <summary>
        /// Специализированный метод: формирует список просроченных должников
        /// и запрашивает у модели текст письма-уведомления на их основе.
        /// </summary>
        public async Task<string> GenerateOverdueNotificationAsync(string userLanguageCode)
        {
            // 1) читаем должников
            List<Debtor> debtors;
            try
            {
                var json = await File.ReadAllTextAsync(_debtorsPath);
                debtors = JsonSerializer.Deserialize<List<Debtor>>(json)
                          ?? new List<Debtor>();
            }
            catch
            {
                debtors = new List<Debtor>();
            }

            // 2) фильтруем просроченные
            var overdue = debtors
                .Where(d => d.Balance > 0 && d.DueDate < DateTime.Today)
                .ToList();

            if (overdue.Count == 0)
            {
                return userLanguageCode.StartsWith("ru")
                    ? "Нет просроченных должников."
                    : "There are no overdue debtors.";
            }

            // 3) строим пользовательский промпт
            var sb = new StringBuilder();
            sb.AppendLine(userLanguageCode.StartsWith("ru")
                ? "Список просроченных должников:"
                : "List of overdue debtors:");
            foreach (var d in overdue)
            {
                var bal = d.Balance;
                sb.AppendLine(userLanguageCode.StartsWith("ru")
                    ? $"- {d.Name}: долг {bal:0.00}₽, email: {d.Email}"
                    : $"- {d.Name}: debt {bal:0.00}, email: {d.Email}");
            }

            sb.AppendLine();
            sb.AppendLine(userLanguageCode.StartsWith("ru")
                ? "Сгенерируй, пожалуйста, текст письма-уведомления для этих должников, " +
                  "включив приветствие, обращение по имени, сумму долга и призыв к оплате."
                : "Please generate a notification email text for these debtors, " +
                  "including greeting, personalization by name, debt amount and a call to action.");

            // 4) отправляем промпт в OpenAI
            return await GetChatCompletionAsync(sb.ToString());
        }
    }
}
