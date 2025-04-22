using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit;            // для TimePicker
using FinTrack.Controls;
using FinTrack.Models;

namespace FinTrack.Pages
{
    public partial class SettingsPanel : UserControl
    {
        private readonly string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "config.json");

        private readonly string autoConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "autosend_config.json");

        public SettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        // Loaded в XAML
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LocalizeUI(this);
        }

        // Язык: клики по кнопкам в Popup
        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string cultureCode)
            {
                LocalizationManager.SetCulture(cultureCode);
                LocalizationManager.LocalizeUI(Application.Current.MainWindow);
                SaveAppLanguage(cultureCode);
            }
        }

        // Закрытие Popup
        private void LanguagePopup_Closed(object sender, EventArgs e)
        {
            LanguageToggleButton.IsChecked = false;
        }

        private void SaveAppLanguage(string lang)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var cfg = new AppSettings { Language = lang };
            File.WriteAllText(configPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void LoadSettings()
        {
            // 1) Язык
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
                catch { }
            }

            // 2) Авторассылка
            if (File.Exists(autoConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(autoConfigPath);
                    var autoCfg = JsonSerializer.Deserialize<AutoSendSettings>(json);

                    if (autoCfg != null)
                    {
                        AutoSendEnabledCheckBox.IsChecked = autoCfg.Enabled;
                        AutoNotificationSubjectTextBox.Text = autoCfg.SubjectTemplate;
                        AutoNotificationBodyTextBox.Text = autoCfg.BodyTemplate;

                        // Восстанавливаем время в TimePicker
                        if (TimeSpan.TryParse(autoCfg.Time, out var ts))
                            AutoNotificationTimePicker.Value = DateTime.Today + ts;

                        // Восстанавливаем дату
                        var today = DateTime.Today;
                        int day = Math.Min(autoCfg.ScheduledDay,
                                           DateTime.DaysInMonth(today.Year, today.Month));
                        AutoNotificationDatePicker.SelectedDate =
                            new DateTime(today.Year, today.Month, day);
                    }
                }
                catch (Exception ex)
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show("Ошибка загрузки автонастроек:\n" + ex.Message,
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Сохранение авторассылки
        private void SaveAutoNotificationText_Click(object sender, RoutedEventArgs e)
        {
            if (AutoNotificationDatePicker.SelectedDate == null)
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("Выберите число месяца.", "Внимание",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (AutoNotificationTimePicker.Value == null)
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("Выберите время.", "Внимание",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cfg = new AutoSendSettings
            {
                Enabled = AutoSendEnabledCheckBox.IsChecked == true,
                ScheduledDay = AutoNotificationDatePicker.SelectedDate.Value.Day,
                Time = AutoNotificationTimePicker.Value.Value.ToString("HH:mm"),
                SubjectTemplate = AutoNotificationSubjectTextBox.Text.Trim(),
                BodyTemplate = AutoNotificationBodyTextBox.Text.Trim()
            };

            Directory.CreateDirectory(Path.GetDirectoryName(autoConfigPath)!);
            File.WriteAllText(autoConfigPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));

            Xceed.Wpf.Toolkit.MessageBox.Show("Настройки авторассылки сохранены.",
                            "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Теги
        private void InsertNameTag_Auto_Click(object sender, RoutedEventArgs e)
        {
            AutoNotificationBodyTextBox.Text += " {Name}";
            AutoNotificationBodyTextBox.CaretIndex = AutoNotificationBodyTextBox.Text.Length;
            AutoNotificationBodyTextBox.Focus();
        }

        private void InsertDebtTag_Auto_Click(object sender, RoutedEventArgs e)
        {
            AutoNotificationBodyTextBox.Text += " {Debt}";
            AutoNotificationBodyTextBox.CaretIndex = AutoNotificationBodyTextBox.Text.Length;
            AutoNotificationBodyTextBox.Focus();
        }

        // Сохранение email‑настроек
        private void SaveEmailButton_Click(object sender, RoutedEventArgs e)
        {
            var email = SenderEmailBox.Text.Trim();
            var pwd = SenderPasswordBox.Password;
            var readPwd = ReadPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email)
             || string.IsNullOrWhiteSpace(pwd)
             || string.IsNullOrWhiteSpace(readPwd))
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("Заполните все поля.", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

            // Применяем к панели сообщений
            if (Application.Current.MainWindow is MainWindow main
             && main.FindName("MainContentPanel") is ContentControl ctrl
             && ctrl.Content is MessagesPanel mp)
            {
                mp.ApplySenderSettings(email, pwd, readPwd);
            }

            Xceed.Wpf.Toolkit.MessageBox.Show("Настройки email сохранены.", "Готово",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private class AppSettings
        {
            public string Language { get; set; } = "ru";
        }
    }

    // Модель авторассылки
    public class AutoSendSettings
    {
        public bool Enabled { get; set; }
        public string Time { get; set; } = "09:00";
        public int ScheduledDay { get; set; } = 1;
        public string SubjectTemplate { get; set; } = "Просроченная задолженность";
        public string BodyTemplate { get; set; } = "Здравствуйте, {Name}! У вас задолженность {Debt} ₽.";
    }
}
