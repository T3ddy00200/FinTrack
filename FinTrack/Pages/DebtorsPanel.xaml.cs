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
using FinTrack.Services;
using System.Windows.Shell;
using System.Windows.Media.Animation;
using System.Windows.Data;

namespace FinTrack.Pages
{
  

    public partial class DebtorsPanel : UserControl
    {
        private ObservableCollection<Debtor> OriginalDebtors = new();

        private static bool IsDirty = false; // true — если были изменения
        private bool isInitialized = false; // для одноразовой загрузки

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
        }

        private void ChangeEmail_Click(object sender, RoutedEventArgs e)
        {
            if (DebtorsGrid.SelectedItem is Debtor selected)
            {
                var prompt = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Введите новый Email для {selected.Name}:", "Сменить Email", selected.Email ?? "");

                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    selected.Email = prompt.Trim();
                    EmailDatabase.AddOrUpdate(selected.Name, selected.Email);
                    EmailDatabase.Save();
                    DebtorsGrid.Items.Refresh();
                    MessageBox.Show($"Email обновлён: {selected.Name} → {selected.Email}", "Успех");
                }
            }
            else
            {
                MessageBox.Show("Выберите должника для изменения Email.", "Нет выбора", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (isInitialized && !IsDirty)
                return;

            var loaded = await Task.Run(() => DebtorStorage.Load(localFilePath));

            var sorted = loaded
                .OrderBy(d => d.IsPaid)
                .ThenBy(d => d.DueDate)
                .ToList();

            Debtors.ReplaceRange(sorted);
            OriginalDebtors = new ObservableCollection<Debtor>(sorted);

            var view = CollectionViewSource.GetDefaultView(Debtors);
            view.Filter = FilterDebtors;

            isInitialized = true;
            IsDirty = false;
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
            string name = CompanyInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название компании.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var debtor = new Debtor
            {
                Name = name,
                ContactName = ContactNameInput.Text.Trim(),
                Email = EmailInput.Text.Trim(),
                InvoiceNumber = PhoneInput.Text.Trim(),
                TotalDebt = decimal.TryParse(DebtInput.Text, out var debt) ? debt : 0,
                DueDate = DueDateInput.SelectedDate ?? DateTime.Today,
                InvoiceFilePath = selectedInvoicePath // используем выбранный инвойс
            };

            Debtors.Add(debtor);
            OriginalDebtors.Add(debtor); // ← важно для поиска и фильтрации

            _ = Task.Run(() => AuditLogger.Log($"Добавлен должник: {debtor.Name}"));

            SaveDebtors();  // сохраняем JSON
            SortDebtors();  // сохраняем порядок

            AddDebtorModal.Visibility = Visibility.Collapsed;
            ClearInputs();
        }


        private void SortDebtors()
        {
            if (Debtors.Count <= 1)
                return;

            var sorted = Debtors
                .OrderBy(d => d.IsPaid)
                .ThenBy(d => d.DueDate)
                .ToList();

            if (Debtors.SequenceEqual(sorted))
                return;
            Debtors.ReplaceRange(sorted);

            DebtorsGrid.Items.Refresh(); // ItemsSource не меняется, достаточно

        }

        private void DeleteDebtor_Click(object sender, RoutedEventArgs e)
        {
            var selected = DebtorsGrid.SelectedItems.Cast<Debtor>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одного должника для удаления.",
                                "Нет выбора", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Удалить {selected.Count} должников?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            foreach (var debtor in selected)
            {
                Debtors.Remove(debtor); // ObservableCollection уведомит UI
            }

            _ = Task.Run(() =>
            {
                foreach (var debtor in selected)
                    AuditLogger.Log($"Удалён должник: {debtor.Name}");
            });

            SaveDebtors();
            DebtorsGrid.Items.Refresh();
        }

        private void ChangeStatus_Click(object sender, RoutedEventArgs e)
        {
            var selected = DebtorsGrid.SelectedItems.Cast<Debtor>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Выберите одного или нескольких должников.",
                                "Нет выбора", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var statusWindow = new ChangeStatusWindow();
            if (statusWindow.ShowDialog() != true)
                return;

            string newStatus = statusWindow.SelectedStatus;
            bool anyChanged = false;
            var logs = new List<string>();

            foreach (var d in selected)
            {
                switch (newStatus)
                {
                    case "Оплачено":
                        if (d.Paid != d.TotalDebt)
                        {
                            d.Paid = d.TotalDebt;
                            logs.Add($"Статус должника {d.Name} => Оплачено");
                            anyChanged = true;
                        }
                        break;

                    case "Не оплачено":
                        if (d.Paid != 0)
                        {
                            d.Paid = 0;
                            logs.Add($"Статус должника {d.Name} => Не оплачено");
                            anyChanged = true;
                        }
                        break;

                    case "Частично оплачено":
                        var partialWindow = new PartialPaymentWindow(d.Name);
                        if (partialWindow.ShowDialog() == true)
                        {
                            var amount = partialWindow.EnteredAmount;
                            if (amount > 0)
                            {
                                d.Paid = Math.Min(d.Paid + amount, d.TotalDebt);
                                logs.Add($"Статус должника {d.Name} => Частично оплачено (+{amount:0.00})");
                                anyChanged = true;
                            }
                        }
                        break;
                }
            }

            if (anyChanged)
            {
                DebtorsGrid.Items.Refresh();
                SaveDebtors();
                SortDebtors();

                _ = Task.Run(() =>
                {
                    foreach (var log in logs)
                        AuditLogger.Log(log);
                });
            }
        }
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            DebtorsGrid.SelectAll();
        }

