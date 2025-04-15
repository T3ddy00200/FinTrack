using FinTrack.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MailKit.Net.Imap;
using MailKit;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using FinTrack.Views;

namespace FinTrack.Controls
{
    public partial class MessagesPanel : UserControl
    {
        private readonly string senderFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "sender.json");

        private readonly string debtorFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "debtors.json");

        private readonly string autoSendLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "autosend_log.json");

        private List<Debtor> AllDebtors = new();
        private List<string> knownEmails = new();
        private List<EmailMessage> allMessages = new();

        private string senderEmail = string.Empty;
        private string sendPassword = string.Empty;
        private string readPassword = string.Empty;
        private string selectedPdfPath = string.Empty;

        private DispatcherTimer refreshTimer;

        public MessagesPanel()
        {
            InitializeComponent();

            Loaded += async (_, _) =>
            {
                LoadDebtors();
                LoadSenderData();
                await LoadMessagesAsync();
                AutoSendNotifications();

                refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
                refreshTimer.Tick += async (_, _) =>
                {
                    StatusTextBlock.Text = "🔄 Автообновление...";
                    await LoadMessagesAsync();
                };
                refreshTimer.Start();
            };
        }

        // === Входящие письма ===
        private async Task LoadMessagesAsync()
        {
            ShowLoading(true);

            try
            {
                if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(readPassword))
                {
                    StatusTextBlock.Text = "⚠️ Нет данных для чтения почты.";
                    return;
                }

                using var client = new ImapClient();
                await client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
                await client.AuthenticateAsync(senderEmail, readPassword);
                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadWrite);

                var sinceDate = DateTime.UtcNow.AddDays(-7);
                var uids = await inbox.SearchAsync(SearchQuery.DeliveredAfter(sinceDate));

                allMessages.Clear();

                foreach (var uid in uids.Reverse())
                {
                    var msg = await inbox.GetMessageAsync(uid);
                    var from = msg.From.Mailboxes.FirstOrDefault()?.Address.ToLower();

                    if (!string.IsNullOrWhiteSpace(from) && knownEmails.Contains(from))
                    {
                        var text = !string.IsNullOrEmpty(msg.TextBody) ? msg.TextBody :
                                   !string.IsNullOrEmpty(msg.HtmlBody) ? msg.HtmlBody : "(пусто)";

                        allMessages.Add(new EmailMessage
                        {
                            From = from,
                            Subject = msg.Subject ?? "(без темы)",
                            FullBody = text,
                            Preview = text.Length > 100 ? text[..100] : text,
                            Uid = uid
                        });
                    }
                }

                MessagesListBox.ItemsSource = allMessages;
                StatusTextBlock.Text = $"✅ Найдено писем: {allMessages.Count}";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"❌ Ошибка IMAP: {ex.Message}";
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void MessagesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void ChoosePdf_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "PDF файлы (*.pdf)|*.pdf" };
            if (dialog.ShowDialog() == true)
            {
                selectedPdfPath = dialog.FileName;

                if (sender is Button btn)
                {
                    if (btn.DataContext?.ToString()?.Contains("Notification") == true ||
                        btn.Name == "NotificationChoosePdfButton")
                    {
                        NotificationPdfFileNameTextBlock.Text = Path.GetFileName(selectedPdfPath);
                    }
                    else
                    {
                        ReplyPdfFileNameTextBlock.Text = Path.GetFileName(selectedPdfPath);
                    }
                }
            }
        }


        private async void ReplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessagesListBox.SelectedItem is not EmailMessage selected || string.IsNullOrWhiteSpace(ReplyTextBox.Text))
            {
                MessageBox.Show("Выберите письмо и введите текст ответа.");
                return;
            }

            try
            {
                var reply = new MimeMessage();
                reply.From.Add(new MailboxAddress("", senderEmail));
                reply.To.Add(new MailboxAddress("", selected.From));
                reply.Subject = "RE: " + selected.Subject;

                var bodyText = new TextPart("plain") { Text = ReplyTextBox.Text };

                if (!string.IsNullOrEmpty(selectedPdfPath) && File.Exists(selectedPdfPath))
                {
                    var multipart = new Multipart("mixed");
                    multipart.Add(bodyText);
                    multipart.Add(new MimePart("application", "pdf")
                    {
                        Content = new MimeContent(File.OpenRead(selectedPdfPath)),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = Path.GetFileName(selectedPdfPath)
                    });
                    reply.Body = multipart;
                }
                else
                {
                    reply.Body = bodyText;
                }

                using var smtp = new MailKit.Net.Smtp.SmtpClient();
                await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(senderEmail, sendPassword);
                await smtp.SendAsync(reply);
                await smtp.DisconnectAsync(true);

                using var imap = new ImapClient();
                await imap.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
                await imap.AuthenticateAsync(senderEmail, readPassword);
                await imap.Inbox.OpenAsync(FolderAccess.ReadWrite);
                await imap.Inbox.AddFlagsAsync(selected.Uid, MessageFlags.Seen, true);
                await imap.DisconnectAsync(true);

                MessageBox.Show("Ответ отправлен.");
                ReplyTextBox.Clear();
                ReplyPdfFileNameTextBlock.Text = "(файл не выбран)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка отправки: " + ex.Message);
            }
        }

        // === Рассылка и автоуведомления ===

        private void SendNotification_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(sendPassword))
            {
                MessageBox.Show("Настройки отправителя не заполнены.");
                return;
            }

            var text = MessageTextBox.Text.Trim();
            var selected = RecipientsListBox.SelectedItems.Cast<Debtor>().ToList();

            if (string.IsNullOrWhiteSpace(text) || selected.Count == 0)
            {
                MessageBox.Show("Введите текст и выберите получателей.");
                return;
            }

            try
            {
                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, sendPassword),
                    EnableSsl = true
                };

                foreach (var debtor in selected)
                {
                    var mail = new MailMessage
                    {
                        From = new MailAddress(senderEmail),
                        Subject = "Уведомление от FinTrack",
                        Body = text,
                        IsBodyHtml = false
                    };
                    mail.To.Add(debtor.Email);

                    if (!string.IsNullOrEmpty(debtor.InvoiceFilePath) && File.Exists(debtor.InvoiceFilePath))
                        mail.Attachments.Add(new Attachment(debtor.InvoiceFilePath));

                    smtp.Send(mail);
                }

                MessageBox.Show("Уведомления отправлены.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка отправки: " + ex.Message);
            }
        }

        private void AutoSendNotifications()
        {
            if (!IsAutoSendDay() || WasAlreadySentThisMonth()) return;

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(sendPassword)) return;

            try
            {
                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, sendPassword),
                    EnableSsl = true
                };

                var overdue = AllDebtors.Where(d => d.TotalDebt > d.Paid && d.DueDate < DateTime.Today).ToList();

                foreach (var debtor in overdue)
                {
                    var mail = new MailMessage
                    {
                        From = new MailAddress(senderEmail),
                        Subject = "Просроченная задолженность",
                        Body = $"Здравствуйте, {debtor.Name}, у вас задолженность {debtor.TotalDebt - debtor.Paid:0.00} ₽"
                    };
                    mail.To.Add(debtor.Email);

                    if (!string.IsNullOrEmpty(debtor.InvoiceFilePath) && File.Exists(debtor.InvoiceFilePath))
                        mail.Attachments.Add(new Attachment(debtor.InvoiceFilePath));

                    smtp.Send(mail);
                }

                File.WriteAllText(autoSendLogPath, JsonSerializer.Serialize(new AutoSendLog
                {
                    Year = DateTime.Today.Year,
                    Month = DateTime.Today.Month,
                    DateSent = DateTime.Now
                }, new JsonSerializerOptions { WriteIndented = true }));

                MessageBox.Show("Автоуведомления отправлены.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка автоотправки: " + ex.Message);
            }
        }

        private bool WasAlreadySentThisMonth()
        {
            if (!File.Exists(autoSendLogPath)) return false;
            var log = JsonSerializer.Deserialize<AutoSendLog>(File.ReadAllText(autoSendLogPath));
            return log?.Year == DateTime.Today.Year && log?.Month == DateTime.Today.Month;
        }

        private bool IsAutoSendDay()
        {
            var today = DateTime.Today;
            var scheduled = new DateTime(today.Year, today.Month, 5);
            if (scheduled.DayOfWeek == DayOfWeek.Saturday) scheduled = scheduled.AddDays(2);
            if (scheduled.DayOfWeek == DayOfWeek.Sunday) scheduled = scheduled.AddDays(1);
            return today == scheduled;
        }

        // === Загрузка данных ===

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
                    knownEmails = loaded
                        .Where(d => !string.IsNullOrWhiteSpace(d.Email))
                        .Select(d => d.Email.Trim().ToLower())
                        .Distinct()
                        .ToList();
                }
            }
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

            var config = new EmailSenderConfig
            {
                Email = email,
                Password = password,
                ReadPassword = readPassword
            };

            Directory.CreateDirectory(Path.GetDirectoryName(senderFilePath)!);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(senderFilePath, json);

            senderEmail = email;
            sendPassword = password;
            readPassword = readPassword;

            MessageBox.Show("Настройки успешно сохранены.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadSenderData()
        {
            if (!File.Exists(senderFilePath)) return;

            try
            {
                var json = File.ReadAllText(senderFilePath);
                var config = JsonSerializer.Deserialize<EmailSenderConfig>(json);
                if (config != null)
                {
                    senderEmail = config.Email;
                    sendPassword = config.Password;
                    readPassword = config.ReadPassword;
                    SenderEmailBox.Text = senderEmail;
                    SenderPasswordBox.Password = sendPassword;
                    ReadPasswordBox.Password = readPassword;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка чтения sender.json: " + ex.Message);
            }
        }

        private void ShowLoading(bool show)
        {
            LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            this.IsEnabled = !show;
        }
    }

    public class EmailMessage
    {
        public string From { get; set; }
        public string Subject { get; set; }
        public string Preview { get; set; }
        public string FullBody { get; set; }
        public UniqueId Uid { get; set; }
    }

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
        public string ReadPassword { get; set; }
    }
}
