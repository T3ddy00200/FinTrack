using System;
using System.Globalization;
using System.Windows;

namespace FinTrack.Windows
{
    public partial class PartialPaymentWindow : Window
    {
        public decimal EnteredAmount { get; private set; }
        public bool AddToExisting { get; private set; }

        public PartialPaymentWindow(string debtorName)
        {
            InitializeComponent();
            DebtorNameText.Text = debtorName;
        }

        private void SetExactAmount_Click(object sender, RoutedEventArgs e)
        {
            ProcessInput(addToExisting: false);
        }

        private void AddToExisting_Click(object sender, RoutedEventArgs e)
        {
            ProcessInput(addToExisting: true);
        }


        private void ProcessInput(bool addToExisting)
        {
            if (decimal.TryParse(AmountBox.Text, out decimal amount) && amount >= 0)
            {
                EnteredAmount = amount;
                AddToExisting = addToExisting;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Введите корректную сумму", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}