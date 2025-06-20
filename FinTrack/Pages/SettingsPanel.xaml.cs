﻿// SettingsPanel.xaml.cs
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit;            // для TimePicker и MessageBox
using FinTrack.Controls;
using FinTrack.Models;
using FinTrack.Services;
using MessageBox = System.Windows.MessageBox;
using System.Diagnostics;

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

        private async void TestAutoSendButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await AutoNotifier.TryAutoSend();
                Xceed.Wpf.Toolkit.MessageBox.Show("Проверка авторассылки завершена.", "Готово",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("Ошибка при запуске авторассылки: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //LocalizationManager.LocalizeUI(this);
        }

        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string cultureCode)
            {
               // LocalizationManager.SetCulture(cultureCode);
               // LocalizationManager.LocalizeUI(Application.Current.MainWindow);
                SaveAppLanguage(cultureCode);
            }
        }

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
            // Язык
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var appCfg = JsonSerializer.Deserialize<AppSettings>(json);
                    if (appCfg != null)
                    {
                        //LocalizationManager.SetCulture(appCfg.Language);
                        //LocalizationManager.LocalizeUI(Application.Current.MainWindow);
                    }
                    if (appCfg != null)
                    {
                        SystemPromptTextBox.Text = appCfg.SystemPrompt ?? "";
                    }

                }
                catch { /* лог */ }
            }

            // Авторассылка
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

                        // Дата
                        var today = DateTime.Today;
                        int day = Math.Min(autoCfg.ScheduledDay,
                                            DateTime.DaysInMonth(today.Year, today.Month));
                        AutoNotificationDatePicker.SelectedDate =
                            new DateTime(today.Year, today.Month, day);

                        // Время
                        if (TimeSpan.TryParse(autoCfg.Time, out var ts))
                            AutoNotificationTimePicker.Value = DateTime.Today + ts;
                    }
                }
                catch { /* лог */ }
            }

            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var appCfg = JsonSerializer.Deserialize<AppSettings>(json);
                    if (appCfg != null)
                    {
                        SystemPromptTextBox.Text = appCfg.SystemPrompt ?? "";
                        AIApiKeyBox.Password = appCfg.AIApiKey ?? "";
                        MaxTokensBox.Text = appCfg.MaxTokens.ToString();
                        TemperatureBox.Text = appCfg.Temperature.ToString("0.0");
                    }
                }
                catch { /* лог */ }
            }


        }


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

            Xceed.Wpf.Toolkit.MessageBox.Show("Настройки авторассылки сохранены.", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenImapSettings_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://mail.google.com/mail/u/0/#settings/fwdandpop",
                UseShellExecute = true
            });
        }

        private void OpenSecuritySettings_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://myaccount.google.com/security",
                UseShellExecute = true
            });
        }

        private void OpenAppPasswords_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "https://myaccount.google.com/apppasswords",
                UseShellExecute = true
            });
        }

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

            // Применяем в MessagesPanel, если он загружен
            if (Application.Current.MainWindow is MainWindow main
             && main.FindName("MainContentPanel") is ContentControl ctrl
             && ctrl.Content is MessagesPanel mp)
            {
                mp.ApplySenderSettings(email, pwd, readPwd);
            }

            Xceed.Wpf.Toolkit.MessageBox.Show("Настройки email сохранены.", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void SaveSystemPrompt_Click(object sender, RoutedEventArgs e)
        {
            string prompt = SystemPromptTextBox.Text.Trim();
            string apiKey = AIApiKeyBox.Password.Trim();
            int.TryParse(MaxTokensBox.Text.Trim(), out int tokens);
            double.TryParse(TemperatureBox.Text.Trim(), out double temperature);

            if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("Введите системный промпт.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var appCfg = new AppSettings
            {
                Language = "he",
                SystemPrompt = prompt,
                AIApiKey = apiKey,
                MaxTokens = tokens <= 0 ? 1024 : tokens,
                Temperature = temperature <= 0 ? 1.0 : temperature
            };

            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(configPath, JsonSerializer.Serialize(appCfg, new JsonSerializerOptions { WriteIndented = true }));

            MessageBox.Show("Настройки AI сохранены. Перезапустите приложение для применения.", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
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

    public class AutoSendSettings
    {
        public bool Enabled { get; set; }
        public int ScheduledDay { get; set; }
        public string Time { get; set; }
        public string SubjectTemplate { get; set; }
        public string BodyTemplate { get; set; }
    }

}
