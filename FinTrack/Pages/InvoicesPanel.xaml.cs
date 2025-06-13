using FinTrack.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FinTrack.Views;
using FinTrack.Services;
using System.Windows.Input;
using System.Windows.Media;

namespace FinTrack.Controls
{
    public partial class InvoicesPanel : UserControl
    {
        private readonly string debtorFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json");

        private readonly string invoicesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "Invoices");

        private ObservableCollection<Debtor> AllDebtors = new();

        private class DebtorDisplay
        {
            public string Name { get; set; }
            public string FileName { get; set; }
            public Debtor FullDebtor { get; set; }
            public decimal TotalDebt => FullDebtor?.TotalDebt ?? 0;
        }

        public InvoicesPanel()
        {
            InitializeComponent();
            InvoicesGrid.PreviewMouseWheel += InvoicesGrid_PreviewMouseWheel;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDebtorsAsync();
        }

        private async Task LoadDebtorsAsync()
        {
            try
            {
                if (File.Exists(debtorFilePath))
                {
                    string json = await File.ReadAllTextAsync(debtorFilePath);
                    var loaded = JsonSerializer.Deserialize<List<Debtor>>(json);

                    if (loaded != null)
                    {
                        AllDebtors = new ObservableCollection<Debtor>(loaded); // не фильтруем
                        RefreshGrid();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке должников: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshGrid(string filter = "")
        {
            try
            {
                var filtered = string.IsNullOrWhiteSpace(filter)
                    ? AllDebtors
                    : new ObservableCollection<Debtor>(AllDebtors.Where(d =>
                        (!string.IsNullOrEmpty(d.Name) && d.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(d.InvoiceFilePath) &&
                         Path.GetFileName(d.InvoiceFilePath).Contains(filter, StringComparison.OrdinalIgnoreCase))));

                var displayList = filtered.Select(d => new DebtorDisplay
                {
                    Name = d.Name,
                    FileName = Path.GetFileName(d.InvoiceFilePath),
                    FullDebtor = d
                }).ToList();

                InvoicesGrid.ItemsSource = displayList;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при обновлении таблицы:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshGrid(SearchBox.Text.Trim());
        }

        private void OpenInvoice_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DebtorDisplay row && !string.IsNullOrWhiteSpace(row.FullDebtor.InvoiceFilePath))
            {
                try
                {
                    if (File.Exists(row.FullDebtor.InvoiceFilePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = row.FullDebtor.InvoiceFilePath,
                            UseShellExecute = true
                        });

                        AuditLogger.Log($"Открыт инвойс '{row.FileName}' для клиента {row.Name}");
                    }
                    else
                    {
                        MessageBox.Show("Файл не найден. Возможно, он был перемещён или удалён.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при открытии инвойса:\n" + ex.Message,
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ReplaceInvoice_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DebtorDisplay row && row.FullDebtor is Debtor debtor)
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "PDF файлы (*.pdf)|*.pdf"
                };

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        Directory.CreateDirectory(invoicesDir);
                        string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(dialog.FileName)}";
                        string targetPath = Path.Combine(invoicesDir, fileName);

                        File.Copy(dialog.FileName, targetPath, true);
                        debtor.InvoiceFilePath = targetPath;

                        SaveDebtors();
                        RefreshGrid(SearchBox.Text);

                        AuditLogger.Log($"Инвойс заменён для {debtor.Name}: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка при замене файла:\n" + ex.Message,
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteInvoice_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DebtorDisplay row && row.FullDebtor is Debtor debtor)
            {
                if (MessageBox.Show($"Удалить инвойс для клиента {debtor.Name}?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        debtor.InvoiceFilePath = string.Empty;
                        SaveDebtors();
                        RefreshGrid(SearchBox.Text);

                        AuditLogger.Log($"Инвойс удалён у клиента: {debtor.Name}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка при удалении:\n" + ex.Message,
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SaveDebtors()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(debtorFilePath));
                string json = JsonSerializer.Serialize(AllDebtors, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(debtorFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении данных:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InvoicesGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualParent<ScrollViewer>(this);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
