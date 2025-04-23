using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Windows;
using System.Windows.Threading;
using FinTrack.Services;    // для AutoNotifier

namespace FinTrack
{
    public partial class App : Application
    {
        private TaskbarIcon? _trayIcon;
        private DispatcherTimer autoSendTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1) Инициализируем трей-иконку из XAML-ресурса
            InitTrayIcon();

            // 2) Таймер автоотправки
            autoSendTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            autoSendTimer.Tick += (_, _) => AutoNotifier.TryAutoSend();
            autoSendTimer.Start();

            // 3) Показ главного окна
            MainWindow = new MainWindow();
            MainWindow.Closing += (s, args) =>
            {
                args.Cancel = true;
                MainWindow.Hide();
            };
            MainWindow.Show();
        }

        private void InitTrayIcon()
        {
            // просто берём TaskbarIcon, описанный в App.xaml
            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        }

        // Обработчики контекстного меню (упоминаются в XAML)
        private void Tray_Open_Click(object? sender, RoutedEventArgs e)
        {
            if (MainWindow == null) MainWindow = new MainWindow();
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        private void Tray_Exit_Click(object? sender, RoutedEventArgs e)
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
