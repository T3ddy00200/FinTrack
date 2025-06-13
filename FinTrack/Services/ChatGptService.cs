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
using FinTrack.Views;

namespace FinTrack.Services
{
    public class ChatGptService
    {
        private readonly HttpClient _http;
        private readonly string _debtorsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json");

        private readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "config.json");

        private readonly string _model = "gpt-3.5-turbo";
        private readonly string _systemPrompt;
        private readonly int _maxTokens;
        private readonly double _temperature;
        private readonly string _apiKey;

        private const string DefaultPrompt = @"
You are FinTrack assistant.
You have access to the full list of debtors (name, email, and outstanding balance).
Your task is to generate notification emails for all overdue debtors.
Always respond in Hebrew.";

        public ChatGptService()
        {
            _http = new HttpClient();

            _systemPrompt = DefaultPrompt;
            _maxTokens = 1024;
            _temperature = 1.0;
            _apiKey = "";

            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var cfg = JsonSerializer.Deserialize<AppSettings>(json);

                    _systemPrompt = string.IsNullOrWhiteSpace(cfg?.SystemPrompt) ? DefaultPrompt : cfg.SystemPrompt;
                    _apiKey = cfg?.AIApiKey?.Trim() ?? "";
                    _maxTokens = cfg?.MaxTokens > 0 ? cfg.MaxTokens : 1024;
                    _temperature = cfg?.Temperature > 0 ? cfg.Temperature : 1.0;
                }

                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    _http.BaseAddress = new Uri("https://api.openai.com/");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Ошибка инициализации ChatGptService: " + ex.Message);
            }
        }

        public async Task<string> GetChatCompletionAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return "⚠️ GPT отключён: API ключ не задан.";

            var payload = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = _systemPrompt },
                    new { role = "user", content = prompt }
                },
                max_tokens = _maxTokens,
                temperature = _temperature
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.PostAsync("v1/chat/completions", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"OpenAI error {(int)response.StatusCode}: {responseBody}";

                using var doc = JsonDocument.Parse(responseBody);
                return doc.RootElement
                          .GetProperty("choices")[0]
                          .GetProperty("message")
                          .GetProperty("content")
                          .GetString()?
                          .Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"❌ Ошибка при обращении к OpenAI: {ex.Message}";
            }
        }

        public async Task<string> GenerateOverdueNotificationAsync()
        {
            List<Debtor> debtors;

            try
            {
                var json = await File.ReadAllTextAsync(_debtorsPath);
                debtors = JsonSerializer.Deserialize<List<Debtor>>(json) ?? new List<Debtor>();
            }
            catch
            {
                return "שגיאה בקריאת רשימת החייבים.";
            }

            var overdue = debtors
                .Where(d => d.Balance > 0 && d.DueDate < DateTime.Today)
                .ToList();

            if (overdue.Count == 0)
                return "אין חייבים בפיגור.";

            var grouped = overdue
                .GroupBy(d => d.Email)
                .Select(g => new
                {
                    Email = g.Key,
                    Name = g.First().Name,
                    Debts = g.Select(d => new
                    {
                        Amount = d.Balance,
                        DueDate = d.DueDate.ToString("yyyy-MM-dd")
                    }).ToList()
                }).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("הנה רשימת החייבים בפיגור, מפורקת לפי אימייל. צור עבור כל אחד מהם מייל אישי עם פנייה בשמו, פירוט כל החובות (תאריך + סכום) וסיום מנומס שמבקש תשלום:");
            sb.AppendLine("[");
            for (int i = 0; i < grouped.Count; i++)
            {
                var d = grouped[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"name\": \"{d.Name}\",");
                sb.AppendLine($"    \"email\": \"{d.Email}\",");
                sb.AppendLine("    \"debts\": [");
                for (int j = 0; j < d.Debts.Count; j++)
                {
                    var debt = d.Debts[j];
                    sb.AppendLine("      {");
                    sb.AppendLine($"        \"amount\": {debt.Amount:0.00},");
                    sb.AppendLine($"        \"dueDate\": \"{debt.DueDate}\"");
                    sb.Append("      }");
                    if (j < d.Debts.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.AppendLine("    ]");
                sb.Append("  }");
                if (i < grouped.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("]");
            sb.AppendLine();
            sb.AppendLine("כתוב מייל התראה אישי עבור כל אחד מהחייבים האלה. השתמש בעברית בלבד.");

            return await GetChatCompletionAsync(sb.ToString());
        }

        private class AppSettings
        {
            public string Language { get; set; } = "he";
            public string SystemPrompt { get; set; } = "";
            public string AIApiKey { get; set; } = "";
            public int MaxTokens { get; set; } = 1024;
            public double Temperature { get; set; } = 1.0;
        }
    }
}
