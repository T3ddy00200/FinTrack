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
                else _debtors = new List<Debtor>();
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
        new PieSeries { Title="Оплачено",    Values=new ChartValues<int>{PaidCount},    DataLabels=true },
        new PieSeries { Title="Не оплачено", Values=new ChartValues<int>{UnpaidCount},  DataLabels=true },
        new PieSeries { Title="Частично",    Values=new ChartValues<int>{PartialCount}, DataLabels=true }
    };

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
                    var cfg = JsonSerializer.Deserialize<EmailSenderConfig>(
                        File.ReadAllText(senderConfigPath));
                    if (cfg == null
                     || string.IsNullOrWhiteSpace(cfg.Email)
                     || string.IsNullOrWhiteSpace(cfg.ReadPassword))
                        return 0;

                    using var client = new ImapClient();
                    client.Connect("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
                    client.Authenticate(cfg.Email, cfg.ReadPassword);
                    client.Inbox.Open(FolderAccess.ReadOnly);

                    var uids = client.Inbox.Search(SearchQuery.NotSeen);
                    if (uids.Count == 0) return 0;

                    var summaries = client.Inbox.Fetch(uids, MessageSummaryItems.Envelope);
                    var localCount = summaries.Count(s =>
                        s.Envelope?.From?.Mailboxes
                         .Any(mb => debtorEmails.Contains(mb.Address.ToLower())) == true);

                    client.Disconnect(true);
                    return localCount;
                });

                UnreadCount = count;
            }
            catch
            {
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
            var debtorEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(debtorFilePath))
            {
                var json = File.ReadAllText(debtorFilePath);
                var debtors = JsonSerializer.Deserialize<List<Debtor>>(json) ?? new();
                foreach (var d in debtors)
                    if (!string.IsNullOrWhiteSpace(d.Email))
                        debtorEmails.Add(d.Email.Trim().ToLower());
            }

            // Считаем только письма от должников
            LoadUnreadCountFromDebtors(debtorEmails);

        }

        private void LoadUnreadCountFromDebtors(HashSet<string> debtorEmails)
        {
            try
            {
                // сначала сбросим
                UnreadCount = 0;

                // читаем логин/пароль для чтения почты
                if (!File.Exists(senderConfigPath)) return;
                var cfg = JsonSerializer.Deserialize<EmailSenderConfig>(
                    File.ReadAllText(senderConfigPath));
                if (cfg == null ||
                    string.IsNullOrWhiteSpace(cfg.Email) ||
                    string.IsNullOrWhiteSpace(cfg.ReadPassword))
                    return;

                using var client = new ImapClient();
                client.Connect("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);
                client.Authenticate(cfg.Email, cfg.ReadPassword);
                client.Inbox.Open(FolderAccess.ReadOnly);

                // получаем все непрочитанные
                var uids = client.Inbox.Search(SearchQuery.NotSeen);

                if (uids.Count > 0)
                {
                    // берём только заголовки (Envelope)
                    var summaries = client.Inbox.Fetch(uids,
                        MessageSummaryItems.Envelope);

                    // фильтруем по адресу отправителя
                    var count = summaries.Count(s =>
                        s.Envelope?.From?.Mailboxes
                         .Any(mb => debtorEmails.Contains(mb.Address.ToLower())) == true);

                    UnreadCount = count;
                }

                client.Disconnect(true);
            }
            catch
            {
                UnreadCount = 0;
            }
        }


        // --------------- Локализация ---------------
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LocalizeUI(this);
        }

        // --------------- INotifyPropertyChanged ---------------
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
