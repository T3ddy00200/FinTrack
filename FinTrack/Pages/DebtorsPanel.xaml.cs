using FinTrack.Models;
using FinTrack.Views;
using FinTrack.Windows;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FinTrack.Properties;

namespace FinTrack.Pages
{
    public partial class DebtorsPanel : UserControl
    {
        private ObservableCollection<Debtor> OriginalDebtors = new();

        private readonly string localFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json"
        );

        private readonly string invoicesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "Invoices"
        );

        public ObservableCollection<Debtor> Debtors { get; set; } = new ObservableCollection<Debtor>();

        public DebtorsPanel()
        {
            InitializeComponent();
            DebtorsGrid.ItemsSource = Debtors;
            LoadDebtors();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LocalizeUI(this);
        }

        private void OpenAddModal_Click(object sender, RoutedEventArgs e)
        {
            AddDebtorModal.Visibility = Visibility.Visible;
            SortDebtors();
        }

        private void CancelDebtor_Click(object sender, RoutedEventArgs e)
        {
            AddDebtorModal.Visibility = Visibility.Collapsed;
            ClearInputs();
        }

        private void SaveDebtor_Click(object sender, RoutedEventArgs e)
        {
            var debtor = new Debtor
            {
                Name = CompanyInput.Text,
                Email = EmailInput.Text,
                Phone = PhoneInput.Text,
                TotalDebt = decimal.TryParse(DebtInput.Text, out var debt) ? debt : 0,
                DueDate = DueDateInput.SelectedDate ?? DateTime.Today,
                
            };

            Debtors.Add(debtor);
            AddDebtorModal.Visibility = Visibility.Collapsed;
            ClearInputs();
            SaveDebtors();
        }

        private void SortDebtors()
        {
            var sorted = Debtors
                .OrderBy(d => d.IsPaid)
                .ThenBy(d => d.DueDate)
                .ToList();

            Debtors.Clear();
            foreach (var d in sorted)
                Debtors.Add(d);
        }

        private void DeleteDebtor_Click(object sender, RoutedEventArgs e)
        {
            var selected = DebtorsGrid.SelectedItems.Cast<Debtor>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одного должника для удаления.");
                return;
            }

            if (MessageBox.Show("Удалить выбранных должников?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var debtor in selected)
                    Debtors.Remove(debtor);

                SaveDebtors();
            }
        }

        private void ChangeStatus_Click(object sender, RoutedEventArgs e)
        {
            var selected = DebtorsGrid.SelectedItems.Cast<Debtor>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Выберите одного или нескольких должников.", "Нет выбора", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var statusWindow = new ChangeStatusWindow(); // окно выбора статуса
            if (statusWindow.ShowDialog() == true)
            {
                string selectedStatus = statusWindow.SelectedStatus;

                if (selectedStatus == "Оплачено")
                {
                    foreach (var debtor in selected)
                        debtor.Paid = debtor.TotalDebt;
                }
                else if (selectedStatus == "Не оплачено")
                {
                    foreach (var debtor in selected)
                        debtor.Paid = 0;
                }
                else if (selectedStatus == "Частично оплачено")
                {
                    foreach (var debtor in selected)
                    {
                        var partialWindow = new PartialPaymentWindow(debtor.Name);
                        if (partialWindow.ShowDialog() == true)
                        {
                            var amount = partialWindow.EnteredAmount;
                            debtor.Paid = Math.Min(debtor.Paid + amount, debtor.TotalDebt);
                        }
                    }
                }

                DebtorsGrid.Items.Refresh();
                SaveDebtors();
                SortDebtors();
            }
        }


        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            DebtorsGrid.SelectAll();
        }

        private void DebtorsGrid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (MultiSelectToggle.IsChecked == true)
            {
                var row = UIHelpers.GetDataGridRowUnderMouse<Debtor>(DebtorsGrid, e);
                if (row != null)
                {
                    var item = (Debtor)row.Item;
                    if (DebtorsGrid.SelectedItems.Contains(item))
                        DebtorsGrid.SelectedItems.Remove(item);
                    else
                        DebtorsGrid.SelectedItems.Add(item);

                    e.Handled = true;
                }
            }
        }


        private void UploadInvoice_Click(object sender, RoutedEventArgs e)
        {
            var selectedDebtor = DebtorsGrid.SelectedItem as Debtor;
            if (selectedDebtor == null)
            {
                MessageBox.Show("Выберите должника для загрузки инвойса.");
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                Title = "Выберите PDF-инвойс"
            };

            if (dialog.ShowDialog() == true)
            {
                Directory.CreateDirectory(invoicesDir);
                var fileName = Path.GetFileName(dialog.FileName);
                var targetPath = Path.Combine(invoicesDir, $"{selectedDebtor.Name}_{fileName}");

                File.Copy(dialog.FileName, targetPath, true);
                selectedDebtor.InvoiceFilePath = targetPath;
                SaveDebtors();

                MessageBox.Show("Инвойс успешно привязан.");
            }
        }

        private void SaveDebtors()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));
            string json = JsonSerializer.Serialize(Debtors);
            File.WriteAllText(localFilePath, json);
        }

        private void LoadDebtors()
        {
            if (File.Exists(localFilePath))
            {
                string json = File.ReadAllText(localFilePath);
                var loaded = JsonSerializer.Deserialize<ObservableCollection<Debtor>>(json);
                if (loaded != null)
                    Debtors = loaded;
                OriginalDebtors = loaded;
                Debtors = new ObservableCollection<Debtor>(OriginalDebtors);
                DebtorsGrid.ItemsSource = Debtors;
            }

            DebtorsGrid.ItemsSource = Debtors;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.ToLower();

            var filtered = OriginalDebtors.Where(d =>
                d.Name.ToLower().Contains(query) ||
                d.Email.ToLower().Contains(query) ||
                d.Phone.ToLower().Contains(query)).ToList();

            Debtors.Clear();
            foreach (var d in filtered)
                Debtors.Add(d);
        }

        private void ClearInputs()
        {
            CompanyInput.Text = "";
            EmailInput.Text = "";
            PhoneInput.Text = "";
            DebtInput.Text = "";
            DueDateInput.SelectedDate = null;
        }
        private void ClearInvoice_Click(object sender, RoutedEventArgs e)
        {
            selectedInvoicePath = string.Empty;
            InvoiceFileNameText.Text = "Инвойс не выбран";
        }


        private string selectedInvoicePath = string.Empty;

        private void SelectInvoiceFromModal_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                Title = "Выберите PDF-инвойс"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedInvoicePath = dialog.FileName;
                InvoiceFileNameText.Text = System.IO.Path.GetFileName(selectedInvoicePath);
            }
        }

    }
}