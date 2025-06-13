using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FinTrack.Controls;
using FinTrack.Pages;

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
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
            base.OnStateChanged(e);
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
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
