using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Resources;
using System.Windows.Threading;
using FinTrack.Services;    // для AutoNotifier
using FinTrack.Pages;       // для MainWindow

namespace FinTrack
{
    public partial class App : Application
    {
        private TaskbarIcon? _trayIcon;
        private DispatcherTimer autoSendTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1) Инициализация трей‑иконки из встроенного ресурса
            InitTrayIcon();

            // 2) Запускаем таймер автоотправки
            autoSendTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            autoSendTimer.Tick += (_, _) => AutoNotifier.TryAutoSend();
            autoSendTimer.Start();

            // 3) Создаём и показываем главное окно (с возможностью сворачивать в трей)
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
            try
            {
                // Pack URI к ресурсу icon.ico (Build Action = Resource)
                var uri = new Uri("pack://application:,,,/FinTrack;component/Themes/Images/icon.ico", UriKind.Absolute);

                // Получаем поток из ресурса
                StreamResourceInfo sri = Application.GetResourceStream(uri);
                if (sri == null)
                    throw new FileNotFoundException("Ресурс icon.ico не найден внутри сборки.", uri.ToString());

                // Создаём TaskbarIcon из потока
                using (var iconStream = sri.Stream)
                {
                    _trayIcon = new TaskbarIcon
                    {
                        Icon = new System.Drawing.Icon(iconStream),
                        ToolTipText = "FinTrack работает в фоне"
                    };
                }

                // Контекстное меню
                _trayIcon.ContextMenu = new ContextMenu
                {
                    Items =
                    {
                        new MenuItem { Header = "Открыть", Command = new RelayCommand(_ => ShowMainWindow()) },
                        new MenuItem { Header = "Выход",   Command = new RelayCommand(_ => ExitApp()) }
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось инициализировать трей‑иконку:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
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
