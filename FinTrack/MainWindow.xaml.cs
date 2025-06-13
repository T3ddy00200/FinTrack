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
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            string startPage = Properties.Settings.Default.StartPage;
            if (string.IsNullOrEmpty(startPage))
            {
                startPage = "Home";
            }

            UpdateContentByTag(startPage);

            var initialItem = MenuListBox.Items.OfType<ListBoxItem>().FirstOrDefault(item => (string)item.Tag == startPage);
            if (initialItem != null)
            {
                MenuListBox.SelectedItem = initialItem;
            }
        }

        private void Menu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ListBoxItem selectedItem)
            {
                if (selectedItem.Tag is string tag)
                {
                    UpdateContentByTag(tag);
                }
            }
        }

        private void UpdateContentByTag(string tag)
        {
            if (SectionTitle == null)
            {
                MessageBox.Show("SectionTitle не инициализирован!");
                return;
            }
            SectionTitle.Text = tag;
            MainContentPanel.Content = CreatePanelByKey(tag);

            if (tag == "Messages")
            {
                _ = _messagesPanel.LoadMessagesIfConfiguredAsync();
            }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var theme = isDarkTheme ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";
            ApplyTheme(theme);
            isDarkTheme = !isDarkTheme;
        }

        private void ApplyTheme(string themePath)
        {
            var dict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
            var existing = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme"));

            if (existing != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existing);
            }
            Application.Current.Resources.MergedDictionaries.Add(dict);
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

        #region Window State Handlers
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized) Hide();
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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) { }
        #endregion
    }
}