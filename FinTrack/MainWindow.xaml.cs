using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FinTrack.Controls;
using FinTrack.Pages;
using FinTrack.Properties;
using FinTrack.Models;
using static FinTrack.Pages.SettingsPanel;

namespace FinTrack
{
    public partial class MainWindow : Window
    {
        private bool isDarkTheme = true;
        private MessagesPanel _messagesPanel;

        public MainWindow()
        {
            var savedLang = Settings.Default.Language;
            LocalizationManager.SetCulture(savedLang);
            AppInitializer.LoadLanguageFromConfig();

            InitializeComponent();
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            LocalizationManager.LocalizeUI(this);

            // Инициализация MessagesPanel один раз, чтобы она работала в фоне
            _messagesPanel = new MessagesPanel();

            string startPage = Settings.Default.StartPage;
            SectionTitle.Text = LocalizationManager.GetStringByKey(startPage);
            MainContentPanel.Content = CreatePanelByKey(startPage);
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
            // Можно логировать или сохранять настройки при закрытии
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LocalizeUI(this);
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                SectionTitle.Text = LocalizationManager.GetStringByKey(button.Name);
                MainContentPanel.Content = CreatePanelByKey(tag);
            }
        }

        private UserControl CreatePanelByKey(string tag) => tag switch
        {
            "Главная" => new DashboardPanel(),
            "Должники" => new DebtorsPanel(),
            "Инвойсы" => new InvoicesPanel(),
            "Отчёты" => new ReportsPanel(),
            "Уведомления" => new NotificationsPanel(),
            "Сообщения" => _messagesPanel,
            "Ответы" => new RepliesPanel(),
            "Безопасность" => new SecurityPanel(),
            "Настройки" => new SettingsPanel(),
            "Пользователи" => new UsersPanel(),
            _ => new DashboardPanel()
        };
    }
}
