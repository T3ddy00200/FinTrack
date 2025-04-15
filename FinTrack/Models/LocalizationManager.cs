using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FinTrack.Models
{
    public static class LocalizationManager
    {
        private static readonly ResourceManager _resourceManager =
            new ResourceManager("FinTrack.Language.Strings", typeof(LocalizationManager).Assembly);

        public static void SetCulture(string cultureCode)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureCode);
        }

        public static void LocalizeUI(DependencyObject root)
        {
            foreach (var element in FindVisualChildren<FrameworkElement>(root))
            {
                if (string.IsNullOrEmpty(element.Name))
                    continue;

                string value = _resourceManager.GetString(element.Name);
                if (string.IsNullOrEmpty(value))
                    continue;

                switch (element)
                {
                    case HeaderedContentControl hcc:
                        hcc.Header = value;
                        break;
                    case TextBlock tb:
                        tb.Text = value;
                        break;
                    case ContentControl cc:
                        cc.Content = value;
                        break;
                }

            }
        }

        public static string GetStringByKey(string key)
        {
            var value = _resourceManager.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }


        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T t)
                    yield return t;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        public static void RefreshAllWindows()
        {
            foreach (Window window in Application.Current.Windows)
            {
                LocalizeUI(window);
            }
        }

    }
}
