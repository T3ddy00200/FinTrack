using FinTrack.Models;
using System.IO;
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
using MailMessage = System.Net.Mail.MailMessage;
using MailAddress = System.Net.Mail.MailAddress;
using Task = System.Threading.Tasks.Task;
using System.Windows.Input;
using System.Collections.ObjectModel;
using FinTrack.Services;   // <- подключаем наш сервис
using FinTrack.Views;


namespace FinTrack.Controls
{

    public partial class MessagesPanel : UserControl
    {
        private readonly ChatGptService _chatGpt = new ChatGptService();

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
        private readonly ChatGptService _chat = new ChatGptService();
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
            this.IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)  // если панель стала видимой
                {
                    LoadDebtors();
                    RecipientsListBox.Items.Refresh();
                }
            };
        }
        private async void SuggestReply_Click(object sender, RoutedEventArgs e)
        {
            if (MessagesListBox.SelectedItem is EmailMessage msg)
            {
                StatusTextBlock.Text = "🤖 Generating suggestion…";

                // Получаем текущий язык UI ("ru", "en" и т.п.)
                var lang = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;

                // Генерируем уведомление по просроченным должникам на этом языке
                var notificationText = await _chatGpt.GenerateOverdueNotificationAsync(lang);

                // Отображаем его в поле ответа
                ReplyTextBox.Text = notificationText;
                StatusTextBlock.Text = "✅ Suggestion ready";
            }
            else
            {
                MessageBox.Show("Please select a message first.", "No selection",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ApplySenderSettings(string email, string sendPwd, string readPwd)
        {
            senderEmail = email;
            sendPassword = sendPwd;
            readPassword = readPwd;
        }
       private async Task LoadMessagesAsync()
        {
            AuditLogger.Log("LoadMessagesAsync: старт загрузки писем");
            ShowLoading(true);
            try
            {
                if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(readPassword))
                {
                    StatusTextBlock.Text = "⚠️ Нет данных для чтения почты.";
                    AuditLogger.Log("LoadMessagesAsync: пропущено — нет данных для чтения почты");
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

                AuditLogger.Log($"LoadMessagesAsync: успешно загружено {allMessages.Count} непрочитанных писем");
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"❌ Ошибка IMAP: {ex.Message}";
                AuditLogger.Log($"LoadMessagesAsync: ошибка — {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
                AuditLogger.Log("LoadMessagesAsync: завершение загрузки писем");
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
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "PDF файлы (*.pdf)|*.pdf" };
            if (dlg.ShowDialog() != true)
                return;

            // полный путь к выбранному файлу
            selectedPdfPath = dlg.FileName;
            // только имя файла
            var fileName = System.IO.Path.GetFileName(selectedPdfPath);

            var btn = sender as Button;
            if (btn?.Name == "NotificationChoosePdfButton")
            {
                // обновляем текст в нужном TextBlock
                NotificationPdfFileNameTextBlock.Text = fileName;
            }
            else
            {
                // если у вас есть вторая кнопка для ответа, например ReplyChoosePdfButton
                ReplyPdfFileNameTextBlock.Text = fileName;
            }
        }
        // Вставляет {Name} в текст ручного уведомления
        private void InsertNameTag_Manual_Click(object sender, RoutedEventArgs e)
        {
            MessageTextBox.Text += " {Name}";
            MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
            MessageTextBox.Focus();
        }
        // Вставляет {Debt} в текст ручного уведомления
        private void InsertDebtTag_Manual_Click(object sender, RoutedEventArgs e)
        {
            MessageTextBox.Text += " {Debt}";
            MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
            MessageTextBox.Focus();
        }
        private async void ReplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessagesListBox.SelectedItem is not EmailMessage selected || string.IsNullOrWhiteSpace(ReplyTextBox.Text))
            {
                MessageBox.Show("Выберите письмо и введите текст ответа.");
                return;
            }
            AuditLogger.Log($"ReplyButton_Click: попытка ответа на письмо от {selected.From} (UID={selected.Uid})");
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
                AuditLogger.Log($"ReplyButton_Click: ответ отправлен на {selected.From}, тема «{selected.Subject}»");
                MessageBox.Show("Ответ отправлен.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка отправки: " + ex.Message);
                AuditLogger.Log($"ReplyButton_Click: ошибка отправки — {ex.Message}");
            }
            finally
            {
                SendingOverlay.Visibility = Visibility.Collapsed;
            }
        }
        private void SendNotification_Click(object sender, RoutedEventArgs e)
        {
            AuditLogger.Log("SendNotification_Click: ручная рассылка уведомлений — начало");

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(sendPassword))
            {
                MessageBox.Show("Настройки отправителя не заполнены.");
                AuditLogger.Log("SendNotification_Click: отменено — нет настроек отправителя");
                return;
            }

            // Тема письма
            string subject = !string.IsNullOrWhiteSpace(SubjectTextBox.Text)
                ? SubjectTextBox.Text.Trim()
                : "Уведомление от FinTrack";

            // Шаблон текста
            string template = MessageTextBox.Text.Trim();
            var recipients = RecipientsListBox.SelectedItems.Cast<Debtor>().ToList();
            if (string.IsNullOrWhiteSpace(template) || recipients.Count == 0)
            {
                MessageBox.Show("Введите тему, текст и выберите получателей.");
                AuditLogger.Log("SendNotification_Click: отменено — нет текста или нет получателей");
                return;
            }

            try
            {
                using var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, sendPassword),
                    EnableSsl = true
                };

                foreach (var debtor in recipients)
                {
                    // Подготовка тела
                    decimal debtAmount = debtor.TotalDebt - debtor.Paid;
                    string body = template
                        .Replace("{Name}", debtor.ContactName)
                        .Replace("{Debt}", debtAmount.ToString("0.00"));

                    var mail = new MailMessage
                    {
                        From = new MailAddress(senderEmail),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = false
                    };
                    mail.To.Add(debtor.Email);

                    // Выбираем вложение: ручное (selectedPdfPath) имеет приоритет,
                    // иначе встроенное debtor.InvoiceFilePath
                    string attachPath = null;
                    if (!string.IsNullOrEmpty(selectedPdfPath) && File.Exists(selectedPdfPath))
                    {
                        attachPath = selectedPdfPath;
                    }
                    else if (!string.IsNullOrEmpty(debtor.InvoiceFilePath) && File.Exists(debtor.InvoiceFilePath))
                    {
                        attachPath = debtor.InvoiceFilePath;
                    }
                    else
                    {
                        // Нет ни ручного, ни встроенного инвойса
                        var result = MessageBox.Show(
                            $"У должника «{debtor.Name}» нет инвойса.\n" +
                            "Отправить без вложения?",
                            "Внимание",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No)
                        {
                            AuditLogger.Log(
                                $"SendNotification_Click: отменено отправление для {debtor.Name} — нет инвойса");
                            return; // Прерываем всю рассылку
                        }
                    }

                    if (attachPath != null)
                        mail.Attachments.Add(new Attachment(attachPath));

                    smtp.Send(mail);
                    Thread.Sleep(3000);
                    AuditLogger.Log($"SendNotification_Click: письмо отправлено {debtor.Email} (инвойс: {attachPath ?? "нет"})");
                }

                AuditLogger.Log($"SendNotification_Click: уведомления успешно отправлены {recipients.Count} получателям");
                MessageBox.Show("Уведомления отправлены.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка отправки: " + ex.Message);
                AuditLogger.Log($"SendNotification_Click: ошибка рассылки — {ex.Message}");
            }
        }
        private void AutoSendNotifications()
        {
            AuditLogger.Log("AutoSendNotifications: проверка автосенд-дня");
            if (!IsAutoSendDay() || WasAlreadySentThisMonth())
            {
                AuditLogger.Log("AutoSendNotifications: не день автосенд или уже отправлено в этом месяце");
                return;
            }
            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(sendPassword))
            {
                AuditLogger.Log("AutoSendNotifications: отменено — нет настроек отправителя");
                return;
            }

            try
            {       
                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, sendPassword),
                    EnableSsl = true
                };

                var overdue = AllDebtors.Where(d => d.TotalDebt > d.Paid && d.DueDate < DateTime.Today).ToList();
                AuditLogger.Log($"AutoSendNotifications: найдены {overdue.Count} просроченных должников");

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

                AuditLogger.Log("AutoSendNotifications: автосообщения успешно отправлены");
                MessageBox.Show("Автоуведомления отправлены.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка автоотправки: " + ex.Message);
                AuditLogger.Log($"AutoSendNotifications: ошибка — {ex.Message}");
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
        private void RecipientsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (RecipientsListBox.SelectedItem is Debtor debtor && !debtor.HasInvoice)
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "PDF файлы (*.pdf)|*.pdf"
                };
                if (dlg.ShowDialog() == true)
                {
                    debtor.InvoiceFilePath = dlg.FileName;

                    // Сохраняем обновлённый список должников
                    var json = JsonSerializer.Serialize(AllDebtors, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(debtorFilePath, json);

                    // Обновляем отображение списка
                    RecipientsListBox.Items.Refresh();
                }
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
