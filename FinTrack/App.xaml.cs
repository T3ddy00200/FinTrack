using FinTrack;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FinTrack.Services;
using FinTrack.Pages;
using System.Linq;

namespace decoder
{
    public partial class App : Application
    {
        private static Mutex _mutex;
        private TaskbarIcon? _trayIcon;
        private DispatcherTimer autoSendTimer;
        private LicenseStatusWindow _licenseWindow;
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool createdNew;
            _mutex = new Mutex(true, "FinTrackSingletonMutex", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Приложение уже запущено.", "FinTrack", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Отложенный запуск проверки лицензии
            Dispatcher.InvokeAsync(async () =>
            {
                await CheckLicenseAndStartAsync();
            });
        }

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"UI Exception: {e.Exception.Message}", "Unhandled UI Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                MessageBox.Show($"Domain Exception: {ex.Message}", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            MessageBox.Show($"Task Exception: {e.Exception.Message}", "Unobserved Task Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.SetObserved();
        }

        private async Task CheckLicenseAndStartAsync()
        {
            string hwid = HardwareIdHelper.GetHardwareId();

            while (true)
            {
                string? savedLogin = null;
                string? savedKey = null;

                var loginWindow = new LicenseStatusWindow();
                bool? result = loginWindow.ShowDialog();

                if (result == true)
                {
                    savedLogin = loginWindow.EnteredLogin;
                    savedKey = loginWindow.EnteredKey;

                    LicenseStorage.Save(savedLogin, savedKey);
                }
                else
                {
                    MessageBox.Show("🚪 Вход отменён.", "Прерывание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown(); // завершение
                    return;
                }

                if (string.IsNullOrWhiteSpace(savedLogin) || string.IsNullOrWhiteSpace(savedKey))
                {
                    MessageBox.Show("❌ Не удалось загрузить логин и ключ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    LicenseStorage.Clear();
                    continue;
                }

                string response = await LicenseVerifier.VerifyAsync(savedLogin, savedKey, hwid);

                switch (response)
                {
                    case "APPROVED":
                        //MessageBox.Show("✅ Лицензия подтверждена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        InitTrayIcon();
                        InitApp();
                        return;

                    case "REJECTED":
                        MessageBox.Show("🚫 HWID не совпадает или неверный логин/ключ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    case "INVALID":
                        MessageBox.Show("🚫 Такой лицензии не существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;

                    case "WAITING":
                        MessageBox.Show("⏳ Лицензия ещё не активирована.", "Ожидание", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;

                    case "EXPIRED":
                        MessageBox.Show("⌛ Срок действия лицензии истёк.", "Истекло", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;

                    default:
                        MessageBox.Show("🚫 Ошибка соединения с сервером.", "Сервер недоступен", MessageBoxButton.OK, MessageBoxImage.Warning);
                        await Task.Delay(15000);
                        break;
                }

                LicenseStorage.Clear(); // очистка всегда при неудаче
            }
        }


        private void InitApp()
        {
            InitTrayIcon();

            autoSendTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            autoSendTimer.Tick += async (_, _) =>
            {
                await AutoNotifier.TryAutoSend();
            };
            autoSendTimer.Start();

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

        protected override void OnExit(ExitEventArgs e)
        {
            try { _mutex?.ReleaseMutex(); } catch { }
            _trayIcon?.Dispose();
            base.OnExit(e);
        }

        public void Tray_Open_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow == null)
                MainWindow = new MainWindow();

            MainWindow.ShowInTaskbar = true;
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

        public void Tray_OpenDebtors_Click(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
            if (MainWindow is MainWindow mw)
                mw.OpenDebtorsPanel();
        }

        public void Tray_OpenInvoices_Click(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
            if (MainWindow is MainWindow mw)
                mw.OpenInvoicesPanel();
        }

        public void Tray_OpenMessages_Click(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
            if (MainWindow is MainWindow mw)
                mw.OpenMessagesPanel();
        }

        public void Tray_Exit_Click(object sender, RoutedEventArgs e)
        {
            _trayIcon?.Dispose();
            Shutdown();
        }

        private void ShowMainWindow()
        {
            if (MainWindow == null)
                MainWindow = new MainWindow();

            MainWindow.ShowInTaskbar = true;
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }

    }
}
