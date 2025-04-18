using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FinTrack.Models;

namespace FinTrack
{
    public partial class App : Application
    {
        private TaskbarIcon? _trayIcon;
        private DispatcherTimer autoSendTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Загружаем язык
            Pages.SettingsPanel.AppInitializer.LoadLanguageFromConfig();

            // 2. Трей-иконка
            InitTrayIcon();

            // 3. Автоотправка уведомлений по таймеру
            autoSendTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            autoSendTimer.Tick += (_, _) =>
            {
                Services.AutoNotifier.TryAutoSend();
            };
            autoSendTimer.Start();

            // 4. Главное окно
            MainWindow = new MainWindow();
            MainWindow.Closing += (s, args) =>
            {
                args.Cancel = true;
                MainWindow.Hide(); // свернуть в трей
            };
            MainWindow.Show();
        }

        private void InitTrayIcon()
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (!File.Exists(iconPath))
            {
                MessageBox.Show("Не найден icon.ico для трей-иконки!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            _trayIcon = new TaskbarIcon
            {
                Icon = new System.Drawing.Icon(iconPath),
                ToolTipText = "FinTrack работает в фоне"
            };

            _trayIcon.ContextMenu = new ContextMenu
            {
                Items =
                {
                    new MenuItem
                    {
                        Header = "Открыть",
                        Command = new RelayCommand(_ => ShowMainWindow())
                    },
                    new MenuItem
                    {
                        Header = "Выход",
                        Command = new RelayCommand(_ => ExitApp())
                    }
                }
            };
        }

        private void ShowMainWindow()
        {
            if (MainWindow == null)
                MainWindow = new MainWindow();

            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        private void ExitApp()
        {
            _trayIcon?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
