using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FinTrack.Controls;
using FinTrack.Models;

namespace FinTrack.Pages
{
    public partial class SettingsPanel : UserControl
    {
        // Путь к основному конфигу приложения (язык)
        private readonly string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "config.json");

        // Путь к файлу настроек автоотправки
        private readonly string autoConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "autosend_config.json");

        public SettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Применяем локализацию к панели
            LocalizationManager.LocalizeUI(this);
        }

        // Обработчик выбора языка
        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string culture)
            {
                LocalizationManager.SetCulture(culture);
                LocalizationManager.LocalizeUI(Application.Current.MainWindow);
                SaveAppLanguage(culture);
            }
        }

        private void LanguagePopup_Closed(object sender, EventArgs e)
        {
            LanguageToggleButton.IsChecked = false;
        }

        // Сохранение языка
        private void SaveAppLanguage(string lang)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var cfg = new AppSettings { Language = lang };
            File.WriteAllText(configPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Загрузка всех настроек
        private void LoadSettings()
        {
            // 1. Язык
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var appCfg = JsonSerializer.Deserialize<AppSettings>(json);
                    if (appCfg != null)
                    {
                        LocalizationManager.SetCulture(appCfg.Language);
                        LocalizationManager.LocalizeUI(Application.Current.MainWindow);
                    }
                }
                catch { /* можно логировать */ }
            }

            // 2. Настройки автоотправки
            if (File.Exists(autoConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(autoConfigPath);
                    var autoCfg = JsonSerializer.Deserialize<AutoSendSettings>(json);
                    if (autoCfg != null)
                    {
                        AutoSendEnabledCheckBox.IsChecked = autoCfg.Enabled;
                        AutoNotificationTextBox.Text = autoCfg.MessageText;

                        // Время
                        var timeItem = AutoNotificationTimeBox.Items
                            .OfType<ComboBoxItem>()
                            .FirstOrDefault(i => (string)i.Content == autoCfg.Time);
                        if (timeItem != null)
                            AutoNotificationTimeBox.SelectedItem = timeItem;

                        // Дата: строим дату текущего месяца на основании ScheduledDay
                        var today = DateTime.Today;
                        int day = Math.Min(autoCfg.ScheduledDay,
                                           DateTime.DaysInMonth(today.Year, today.Month));
                        AutoNotificationDatePicker.SelectedDate =
                            new DateTime(today.Year, today.Month, day);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка загрузки настроек автоуведомления:\n" + ex.Message,
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Вставка тега {Name}
        private void InsertNameTag_Click(object sender, RoutedEventArgs e)
        {
            AutoNotificationTextBox.Text += " {Name}";
            AutoNotificationTextBox.CaretIndex = AutoNotificationTextBox.Text.Length;
            AutoNotificationTextBox.Focus();
        }

        // Вставка тега {Debt}
        private void InsertDebtTag_Click(object sender, RoutedEventArgs e)
        {
            AutoNotificationTextBox.Text += " {Debt}";
            AutoNotificationTextBox.CaretIndex = AutoNotificationTextBox.Text.Length;
            AutoNotificationTextBox.Focus();
        }

        // Сохранение настроек автоотправки
        private void SaveAutoNotificationText_Click(object sender, RoutedEventArgs e)
        {
            if (AutoNotificationDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Выберите число месяца для автоотправки.",
                                "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int day = AutoNotificationDatePicker.SelectedDate.Value.Day;

            if (!(AutoNotificationTimeBox.SelectedItem is ComboBoxItem cbo))
            {
                MessageBox.Show("Выберите время отправки.",
                                "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string time = (string)cbo.Content;
            string message = AutoNotificationTextBox.Text.Trim();

            var cfg = new AutoSendSettings
            {
                Enabled = AutoSendEnabledCheckBox.IsChecked == true,
                Time = time,
                MessageText = message,
                ScheduledDay = day
            };

            Directory.CreateDirectory(Path.GetDirectoryName(autoConfigPath)!);
            File.WriteAllText(autoConfigPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));

            MessageBox.Show("Настройки автоотправки сохранены.",
                            "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Сохранение настроек email-отправителя
        private void SaveEmailButton_Click(object sender, RoutedEventArgs e)
        {
            var email = SenderEmailBox.Text.Trim();
            var pwd = SenderPasswordBox.Password;
            var readPwd = ReadPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(pwd) ||
                string.IsNullOrWhiteSpace(readPwd))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SaveSenderSettings(email, pwd, readPwd);
        }

        private void SaveSenderSettings(string email, string pwd, string readPwd)
        {
            var senderPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FinTrack", "sender.json");
            Directory.CreateDirectory(Path.GetDirectoryName(senderPath)!);

            var cfg = new EmailSenderConfig
            {
                Email = email,
                Password = pwd,
                ReadPassword = readPwd
            };
            File.WriteAllText(senderPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));

            // Применяем к панели сообщений сразу
            if (Application.Current.MainWindow is MainWindow main &&
                main.FindName("MainContentPanel") is ContentControl ctrl &&
                ctrl.Content is MessagesPanel mp)
            {
                mp.ApplySenderSettings(email, pwd, readPwd);
            }

            MessageBox.Show("Настройки email сохранены и применены.", "Готово",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Класс настроек приложения
        private class AppSettings
        {
            public string Language { get; set; } = "ru";
        }
    }

    // Модель настроек автоотправки
    public class AutoSendSettings
    {
        public bool Enabled { get; set; }
        public string Time { get; set; } = "09:00";  // формат HH:mm
        public string MessageText { get; set; } =
            "Здравствуйте! У вас есть задолженность.";
        public int ScheduledDay { get; set; } = 1;       // день месяца
    }
}
