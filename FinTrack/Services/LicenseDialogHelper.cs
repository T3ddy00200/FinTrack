using FinTrack.Pages;
using System.Windows;

namespace FinTrack.Services
{
    public static class LicenseDialogHelper
    {
        public static (string login, string key)? AskLoginAndKey()
        {
            var dialog = new LicenseStatusWindow();
            var result = dialog.ShowDialog();

            if (result == true &&
                !string.IsNullOrWhiteSpace(dialog.EnteredLogin) &&
                !string.IsNullOrWhiteSpace(dialog.EnteredKey))
            {
                return (dialog.EnteredLogin, dialog.EnteredKey);
            }

            return null;
        }
    }
}
