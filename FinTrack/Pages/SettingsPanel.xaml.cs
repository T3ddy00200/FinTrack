using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit;            // для TimePicker
using FinTrack.Controls;
using FinTrack.Models;
using FinTrack.Services;

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

        // 1) SaveAppLanguage
        private void SaveAppLanguage(string lang)
        {
            AuditLogger.Log($"SaveAppLanguage: начинаем сохранение языка — {lang}");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var cfg = new AppSettings { Language = lang };
            File.WriteAllText(configPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
            AuditLogger.Log($"SaveAppLanguage: язык сохранён в {configPath}");
        }


        // 2) LoadSettings
        private void LoadSettings()
        {
            AuditLogger.Log("LoadSettings: начало загрузки настроек");

            // язык
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
                        AuditLogger.Log($"LoadSettings: язык загружен — {appCfg.Language}");
                    }
                }
                catch (Exception ex)
                {
                    AuditLogger.Log($"LoadSettings: ошибка при загрузке языка — {ex.Message}");
                }
            }

            // авторассылка
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

                        if (TimeSpan.TryParse(autoCfg.Time, out var ts))
                            AutoNotificationTimePicker.Value = DateTime.Today + ts;

                        var today = DateTime.Today;
                        int day = Math.Min(autoCfg.ScheduledDay,
                                           DateTime.DaysInMonth(today.Year, today.Month));
                        AutoNotificationDatePicker.SelectedDate =
                            new DateTime(today.Year, today.Month, day);

                        AuditLogger.Log($"LoadSettings: автонастройки загружены — день {autoCfg.ScheduledDay}, время {autoCfg.Time}");
                    }
                }
                catch (Exception ex)
                {
                    AuditLogger.Log($"LoadSettings: ошибка при загрузке автонастроек — {ex.Message}");
                }
            }

            AuditLogger.Log("LoadSettings: завершение загрузки настроек");
        }


        // Сохранение авторассылки
        // 3) SaveAutoNotificationText_Click
        private void SaveAutoNotificationText_Click(object sender, RoutedEventArgs e)
        {
            AuditLogger.Log("SaveAutoNotificationText_Click: сохранение авторассылки — начало");

            if (AutoNotificationDatePicker.SelectedDate == null)
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("Выберите число месяца.", "Внимание",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                AuditLogger.Log("SaveAutoNotificationText_Click: отменено — не выбрана дата");
                return;
            }
            if (AutoNotificationTimePicker.Value == null)
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("Выберите время.", "Внимание",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                AuditLogger.Log("SaveAutoNotificationText_Click: отменено — не выбрано время");
                return;
            }

            try
            {
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

                AuditLogger.Log($"SaveAutoNotificationText_Click: авторассылка сохранена — день {cfg.ScheduledDay}, время {cfg.Time}");
                Xceed.Wpf.Toolkit.MessageBox.Show("Настройки авторассылки сохранены.",
                                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AuditLogger.Log($"SaveAutoNotificationText_Click: ошибка сохранения — {ex.Message}");
                Xceed.Wpf.Toolkit.MessageBox.Show("Ошибка при сохранении автонастроек:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // Теги
        // 4) InsertNameTag_Auto_Click & InsertDebtTag_Auto_Click
        private void InsertNameTag_Auto_Click(object sender, RoutedEventArgs e)
        {
            AuditLogger.Log("InsertNameTag_Auto_Click: вставка тега {Name}");
            AutoNotificationBodyTextBox.Text += " {Name}";
            AutoNotificationBodyTextBox.CaretIndex = AutoNotificationBodyTextBox.Text.Length;
            AutoNotificationBodyTextBox.Focus();
        }

        private void InsertDebtTag_Auto_Click(object sender, RoutedEventArgs e)
        {
            AuditLogger.Log("InsertDebtTag_Auto_Click: вставка тега {Debt}");
            AutoNotificationBodyTextBox.Text += " {Debt}";
            AutoNotificationBodyTextBox.CaretIndex = AutoNotificationBodyTextBox.Text.Length;
            AutoNotificationBodyTextBox.Focus();
        }


        // Сохранение email‑настроек
        // 5) SaveEmailButton_Click
        private void SaveEmailButton_Click(object sender, RoutedEventArgs e)
        {
            AuditLogger.Log("SaveEmailButton_Click: сохранение настроек email — начало");

            var email = SenderEmailBox.Text.Trim();
            var pwd = SenderPasswordBox.Password;
            var readPwd = ReadPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email)
             || string.IsNullOrWhiteSpace(pwd)
             || string.IsNullOrWhiteSpace(readPwd))
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("Заполните все поля.", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                AuditLogger.Log("SaveEmailButton_Click: отменено — не все поля заполнены");
                return;
            }

            try
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

                if (Application.Current.MainWindow is MainWindow main
                 && main.FindName("MainContentPanel") is ContentControl ctrl
                 && ctrl.Content is MessagesPanel mp)
                {
                    mp.ApplySenderSettings(email, pwd, readPwd);
                    AuditLogger.Log("SaveEmailButton_Click: настройки email применены к MessagesPanel");
                }

                AuditLogger.Log($"SaveEmailButton_Click: настройки email сохранены в {senderPath}");
                Xceed.Wpf.Toolkit.MessageBox.Show("Настройки email сохранены.", "Готово",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AuditLogger.Log($"SaveEmailButton_Click: ошибка при сохранении email — {ex.Message}");
                Xceed.Wpf.Toolkit.MessageBox.Show("Ошибка при сохранении настроек email:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
