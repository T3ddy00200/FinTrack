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
using System.ComponentModel;
using ClosedXML.Excel;
using System.Windows.Input;
using System.Windows.Media;

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
            SortDebtors();
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
            // Собираем всех выделенных должников
            var selectedDebtors = DebtorsGrid.SelectedItems
                .Cast<Debtor>()
                .ToList();

            if (selectedDebtors.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одного должника, чтобы загрузить ему инвойс.",
                                "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Открываем диалог выбора PDF
            var dlg = new OpenFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                Title = selectedDebtors.Count == 1
                    ? "Выберите PDF-инвойс"
                    : $"Выберите PDF-инвойс для {selectedDebtors.Count} должников"
            };

            if (dlg.ShowDialog() != true)
                return;

            // Копируем файл в общую папку и привязываем к каждому
            Directory.CreateDirectory(invoicesDir);
            var originalName = Path.GetFileName(dlg.FileName);

            foreach (var debtor in selectedDebtors)
            {
                // Формируем уникальное имя на основе имени должника
                var targetName = $"{debtor.Name}_{originalName}";
                var targetPath = Path.Combine(invoicesDir, targetName);

                File.Copy(dlg.FileName, targetPath, overwrite: true);
                debtor.InvoiceFilePath = targetPath;
            }

            // Сохраняем все изменения
            SaveDebtors();
            DebtorsGrid.Items.Refresh();

            MessageBox.Show(
                selectedDebtors.Count == 1
                    ? "Инвойс успешно привязан к выбранному должнику."
                    : $"Инвойс успешно привязан к {selectedDebtors.Count} должникам.",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void DebtorsGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Ищем внешний ScrollViewer (тот, что вокруг всего UserControl)
            var scroll = FindVisualParent<ScrollViewer>((DependencyObject)sender);
            if (scroll != null)
            {
                scroll.ScrollToVerticalOffset(scroll.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        // Универсальный метод поиска родителя в визуальном дереве
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = child;
            while (parent != null && parent is not T)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }
        private void ImportFromExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                Title = "Выберите Excel-файл"
            };

            if (dlg.ShowDialog() != true) return;

            ObservableCollection<Debtor> tempList = new();
            try
            {
                using var workbook = new XLWorkbook(dlg.FileName);
                var sheet = workbook.Worksheets.First();
                for (int i = 2; i <= sheet.LastRowUsed().RowNumber(); i++)
                {
                    var row = sheet.Row(i);
                    var loaded = new Debtor
                    {
                        Name = row.Cell(1).GetValue<string>(),
                        Email = row.Cell(2).GetValue<string>(),
                        Phone = row.Cell(3).GetValue<string>(),
                        TotalDebt = decimal.TryParse(row.Cell(4).GetString(), out var d) ? d : 0,
                        Paid = decimal.TryParse(row.Cell(5).GetString(), out var p) ? p : 0,
                        DueDate = DateTime.TryParse(row.Cell(6).GetString(), out var dt) ? dt : DateTime.Today,
                        InvoiceFilePath = row.Cell(7).GetValue<string>()
                    };
                    tempList.Add(loaded);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка импорта Excel: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Если есть что обрабатывать — открываем окно с импортом
            if (tempList.Count > 0)
            {
                var window = new ImportedDebtorsWindow(tempList);
                if (window.ShowDialog() == true)
                {
                    // По «Подтвердить» добавляем всех в основную коллекцию
                    foreach (var d in tempList)
                    {
                        Debtors.Add(d);
                    }
                    SaveDebtors();
                    SortDebtors();
                    MessageBox.Show("Должники из Excel добавлены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("В файле нет записей для импорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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