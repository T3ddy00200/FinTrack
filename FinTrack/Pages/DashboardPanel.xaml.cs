using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using FinTrack.Views;    // Debtor
using FinTrack.Pages;    // AutoSendSettings, LocalizationManager
using FinTrack.Models;   // Debtor
using FinTrack.Services;    // ваш сервис работы с почтой
using MailKit;              // @MailKit
using MailKit.Search;
using MailKit.Security;
using MailKit.Net.Imap;
using MimeKit;
using MailMessage = System.Net.Mail.MailMessage;
using MailAddress = System.Net.Mail.MailAddress;
using Task = System.Threading.Tasks.Task;

namespace FinTrack.Controls
{

    public partial class DashboardPanel : UserControl, INotifyPropertyChanged
    {
        private readonly string senderConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FinTrack", "sender.json");
        // Путь к файлу должников
        private readonly string debtorFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json");

        private readonly string autoConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "autosend_config.json");

        private List<Debtor> _debtors = new List<Debtor>();

        // --------------- Свойства для биндинга ---------------
        public decimal TotalDebt { get; private set; }
        public int OpenInvoices { get; private set; }

        public int UnpaidCount { get; private set; }
        public int PartialCount { get; private set; }
        public int PaidCount { get; private set; }

        private static DateTime? _lastOverdueNotificationDate = null;


        // Новые срочные договора
        public int UrgentCount { get; private set; }
        public ObservableCollection<string> UrgentNames { get; private set; }
            = new ObservableCollection<string>();

