using System;
using System.Linq;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;

// Убедись, что пространство имен здесь FinTrack
namespace FinTrack
{
    public partial class App : Application
    {
        private static Mutex _mutex;
        private TaskbarIcon? _trayIcon;
        private DispatcherTimer autoSendTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, "FinTrackSingletonMutex", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Приложение уже запущено.", "FinTrack", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);
            InitTrayIcon();

            autoSendTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            autoSendTimer.Tick += async (_, _) => { await Services.AutoNotifier.TryAutoSend(); };
            autoSendTimer.Start();

            // Теперь MainWindow создается из правильного контекста
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
            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        }

        public void Tray_Open_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow == null) MainWindow = new MainWindow();
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        public void Tray_Exit_Click(object sender, RoutedEventArgs e)
        {
            _trayIcon?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}