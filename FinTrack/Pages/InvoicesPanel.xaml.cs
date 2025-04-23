using FinTrack.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FinTrack.Views;
using FinTrack.Services;

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
        }

        public InvoicesPanel()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LocalizationManager.LocalizeUI(this);
            LoadDebtors();
        }

        private void LoadDebtors()
        {
            if (File.Exists(debtorFilePath))
            {
                string json = File.ReadAllText(debtorFilePath);
                var loaded = JsonSerializer.Deserialize<List<Debtor>>(json);
                if (loaded != null)
                {
                    AllDebtors = new ObservableCollection<Debtor>(loaded); // без фильтра
                    RefreshGrid();
                }
            }
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
                    }
                    else
                    {
                        MessageBox.Show("Файл не найден. Возможно, он был перемещён или удалён.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                AuditLogger.Log($"Открыт инвойс '{row.FileName}' для клиента {row.Name}");
            }
        }


        private void RefreshGrid(string filter = "")
        {
            var filtered = string.IsNullOrWhiteSpace(filter)
                ? AllDebtors
                : new ObservableCollection<Debtor>(AllDebtors.Where(d =>
                    d.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(d.InvoiceFilePath).Contains(filter, StringComparison.OrdinalIgnoreCase)));

            var displayList = filtered.Select(d => new DebtorDisplay
            {
                Name = d.Name,
                FileName = Path.GetFileName(d.InvoiceFilePath),
                FullDebtor = d
            }).ToList();

            InvoicesGrid.ItemsSource = displayList;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshGrid(SearchBox.Text);
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
                    Directory.CreateDirectory(invoicesDir);
                    string newPath = Path.Combine(invoicesDir, $"{debtor.Name}_{Path.GetFileName(dialog.FileName)}");
                    File.Copy(dialog.FileName, newPath, true);
                    debtor.InvoiceFilePath = newPath;
                    SaveDebtors();
                    LoadDebtors();
                    AuditLogger.Log($"Инвойс для {debtor.Name} заменён");
                }
            }
        }

        private void DeleteInvoice_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DebtorDisplay row && row.FullDebtor is Debtor debtor)
            {
                if (MessageBox.Show($"Удалить инвойс для клиента {debtor.Name}?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    debtor.InvoiceFilePath = string.Empty;
                    SaveDebtors();
                    LoadDebtors();
                    AuditLogger.Log($"Инвойс удалён для клиента {debtor.Name}");

                }
            }
        }

        private void SaveDebtors()
        {
            string json = JsonSerializer.Serialize(AllDebtors, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(debtorFilePath, json);
        }
    }
}