        public SeriesCollection PaymentStatusSeries { get; private set; }

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            private set
            {
                _unreadCount = value;
                OnPropertyChanged(nameof(UnreadCount));
            }
        }


        public DateTime NextAutoSend
        {
            get
            {
                // ... (ваша прежняя реализация) ...
                try
                {
                    if (!File.Exists(autoConfigPath)) return DateTime.MinValue;
                    var json = File.ReadAllText(autoConfigPath);
                    var cfg = JsonSerializer.Deserialize<AutoSendSettings>(json);
                    if (cfg == null) return DateTime.MinValue;

                    if (!TimeSpan.TryParse(cfg.Time, out var scheduledTime))
                        scheduledTime = TimeSpan.FromHours(9);

                    var now = DateTime.Now;
                    int day = Math.Min(cfg.ScheduledDay, DateTime.DaysInMonth(now.Year, now.Month));
                    var next = new DateTime(now.Year, now.Month, day).Add(scheduledTime);

                    if (next <= now)
                    {
                        var nm = now.AddMonths(1);
                        int dayNext = Math.Min(cfg.ScheduledDay, DateTime.DaysInMonth(nm.Year, nm.Month));
                        next = new DateTime(nm.Year, nm.Month, dayNext).Add(scheduledTime);
                    }
                    return next;
                }
                catch
                {
                    return DateTime.MinValue;
                }
            }
        }

        // --------------- Конструктор ---------------
        // 1) Конструктор: заменяем синхронный вызов на асинхронный
        public DashboardPanel()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += async (_, __) => await InitializeAsync();
        }

        // 2) Новый метод инициализации
        private async Task InitializeAsync()
        {
            LoadDebtorsAndMetrics();
            var debtorEmails = GetDebtorEmails();
            await LoadUnreadCountAsync(debtorEmails);
        }


        // 3) Вынесенная логика чтения должников и расчёта метрик (без сети)
        private void LoadDebtorsAndMetrics()
        {
            try
            {
                if (File.Exists(debtorFilePath))
                {
                    var json = File.ReadAllText(debtorFilePath);
                    _debtors = JsonSerializer.Deserialize<List<Debtor>>(json) ?? new List<Debtor>();
                }
                else
                {
                    _debtors = new List<Debtor>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка чтения должников: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _debtors = new List<Debtor>();
            }

            TotalDebt = _debtors.Sum(d => d.TotalDebt - d.Paid);
            OpenInvoices = _debtors.Count(d => d.TotalDebt > d.Paid);

            UnpaidCount = _debtors.Count(d => d.PaymentStatus == "Не оплачено");
            PartialCount = _debtors.Count(d => d.PaymentStatus == "Частично оплачено");
            PaidCount = _debtors.Count(d => d.PaymentStatus == "Оплачено");

            PaymentStatusSeries = new SeriesCollection
    {
        new PieSeries { Title = "Paid",    Values = new ChartValues<int> { PaidCount },    DataLabels = true },
        new PieSeries { Title = "Unpaid",  Values = new ChartValues<int> { UnpaidCount },  DataLabels = true },
        new PieSeries { Title = "Partial", Values = new ChartValues<int> { PartialCount }, DataLabels = true }
    };

            var today = DateTime.Today;

            // Найдём всех, кто не оплатил и у кого просрочка > 10 дней
            var overdue = _debtors
                .Where(d => d.PaymentStatus != "Оплачено" && (today - d.DueDate.Date).Days > 10)
                .ToList();

            //if (overdue.Count > 0 && _lastOverdueNotificationDate != today)
            //{
            //    _lastOverdueNotificationDate = today;

            //    int minOverdueDays = overdue.Min(d => (today - d.DueDate.Date).Days);

            //    MessageBox.Show(
            //        $"⚠ Обнаружено {overdue.Count} клиентов с просрочкой более 10 дней.\n" +
            //        $"Минимальное количество дней просрочки: {minOverdueDays}",
            //        "Просроченные задолженности",
            //        MessageBoxButton.OK,
            //        MessageBoxImage.Warning);
            //}

            var urgentList = _debtors
                .Where(d =>
                {
                    var diff = (d.DueDate.Date - today).Days;
                    return diff >= 0 && diff <= 1;
                })
                .Select(d => d.Name)
                .ToList();

            UrgentCount = urgentList.Count;
            UrgentNames.Clear();
            foreach (var name in urgentList)
                UrgentNames.Add(name);

            // Обновляем биндинги
            OnPropertyChanged(nameof(TotalDebt));
            OnPropertyChanged(nameof(OpenInvoices));
            OnPropertyChanged(nameof(UnpaidCount));
            OnPropertyChanged(nameof(PartialCount));
            OnPropertyChanged(nameof(PaidCount));
            OnPropertyChanged(nameof(PaymentStatusSeries));
            OnPropertyChanged(nameof(UrgentCount));
            OnPropertyChanged(nameof(UrgentNames));
            OnPropertyChanged(nameof(NextAutoSend));
        }

        // 5) Асинхронная загрузка непрочитанных писем в фоне
        private async Task LoadUnreadCountAsync(HashSet<string> debtorEmails)
        {
            try
            {
                var count = await Task.Run(() =>
                {
                    if (!File.Exists(senderConfigPath)) return 0;
                    var cfg = JsonSerializer.Deserialize<EmailSenderConfig>(
                        File.ReadAllText(senderConfigPath));

                    if (cfg == null ||
                        string.IsNullOrWhiteSpace(cfg.Email) ||
                        string.IsNullOrWhiteSpace(cfg.ReadPassword))
                        return 0;

                    using var client = new ImapClient();
                    client.Connect("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
                    client.Authenticate(cfg.Email, cfg.ReadPassword);
                    client.Inbox.Open(FolderAccess.ReadOnly);

                    var uids = client.Inbox.Search(SearchQuery.NotSeen);
                    if (uids.Count == 0) return 0;

                    var summaries = client.Inbox.Fetch(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);

                    var seenUids = new HashSet<UniqueId>();
                    var seenFrom = new HashSet<string>();

                    foreach (var s in summaries)
                    {
                        if (!s.UniqueId.IsValid || s.Envelope?.From == null)
                            continue;

                        var from = s.Envelope.From.Mailboxes.FirstOrDefault()?.Address?.ToLower();
                        if (string.IsNullOrWhiteSpace(from)) continue;

                        // Только если в базе и ещё не считали от него
                        if (debtorEmails.Contains(from) && !seenUids.Contains(s.UniqueId))
                        {
                            seenUids.Add(s.UniqueId);
                        }
                    }

                    client.Disconnect(true);
                    return seenUids.Count;
                });

                UnreadCount = count;
            }
            catch (Exception ex)
            {
                Console.WriteLine("‼ LoadUnreadCountAsync Error: " + ex.Message);
                UnreadCount = 0;
            }
        }


        // 4) Получение списка email-ов должников
        private HashSet<string> GetDebtorEmails()
        {
            if (!File.Exists(debtorFilePath))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(debtorFilePath);
            var debtors = JsonSerializer.Deserialize<List<Debtor>>(json) ?? new List<Debtor>();

            return new HashSet<string>(
                debtors
                  .Where(d => !string.IsNullOrWhiteSpace(d.Email))
                  .Select(d => d.Email.Trim().ToLower()),
                StringComparer.OrdinalIgnoreCase);
        }

        // --------------- Загрузка и вычисления ---------------
        private void LoadDashboardData()
        {
            // 1) Читает файл должников
            try
            {
                if (File.Exists(debtorFilePath))
                {
                    var json = File.ReadAllText(debtorFilePath);
                    _debtors = JsonSerializer.Deserialize<List<Debtor>>(json)
                              ?? new List<Debtor>();
                }
                else _debtors = new List<Debtor>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка чтения должников: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _debtors = new List<Debtor>();
            }

            // 2) Основные метрики
            TotalDebt = _debtors.Sum(d => d.TotalDebt - d.Paid);
            OpenInvoices = _debtors.Count(d => d.TotalDebt > d.Paid);

            UnpaidCount = _debtors.Count(d => d.PaymentStatus == "Не оплачено");
            PartialCount = _debtors.Count(d => d.PaymentStatus == "Частично оплачено");
            PaidCount = _debtors.Count(d => d.PaymentStatus == "Оплачено");

            // 3) PieChart
            PaymentStatusSeries = new SeriesCollection
            {
                new PieSeries { Title="Оплачено",    Values=new ChartValues<int>{PaidCount},    DataLabels=true },
                new PieSeries { Title="Не оплачено", Values=new ChartValues<int>{UnpaidCount},  DataLabels=true },
                new PieSeries { Title="Частично",    Values=new ChartValues<int>{PartialCount}, DataLabels=true }
            };

            // 4) Срочные договора: сегодня или завтра
            var today = DateTime.Today;
            var urgentList = _debtors
                .Where(d =>
                {
                    var diff = (d.DueDate.Date - today).Days;
                    return diff >= 0 && diff <= 1;
                })
                .Select(d => d.Name)
                .ToList();

            UrgentCount = urgentList.Count;
            UrgentNames.Clear();
            foreach (var name in urgentList)
                UrgentNames.Add(name);

            // 5) Оповестить WPF об обновлении
            OnPropertyChanged(nameof(TotalDebt));
            OnPropertyChanged(nameof(OpenInvoices));
            OnPropertyChanged(nameof(UnpaidCount));
            OnPropertyChanged(nameof(PartialCount));
            OnPropertyChanged(nameof(PaidCount));
            OnPropertyChanged(nameof(PaymentStatusSeries));
            OnPropertyChanged(nameof(NextAutoSend));
            OnPropertyChanged(nameof(UrgentCount));
            OnPropertyChanged(nameof(UrgentNames));
        }

      

        // --------------- Локализация ---------------
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //LocalizationManager.LocalizeUI(this);
        }

        // --------------- INotifyPropertyChanged ---------------
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
