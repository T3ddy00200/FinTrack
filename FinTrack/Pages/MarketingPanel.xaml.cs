using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FinTrack.Models;
using FinTrack.Views;
using FinTrack.Windows;
using FinTrack.Views; // чтобы использовать внешний EmailHelper
using System.Threading.Tasks; // ← обязательно
using System.Net.Http;

namespace FinTrack.Pages
{
    public partial class MarketingPanel : UserControl
    {
        // Путь к списку получателей (можно заменить на вашу логику)
        private readonly string _debtorsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json");

        public ObservableCollection<Debtor> Recipients { get; } = new();

        public MarketingPanel()
        {
            InitializeComponent();
            DataContext = this;
            LoadRecipients();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // при необходимости локализация
            // LocalizationManager.LocalizeUI(this);
        }

        private void LoadRecipients()
        {
            if (!File.Exists(_debtorsPath)) return;
            try
            {
                var json = File.ReadAllText(_debtorsPath);
                var list = JsonSerializer.Deserialize<Debtor[]>(json) ?? Array.Empty<Debtor>();
                foreach (var d in list)
                    Recipients.Add(d);
                RecipientsListBox.ItemsSource = Recipients;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки получателей: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SendMarketing_Click(object sender, RoutedEventArgs e)
        {
            var allRecipients = Recipients
                .Where(d => !string.IsNullOrWhiteSpace(d.Email))
                .GroupBy(d => d.Email.ToLower())
                .Select(g => g.First())
                .ToList();

            if (allRecipients.Count == 0)
            {
                MessageBox.Show("Нет получателей с email.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string templatePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FinTrack", "marketing_template.html");

            if (!File.Exists(templatePath))
            {
                MessageBox.Show("Сначала сохраните шаблон письма.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string htmlTemplate = File.ReadAllText(templatePath);

            var senderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FinTrack", "sender.json");

            if (!File.Exists(senderPath))
            {
                MessageBox.Show("Настройте email отправителя.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var senderJson = File.ReadAllText(senderPath);
            var senderCfg = JsonSerializer.Deserialize<EmailSenderConfig>(senderJson);
            if (senderCfg == null || string.IsNullOrWhiteSpace(senderCfg.Email))
            {
                MessageBox.Show("Email отправителя не задан.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string senderEmail = senderCfg.Email;

            int sent = 0;

            foreach (var debtor in allRecipients)
            {
                string token = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{senderEmail}|{debtor.Email}")
                );

                string body = htmlTemplate
                    .Replace("{Name}", debtor.Name)
                    .Replace("{Debt}", debtor.Balance.ToString("F2"))
                    .Replace("{UnsubscribeToken}", token);

                EmailHelper.Send(senderEmail, debtor.Email, "📢 Новое предложение", body);
                sent++;

                await Task.Delay(2000); // ⏱ задержка 2 сек
            }

            MessageBox.Show($"Сообщения отправлены: {sent}", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenMarketingSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new MarketingWindow
            {
                Owner = Window.GetWindow(this)
            };
            settingsWindow.ShowDialog();

            // Например, после закрытия:
            // MessageTextBox.Text = settingsWindow.ResultTemplate;
        }

        private void CancelMarketing_Click(object sender, RoutedEventArgs e)
        {
            // Сброс формы
            RecipientsListBox.SelectedItems.Clear();
        }

        private async void TestUnsubscribed_Click(object sender, RoutedEventArgs e)
        {
            var senderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FinTrack", "sender.json");

            if (!File.Exists(senderPath))
            {
                MessageBox.Show("sender.json не найден.");
                return;
            }

            var senderJson = File.ReadAllText(senderPath);
            var senderCfg = JsonSerializer.Deserialize<EmailSenderConfig>(senderJson);

            if (senderCfg == null || string.IsNullOrWhiteSpace(senderCfg.Email))
            {
                MessageBox.Show("Email отправителя не задан.");
                return;
            }

            var list = await GetUnsubscribersAsync(senderCfg.Email);

            MessageBox.Show(
                list.Count > 0 ? $"Отписавшиеся:\n\n{string.Join("\n", list)}" : "Список отписавшихся пуст.",
                "Unsubscribed", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task<List<string>> GetUnsubscribersAsync(string senderEmail)
        {
            try
            {
                using var client = new HttpClient();
                var url = $"https://1e5c-84-54-92-35.ngrok-free.app/unsubscribers?sender={senderEmail}";
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось получить список отписавшихся: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return new List<string>();
            }
        }

    }
}
