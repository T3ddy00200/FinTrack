using System.Windows;

namespace FinTrack.Services
{
    public static class MessageHelper
    {
        public static void Info(string message, string title = "Информация") =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        public static void Warn(string message, string title = "Предупреждение") =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

        public static void Error(string message, string title = "Ошибка") =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

        public static bool Confirm(string message, string title = "Подтвердите") =>
            MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
