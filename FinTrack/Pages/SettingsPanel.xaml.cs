using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FinTrack.Models;

namespace FinTrack.Pages
{
    public partial class SettingsPanel : UserControl
    {
        private readonly string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "config.json"
        );

        public SettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LocalizeUI(this);
        }

        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string cultureCode)
            {
                LocalizationManager.SetCulture(cultureCode);
                LocalizationManager.LocalizeUI(Application.Current.MainWindow);

                SaveSettings(cultureCode); // <--- важно!
            }
        }


        private void LanguagePopup_Closed(object sender, EventArgs e)
        {
            LanguageToggleButton.IsChecked = false;
        }

        private void SaveSettings(string lang)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            var config = new AppSettings { Language = lang };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }

        private void LoadSettings()
        {
            if (!File.Exists(configPath)) return;

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppSettings>(json);
            if (config == null) return;

            LocalizationManager.SetCulture(config.Language);
            LocalizationManager.LocalizeUI(Application.Current.MainWindow);
        }


        private class AppSettings
        {
            public string Language { get; set; } = "ru";
        }

        public static class AppInitializer
        {
            public static void LoadLanguageFromConfig()
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FinTrack", "config.json"
                );

                if (!File.Exists(configPath)) return;

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppSettings>(json);
                if (config != null)
                {
                    LocalizationManager.SetCulture(config.Language);
                }
            }

            private class AppSettings
            {
                public string Language { get; set; } = "ru";
            }
        }

    }
}
