using System.Windows;
using System.Windows.Controls;

namespace FinTrack.Windows
{
    public partial class ChangeStatusWindow : Window
    {
        public string SelectedStatus { get; private set; }

        public ChangeStatusWindow()
        {
            InitializeComponent();
        }

        private void Paid_Click(object sender, RoutedEventArgs e)
        {
            SelectedStatus = "Оплачено";
            DialogResult = true;
        }

        private void Partial_Click(object sender, RoutedEventArgs e)
        {
            SelectedStatus = "Частично оплачено";
            DialogResult = true;
        }

        private void Unpaid_Click(object sender, RoutedEventArgs e)
        {
            SelectedStatus = "Не оплачено";
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
