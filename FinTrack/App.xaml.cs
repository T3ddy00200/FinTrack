using FinTrack;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace decoder
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
            autoSendTimer.Tick += async (_, _) =>
            {
                await FinTrack.Services.AutoNotifier.TryAutoSend();
            };
            autoSendTimer.Start();

            MainWindow = new MainWindow();
            MainWindow.Closing += (s, args) =>
            {
                args.Cancel = true;
                MainWindow.Hide();
            };
            MainWindow.Show();

            MainWindow.StateChanged += (s, args) =>
            {
                if (MainWindow.WindowState == WindowState.Minimized)
                {
                    MainWindow.ShowInTaskbar = true;
                }
            };

            SetBrowserFeatureControl();
        }

        private void SetBrowserFeatureControl()
        {
            try
            {
                string appName = System.IO.Path.GetFileName(
                    System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                Registry.SetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION",
                    appName,
                    11001,
                    RegistryValueKind.DWord
                );
            }
            catch
            {
                // игнорируем ошибки
            }
        }

        private void InitTrayIcon()
        {
            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
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

        private void ShowMainWindow()
        {
            if (MainWindow == null)
                MainWindow = new MainWindow();

            MainWindow.ShowInTaskbar = true;
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
            try { _mutex?.ReleaseMutex(); } catch { }
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
