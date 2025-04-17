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
using FinTrack.Pages;
using FinTrack.Views;
using Microsoft.Office.Interop.Word;
using MailMessage = System.Net.Mail.MailMessage;
using MailAddress = System.Net.Mail.MailAddress;
using Task = System.Threading.Tasks.Task;
using System.Windows.Input;
using System.Collections.ObjectModel;


namespace FinTrack.Controls
{
    public partial class MessagesPanel : UserControl
    {
        private ObservableCollection<EmailMessage> allMessages = new();
        private readonly string debtorFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "debtors.json");

        private readonly string autoSendLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "autosend_log.json");

        private readonly string senderFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "sender.json");

        private readonly string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "autosend_log.json");


        private List<Debtor> AllDebtors = new();
        private List<string> knownEmails = new();

        private string senderEmail = string.Empty;
        private string sendPassword = string.Empty;
        private string readPassword = string.Empty;
        private string selectedPdfPath = string.Empty;
        private bool _isInitialized = false;


        private DispatcherTimer refreshTimer;

        public MessagesPanel()
        {
            InitializeComponent();
            Loaded += async (_, _) =>
            {
                if (_isInitialized) return;
                _isInitialized = true;

                LoadSenderData();
                LoadDebtors();
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


        public void ApplySenderSettings(string email, string sendPwd, string readPwd)
        {
            senderEmail = email;
            sendPassword = sendPwd;
            readPassword = readPwd;
        }

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
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                var sinceDate = DateTime.UtcNow.AddDays(-7);
                var uids = await inbox.SearchAsync(SearchQuery.DeliveredAfter(sinceDate));

                // Загружаем все summary с флагами
                var allSummaries = await inbox.FetchAsync(uids,
                    MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.Flags);

                // Фильтруем только НЕпрочитанные
                var unreadSummaries = allSummaries
                    .Where(s => !s.Flags.HasValue || !s.Flags.Value.HasFlag(MessageFlags.Seen))
                    .ToList();

                // Очищаем список перед загрузкой
                System.Windows.Application.Current.Dispatcher.Invoke(() => allMessages.Clear());

                foreach (var summary in unreadSummaries.AsEnumerable().Reverse())
                {
                    var from = summary.Envelope?.From?.Mailboxes.FirstOrDefault()?.Address.ToLower();
                    if (!string.IsNullOrWhiteSpace(from) && knownEmails.Contains(from))
                    {
                        var full = await inbox.GetMessageAsync(summary.UniqueId);

                        var text = !string.IsNullOrEmpty(full.TextBody) ? full.TextBody :
                                   !string.IsNullOrEmpty(full.HtmlBody) ? full.HtmlBody : "(пусто)";

                        var message = new EmailMessage
                        {
                            From = from,
                            Subject = full.Subject ?? "(без темы)",
                            FullBody = text,
                            Preview = text.Length > 100 ? text[..100] : text,
                            Uid = summary.UniqueId
                        };

                        System.Windows.Application.Current.Dispatcher.Invoke(() => allMessages.Add(message));
                    }
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessagesListBox.ItemsSource = null;
                    MessagesListBox.ItemsSource = allMessages;
                    StatusTextBlock.Text = $"✅ Найдено непрочитанных: {allMessages.Count}";
                });
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


        private void MessagesListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };

            var parent = ((Control)sender).Parent as UIElement;
            parent?.RaiseEvent(eventArg);
        }


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

            SendingOverlay.Visibility = Visibility.Visible;

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

                // Отправка ответа
                using var smtp = new MailKit.Net.Smtp.SmtpClient();
                await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(senderEmail, sendPassword);
                await smtp.SendAsync(reply);
                await smtp.DisconnectAsync(true);

                // Пометка как прочитан и удаление из списка
                using var imap = new ImapClient();
                await imap.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
                await imap.AuthenticateAsync(senderEmail, readPassword);
                await imap.Inbox.OpenAsync(FolderAccess.ReadWrite);
                await imap.Inbox.AddFlagsAsync(selected.Uid, MessageFlags.Seen, true);
                await imap.DisconnectAsync(true);

                // Удалить из списка UI
                allMessages.Remove(selected);

                MessageBox.Show("Ответ отправлен.");
                ReplyTextBox.Clear();
                ReplyPdfFileNameTextBlock.Text = "(файл не выбран)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка отправки: " + ex.Message);
            }
            finally
            {
                SendingOverlay.Visibility = Visibility.Collapsed;
            }
        }



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

                var log = new AutoSendLog
                {
                    Year = DateTime.Today.Year,
                    Month = DateTime.Today.Month,
                    DateSent = DateTime.Now
                };

                File.WriteAllText(logPath, JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true }));


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

        public async Task LoadMessagesIfConfiguredAsync()
        {
            if (!string.IsNullOrWhiteSpace(senderEmail) && !string.IsNullOrWhiteSpace(readPassword))
            {
                await LoadMessagesAsync();
            }
            else
            {
                StatusTextBlock.Text = "📭 Для загрузки писем укажите почту и пароли в настройках.";
            }
        }

        private void LoadSenderData()
        {
            if (!File.Exists(senderFilePath)) return;

            try
            {
                var json = File.ReadAllText(senderFilePath);
                var config = JsonSerializer.Deserialize<FinTrack.Models.EmailSenderConfig>(json);
                if (config != null)
                {
                    senderEmail = config.Email;
                    sendPassword = config.Password;
                    readPassword = config.ReadPassword;
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "⚠️ Ошибка чтения sender.json: " + ex.Message;
            }
        }


        private void ShowLoading(bool show)
        {
            LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            this.IsEnabled = !show;
        }
    }

    public class AutoSendLog
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime DateSent { get; set; }
    }
}