        private void DebtorsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MultiSelectToggle.IsChecked != true)
                return;

            var row = UIHelpers.GetDataGridRowUnderMouse<Debtor>(DebtorsGrid, e);
            if (row?.Item is not Debtor item)
                return;

            try
            {
                if (DebtorsGrid.SelectedItems.Contains(item))
                    DebtorsGrid.SelectedItems.Remove(item);
                else
                    DebtorsGrid.SelectedItems.Add(item);

                // Принудительное визуальное выделение строки
                row.IsSelected = true;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе строки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UploadInvoice_Click(object sender, RoutedEventArgs e)
        {
            var selectedDebtors = DebtorsGrid.SelectedItems.Cast<Debtor>().ToList();

            if (selectedDebtors.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одного должника, чтобы загрузить ему инвойс.",
                                "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                Title = selectedDebtors.Count == 1
                    ? "Выберите PDF-инвойс"
                    : $"Выберите PDF-инвойс для {selectedDebtors.Count} должников"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                Directory.CreateDirectory(invoicesDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания папки для инвойсов:\n{ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string originalName = Path.GetFileName(dlg.FileName);

            string CleanFileName(string input)
            {
                var invalid = Path.GetInvalidFileNameChars();
                return string.Join("_", input.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
            }

            var logs = new List<string>();
            int successCount = 0;

            try
            {
                foreach (var debtor in selectedDebtors)
                {
                    var safeName = CleanFileName(debtor.Name);
                    var targetName = $"{safeName}_{originalName}";
                    var targetPath = Path.Combine(invoicesDir, targetName);

                    File.Copy(dlg.FileName, targetPath, overwrite: true);
                    debtor.InvoiceFilePath = targetPath;
                    logs.Add($"Инвойс '{originalName}' привязан к {debtor.Name}");
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                logs.Add($"Ошибка при копировании файла: {ex.Message}");
            }

            DebtorsGrid.Items.Refresh();
            SaveDebtors();

            _ = Task.Run(() =>
            {
                foreach (var log in logs)
                    AuditLogger.Log(log);
            });

            MessageBox.Show(
                successCount == 1
                    ? "Инвойс успешно привязан к выбранному должнику."
                    : $"Инвойс успешно привязан к {successCount} должникам.",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveDebtors()
        {
            if (Debtors == null || Debtors.Count == 0)
            {
                MessageBox.Show("Список должников пуст. Сохранение не выполнено.",
                                "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DebtorStorage.Save(localFilePath, Debtors);
            IsDirty = true;
        }


        private void LoadDebtors()
        {
            if (!File.Exists(localFilePath))
                return;

            try
            {
                string json = File.ReadAllText(localFilePath);
                var loaded = JsonSerializer.Deserialize<List<Debtor>>(json);

                if (loaded == null || loaded.Count == 0)
                {
                    MessageBox.Show("Файл пуст или повреждён. Попробуйте восстановить из резервной копии.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sorted = loaded.OrderBy(d => d.IsPaid).ThenBy(d => d.DueDate).ToList();

                OriginalDebtors = new ObservableCollection<Debtor>(sorted);
                Debtors.ReplaceRange(sorted);


                DebtorsGrid.Items.Refresh();
            }
            catch (Exception ex)
            {
                // Попытка восстановить из .bak
                string backupPath = localFilePath + ".bak";
                if (File.Exists(backupPath))
                {
                    try
                    {
                        File.Copy(backupPath, localFilePath, overwrite: true);
                        MessageBox.Show("Основной файл повреждён. Выполнено восстановление из резервной копии.", "Восстановление", MessageBoxButton.OK, MessageBoxImage.Warning);
                        LoadDebtors(); // повторно загрузить уже из восстановленного
                    }
                    catch (Exception backupEx)
                    {
                        MessageBox.Show("Ошибка восстановления из резервной копии:\n" + backupEx.Message,
                                        "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Ошибка загрузки данных:\n" + ex.Message,
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ManualLoad_Click(object sender, RoutedEventArgs e)
        {
            LoadDebtors();
            MessageBox.Show("Данные успешно загружены из файла.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void ManualSave_Click(object sender, RoutedEventArgs e)
        {
            SaveDebtors();
            MessageBox.Show("Данные успешно сохранены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string _lastSearchQuery = "";

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim().ToLowerInvariant();

            if (query == _lastSearchQuery)
                return;

            _lastSearchQuery = query;
            CollectionViewSource.GetDefaultView(Debtors)?.Refresh();
        }


        private void ResetDebtorsList()
        {
            Debtors.Clear();
            foreach (var d in OriginalDebtors)
                Debtors.Add(d);

            DebtorsGrid.Items.Refresh();
        }

        private void DebtorsGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualParent<ScrollViewer>((DependencyObject)sender);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private bool FilterDebtors(object obj)
        {
            if (obj is not Debtor d) return false;

            return string.IsNullOrWhiteSpace(_lastSearchQuery)
                || d.Name.Contains(_lastSearchQuery, StringComparison.OrdinalIgnoreCase)
                || (d.Email?.Contains(_lastSearchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.InvoiceNumber?.Contains(_lastSearchQuery, StringComparison.OrdinalIgnoreCase) ?? false);
        }


        // Универсальный метод поиска родителя в визуальном дереве
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null && child is not T)
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as T;
        }

        private void ImportFromExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                Title = "Выберите Excel-файл"
            };

            if (dlg.ShowDialog() != true)
                return;

            // 🟢 Пользователь вводит буквы колонок (например: A, b, c, D...)
            int colName = Ask("Название компании", "A");
            int colInvoice = Ask("Номер инвойса", "B");
            int colDate = Ask("Срок оплаты (YYYY-MM-DD)", "C");
            int colTotal = Ask("Сумма долга", "D");
            int colPaid = Ask("Оплачено (можно пропустить для пропуска оставьте поле пустым)", "E");
            int colEmail = Ask("Email (можно пропустить пропуска оставьте поле пустым)", "F");

            var tempList = new List<Debtor>();

            try
            {
                using var workbook = new XLWorkbook(dlg.FileName);
                var sheet = workbook.Worksheets.First();

                for (int i = 2; i <= sheet.LastRowUsed().RowNumber(); i++)
                {
                    var row = sheet.Row(i);

                    var debtor = new Debtor
                    {
                        Name = row.Cell(colName).GetValue<string>(),
                        InvoiceNumber = row.Cell(colInvoice).GetValue<string>(),
                        DueDate = DateTime.TryParse(row.Cell(colDate).GetString(), out var dt) ? dt : DateTime.Today,
                        TotalDebt = decimal.TryParse(row.Cell(colTotal).GetString(), out var total) ? total : 0,
                        Paid = decimal.TryParse(row.Cell(colPaid).GetString(), out var paid) ? paid : 0,
                        Email = row.Cell(colEmail).GetValue<string>()
                    };

                    tempList.Add(debtor);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка импорта Excel: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (tempList.Count > 0)
            {
                var window = new ImportedDebtorsWindow(new ObservableCollection<Debtor>(tempList));
                if (window.ShowDialog() == true)
                {
                    DebtorsGrid.Visibility = Visibility.Collapsed;

                    foreach (var d in tempList)
                        Debtors.Add(d);

                    _ = Task.Run(() =>
                    {
                        foreach (var d in tempList)
                            AuditLogger.Log($"Импортирован должник из Excel: {d.Name}");
                    });

                    SaveDebtors();
                    SortDebtors();
                    DebtorsGrid.Visibility = Visibility.Visible;

                    MessageBox.Show($"Импортировано {tempList.Count} должников.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("В файле нет записей для импорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // 🔧 Утилита для запроса и перевода буквы в номер (A → 1, B → 2, ..., Z → 26)
        private int Ask(string label, string defLetter)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Введите БУКВУ колонки для поля:\n{label}\n(например: A, B, C...)",
                "Колонка Excel", defLetter.ToUpper());

            input = input.Trim().ToUpper();

            if (input.Length == 1 && input[0] >= 'A' && input[0] <= 'Z')
                return input[0] - 'A' + 1;

            return defLetter[0] - 'A' + 1;
        }

        private string selectedInvoicePath = string.Empty;

        private void SelectInvoiceFromModal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "PDF файлы (*.pdf)|*.pdf",
                    Title = "Выберите PDF-инвойс"
                };

                if (dialog.ShowDialog() == true)
                {
                    selectedInvoicePath = dialog.FileName;
                    InvoiceFileNameText.Text = Path.GetFileName(selectedInvoicePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе файла:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearInvoice_Click(object sender, RoutedEventArgs e)
        {
            selectedInvoicePath = string.Empty;
            InvoiceFileNameText.Text = "Инвойс не выбран";
        }

        private void ClearInputs()
        {
            CompanyInput.Text = string.Empty;
            ContactNameInput.Text = string.Empty;
            EmailInput.Text = string.Empty;
            PhoneInput.Text = string.Empty;
            DebtInput.Text = string.Empty;
            DueDateInput.SelectedDate = null;
            ClearInvoice_Click(null, null); // сброс выбранного файла
        }

    }
}