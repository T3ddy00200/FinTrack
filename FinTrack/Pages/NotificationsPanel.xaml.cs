using FinTrack.Models;
using FinTrack.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace FinTrack.Pages
{
    public class AutoSendLog
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime DateSent { get; set; }
    }

    public class EmailSenderConfig
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string ReadPassword { get; set; }  // добавлен
    }


    public partial class NotificationsPanel : UserControl
    {
        private readonly string debtorFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json"
        );

        private readonly string senderFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "sender.json"
        );

        private List<Debtor> AllDebtors = new();

        public NotificationsPanel()
        {
            InitializeComponent();
            LoadDebtors();
            LoadSenderData();
            AutoSendNotifications();
        }

        private void LoadDebtors()
        {
            if (File.Exists(debtorFilePath))
            {
                var json = File.ReadAllText(debtorFilePath);
                var loaded = JsonSerializer.Deserialize<List<Debtor>>(json);
                if (loaded != null)
                {
                    AllDebtors = loaded;
                    RecipientsListBox.ItemsSource = AllDebtors;
                }
            }
        }

        private void LoadSenderData()
        {
            try
            {
                if (File.Exists(senderFilePath))
                {
                    var json = File.ReadAllText(senderFilePath);
                    var config = JsonSerializer.Deserialize<EmailSenderConfig>(json);

                    if (config != null)
                    {
                        SenderEmailBox.Text = config.Email;
                        SenderPasswordBox.Password = config.Password;
                        ReadPasswordBox.Password = config.ReadPassword;
                        return;
                    }
                }

                var defaultConfig = new EmailSenderConfig
                {
                    Email = "",
                    Password = "",
                    ReadPassword = ""
                };

                var defaultJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(senderFilePath)!);
                File.WriteAllText(senderFilePath, defaultJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при чтении или создании sender.json:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveSenderData(string email, string password, string readPassword)
        {
            var config = new EmailSenderConfig
            {
                Email = email,
                Password = password,
                ReadPassword = readPassword
            };

            Directory.CreateDirectory(Path.GetDirectoryName(senderFilePath)!);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(senderFilePath, json);
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var email = SenderEmailBox.Text.Trim();
            var password = SenderPasswordBox.Password;
            var readPassword = ReadPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(readPassword))
            {
                MessageBox.Show("Пожалуйста, заполните все поля перед сохранением.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveSenderData(email, password, readPassword);
            MessageBox.Show("Настройки успешно сохранены.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SendNotification_Click(object sender, RoutedEventArgs e)
        {
            var senderEmail = SenderEmailBox.Text.Trim();
            var senderPassword = SenderPasswordBox.Password;
            var selectedDebtors = RecipientsListBox.SelectedItems.Cast<Debtor>().ToList();
            var messageText = MessageTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(senderPassword) ||
                selectedDebtors.Count == 0 ||
                string.IsNullOrWhiteSpace(messageText))
            {
                MessageBox.Show("Заполните все поля и выберите хотя бы одного должника.");
                return;
            }

            try
            {
                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    EnableSsl = true
                };

                foreach (var debtor in selectedDebtors)
                {
                    var mail = new MailMessage
                    {
                        From = new MailAddress(senderEmail),
                        Subject = "Уведомление от FinTrack",
                        Body = messageText,
                        IsBodyHtml = false
                    };

                    mail.To.Add(debtor.Email);

                    if (!string.IsNullOrWhiteSpace(debtor.InvoiceFilePath) && File.Exists(debtor.InvoiceFilePath))
                    {
                        mail.Attachments.Add(new Attachment(debtor.InvoiceFilePath));
                    }

                    smtp.Send(mail);
                }

                MessageBox.Show("Сообщения успешно отправлены.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка отправки: " + ex.Message);
            }
        }

        private readonly string autoSendLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "autosend_log.json");

        private bool WasAlreadySentThisMonth()
        {
            if (!File.Exists(autoSendLogPath))
                return false;

            var json = File.ReadAllText(autoSendLogPath);
            var log = JsonSerializer.Deserialize<AutoSendLog>(json);
            if (log == null)
                return false;

            return log.Year == DateTime.Today.Year && log.Month == DateTime.Today.Month;
        }

        private void MarkAsSent()
        {
            var log = new AutoSendLog
            {
                Year = DateTime.Today.Year,
                Month = DateTime.Today.Month,
                DateSent = DateTime.Now
            };
            var json = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(autoSendLogPath, json);
        }

        private void AutoSendNotifications()
        {
            if (!IsAutoSendDay() || WasAlreadySentThisMonth())
                return;

            var senderEmail = SenderEmailBox.Text.Trim();
            var senderPassword = SenderPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
                return;

            try
            {
                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    EnableSsl = true
                };

                var overdueDebtors = AllDebtors
                    .Where(d => d.TotalDebt > d.Paid && d.DueDate < DateTime.Today)
                    .ToList();

                foreach (var debtor in overdueDebtors)
                {
                    var mail = new MailMessage
                    {
                        From = new MailAddress(senderEmail),
                        Subject = "Просроченная задолженность",
                        Body = $"Здравствуйте, {debtor.Name}!\nУ вас просрочен платёж на сумму {debtor.TotalDebt - debtor.Paid:0.00} ₽. Пожалуйста, погасите долг.",
                        IsBodyHtml = false
                    };

                    mail.To.Add(debtor.Email);

                    if (!string.IsNullOrWhiteSpace(debtor.InvoiceFilePath) && File.Exists(debtor.InvoiceFilePath))
                    {
                        mail.Attachments.Add(new Attachment(debtor.InvoiceFilePath));
                    }

                    smtp.Send(mail);
                }

                MarkAsSent();
                MessageBox.Show("Автоуведомления отправлены.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка автоотправки: " + ex.Message);
            }
        }

        private bool IsAutoSendDay()
        {
            DateTime today = DateTime.Today;
            DateTime scheduled = new DateTime(today.Year, today.Month, 5);

            if (scheduled.DayOfWeek == DayOfWeek.Saturday)
                scheduled = scheduled.AddDays(2);
            else if (scheduled.DayOfWeek == DayOfWeek.Sunday)
                scheduled = scheduled.AddDays(1);

            return today == scheduled;
        }
    }
}
