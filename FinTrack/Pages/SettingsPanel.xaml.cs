using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FinTrack.Models;
using FinTrack.Controls;

namespace FinTrack.Pages
{
    public partial class SettingsPanel : UserControl
    {
        private readonly string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "config.json"
        );

        public SettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LocalizeUI(this);
        }

        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string cultureCode)
            {
                LocalizationManager.SetCulture(cultureCode);
                LocalizationManager.LocalizeUI(Application.Current.MainWindow);

                SaveSettings(cultureCode); // <--- важно!
            }
        }


        private void LanguagePopup_Closed(object sender, EventArgs e)
        {
            LanguageToggleButton.IsChecked = false;
        }

        private void SaveSettings(string lang)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            var config = new AppSettings { Language = lang };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        private void LoadSettings()
        {
            if (!File.Exists(configPath)) return;

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppSettings>(json);
            if (config == null) return;

            LocalizationManager.SetCulture(config.Language);
            LocalizationManager.LocalizeUI(Application.Current.MainWindow);

            string autoPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "autosend_config.json");

            if (File.Exists(autoPath))
            {
                try
                {
                    var autoJson = File.ReadAllText(autoPath);
                    var autoConfig = JsonSerializer.Deserialize<AutoSendSettings>(autoJson);

                    if (autoConfig != null)
                    {
                        AutoSendEnabledCheckBox.IsChecked = autoConfig.Enabled;
                        AutoNotificationTimeBox.Text = autoConfig.Time;
                        AutoNotificationTextBox.Text = autoConfig.MessageText;
                    }
                }
                catch { }
            }
        }

        private void InsertNameTag_Click(object sender, RoutedEventArgs e)
        {
            AutoNotificationTextBox.Text += " {Name}";
            AutoNotificationTextBox.Focus();
            AutoNotificationTextBox.CaretIndex = AutoNotificationTextBox.Text.Length;
        }

        private void InsertDebtTag_Click(object sender, RoutedEventArgs e)
        {
            AutoNotificationTextBox.Text += " {Debt}";
            AutoNotificationTextBox.Focus();
            AutoNotificationTextBox.CaretIndex = AutoNotificationTextBox.Text.Length;
        }

        private void SaveAutoNotificationText_Click(object sender, RoutedEventArgs e)
        {
            var autoPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "autosend_config.json");

            AutoSendSettings config;

            // Загружаем текущие настройки, если они уже есть
            if (File.Exists(autoPath))
            {
                var json = File.ReadAllText(autoPath);
                config = JsonSerializer.Deserialize<AutoSendSettings>(json) ?? new AutoSendSettings();
            }
            else
            {
                config = new AutoSendSettings();
            }

            // Обновляем только текст уведомления
            config.MessageText = AutoNotificationTextBox.Text.Trim();

            var updatedJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(autoPath)!);
            File.WriteAllText(autoPath, updatedJson);

            MessageBox.Show("Текст автоуведомления сохранён.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private class AppSettings
        {
            public string Language { get; set; } = "ru";
        }

        public static class AppInitializer
        {
            public static void LoadLanguageFromConfig()
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FinTrack", "config.json"
                );

                if (!File.Exists(configPath)) return;

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppSettings>(json);
                if (config != null)
                {
                    LocalizationManager.SetCulture(config.Language);
                }
            }

            private class AppSettings
            {
                public string Language { get; set; } = "ru";
            }
        }

        private void ApplySenderSettingsToMessagesPanel(string email, string password, string readPassword)
        {
            // Проверяем, что главное окно и content-панель доступны
            if (Application.Current.MainWindow is MainWindow main)
            {
                if (main.FindName("MainContentPanel") is ContentControl contentControl &&
                    contentControl.Content is FinTrack.Controls.MessagesPanel messagesPanel)
                {
                    messagesPanel.ApplySenderSettings(email, password, readPassword);
                }
            }
        }



        private void SaveEmailButton_Click(object sender, RoutedEventArgs e)
        {
            var email = SenderEmailBox.Text.Trim();
            var sendPass = SenderPasswordBox.Password;
            var readPass = ReadPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(sendPass) ||
                string.IsNullOrWhiteSpace(readPass))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveSenderSettings(email, sendPass, readPass);
        }

        private void MessagesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Если захочешь — сюда можно добавить отображение полного письма в отдельном окне
        }

        private void SaveSenderSettings(string email, string password, string readPassword)
        {
            string senderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FinTrack", "sender.json"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(senderPath)!);

            var config = new FinTrack.Models.EmailSenderConfig
            {
                Email = email,
                Password = password,
                ReadPassword = readPassword
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(senderPath, json);

            ApplySenderSettingsToMessagesPanel(email, password, readPassword);

            MessageBox.Show("Настройки email сохранены и применены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveAutoSendSettings_Click(object sender, RoutedEventArgs e)
        {
            var config = new AutoSendSettings
            {
                Enabled = AutoSendEnabledCheckBox.IsChecked == true,
                Time = AutoNotificationTimeBox.Text.Trim(),
                MessageText = AutoNotificationTextBox.Text.Trim()
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "autosend_config.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);

            MessageBox.Show("Настройки автоотправки сохранены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }


    }
    public class AutoSendSettings
    {
        public bool Enabled { get; set; }
        public string Time { get; set; } = "09:00";
        public string MessageText { get; set; } = "Здравствуйте! У вас есть задолженность.";
    }

}
