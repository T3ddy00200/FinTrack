using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FinTrack.Controls;
using FinTrack.Pages;
using FinTrack.Services;
namespace FinTrack
{
    public partial class MainWindow : Window
    {
        private readonly MessagesPanel _messagesPanel = new MessagesPanel();
        private bool isDarkTheme = true;


        public MainWindow()
        {
            InitializeComponent();
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;

            string startPage = Properties.Settings.Default.StartPage;

            SectionTitle.Text = startPage;
            MainContentPanel.Content = CreatePanelByKey(startPage);

            if (startPage == "Messages")
                _ = _messagesPanel.LoadMessagesIfConfiguredAsync();

            //MessageBox.Show(HardwareIdHelper.GetHardwareId());

            //string hwid = HardwareIdHelper.GetHardwareId();
            //string license = LicenseManager.GenerateKey(hwid);

            //MessageBox.Show("🔐 License Key:\n" + license);

            //bool isValid = LicenseManager.VerifyKey(hwid, license);
            //MessageBox.Show("Valid: " + isValid);

        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                //Hide();
            }
            base.OnStateChanged(e);
        }

        public void OpenDebtorsPanel()
        {
            SectionTitle.Text = "Debtors";
            MainContentPanel.Content = CreatePanelByKey("Debtors");
        }

        public void OpenInvoicesPanel()
        {
            SectionTitle.Text = "Invoices";
            MainContentPanel.Content = CreatePanelByKey("Invoices");
        }

        public async void OpenMessagesPanel()
        {
            SectionTitle.Text = "Messages";
            MainContentPanel.Content = _messagesPanel;
            await _messagesPanel.LoadMessagesIfConfiguredAsync();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                //Hide();
                ShowInTaskbar = true;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // optional: save settings or log
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var theme = isDarkTheme ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";
            ApplyTheme(theme);
            isDarkTheme = !isDarkTheme;
        }

        private void ApplyTheme(string themePath)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(themePath, UriKind.Relative)
            };
            var existing = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme"));
            if (existing != null)
                Application.Current.Resources.MergedDictionaries.Remove(existing);
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // nothing to do here any more
        }

        private async void Menu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                SectionTitle.Text = tag;
                MainContentPanel.Content = CreatePanelByKey(tag);

                if (tag == "Messages")
                {
                    await _messagesPanel.LoadMessagesIfConfiguredAsync();
                }
            }
        }

        private UserControl CreatePanelByKey(string tag) => tag switch
        {
            "Home" => new DashboardPanel(),
            "Debtors" => new DebtorsPanel(),
            "Invoices" => new InvoicesPanel(),
            "Reports" => new ReportsPanel(),
            "Messages" => _messagesPanel,
            "Security" => new SecurityPanel(),
            "Settings" => new SettingsPanel(),
            "Users" => new UsersPanel(),
            "Marketing" => new MarketingPanel(),
            _ => new DashboardPanel()
        };
    }
}
