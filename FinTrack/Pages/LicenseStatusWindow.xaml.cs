using System.Windows;

namespace FinTrack.Pages
{
    public partial class LicenseStatusWindow : Window
    {
        public string? EnteredLogin { get; private set; }
        public string? EnteredKey { get; private set; }

        public LicenseStatusWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            EnteredLogin = LoginBox.Text.Trim();
            EnteredKey = KeyBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(EnteredLogin) || string.IsNullOrWhiteSpace(EnteredKey))
            {
                StatusText.Text = "Пожалуйста, введите логин и ключ.";
                return;
            }

            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            //this.DialogResult = false;
            this.Close();
        }

    }

}
