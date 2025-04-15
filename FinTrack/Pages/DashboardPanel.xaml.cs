using FinTrack.Models;
using FinTrack.Views;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FinTrack.Pages;
using System.IO;

namespace FinTrack.Controls
{
    public partial class DashboardPanel : UserControl
    {

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LocalizeUI(this);
        }
        private readonly string debtorFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json"
        );

        public decimal TotalDebt { get; set; }
        public int OpenInvoices { get; set; }
        public ObservableCollection<string> RecentNotifications { get; set; } = new();

        public DashboardPanel()
        {
            InitializeComponent();
            DataContext = this;
            LoadDashboardData();
        }

        private void LoadDashboardData()
        {
            try
            {
                if (File.Exists(debtorFilePath))
                {
                    string json = File.ReadAllText(debtorFilePath);
                    var debtors = JsonSerializer.Deserialize<List<Debtor>>(json);

                    if (debtors != null)
                    {
                        TotalDebt = debtors.Sum(d => d.TotalDebt - d.Paid);
                        OpenInvoices = debtors.Count(d => d.TotalDebt > d.Paid);
                    }
                }

                // Пример уведомлений (можно заменить загрузкой из файла или ViewModel)
                RecentNotifications = new ObservableCollection<string>
                {
                    "Новый инвойс от ИП 'Пример'",
                    "Платёж по инвойсу #1045",
                    "Изменения в настройках безопасности"
                };

                // Обновим биндинги
                DataContext = null;
                DataContext = this;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных панели: " + ex.Message);
            }
        }
    }
}
