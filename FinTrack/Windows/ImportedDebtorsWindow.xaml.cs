using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FinTrack.Models;
using ClosedXML.Excel;
using FinTrack.Views;
using FinTrack.Services; // Для EmailDatabase

namespace FinTrack.Windows
{
    public partial class ImportedDebtorsWindow : Window
    {
        public ObservableCollection<Debtor> ImportedDebtors { get; }

        private readonly string invoicesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "Invoices");

        public ImportedDebtorsWindow(ObservableCollection<Debtor> imported)
        {
            InitializeComponent();
            ImportedDebtors = imported;
            DataContext = this;

            AutoFillEmails();
        }

        private void AutoFillEmails()
        {
            EmailDatabase.Load();

            foreach (var debtor in ImportedDebtors)
            {
                if (string.IsNullOrWhiteSpace(debtor.Email))
                {
                    var email = EmailDatabase.GetEmail(debtor.Name);
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        debtor.Email = email;
                    }
                }
            }

            ImportedGrid.Items.Refresh();
        }

        private void AutoAttachInvoices_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите папку, где лежат PDF-инвойсы"
            };

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            string selectedFolder = dlg.SelectedPath;
            if (!Directory.Exists(selectedFolder))
                return;

            int attached = 0;

            foreach (var debtor in ImportedDebtors)
            {
                if (string.IsNullOrWhiteSpace(debtor.InvoiceNumber))
                    continue;

                string numericInvoice = Regex.Match(debtor.InvoiceNumber, @"\d+").Value;
                if (string.IsNullOrEmpty(numericInvoice)) continue;

                var pdfFiles = Directory.GetFiles(selectedFolder, "*.pdf", SearchOption.TopDirectoryOnly);
                string matchedPdf = pdfFiles.FirstOrDefault(f =>
                {
                    string fileNameOnly = Path.GetFileNameWithoutExtension(f);
                    string digitsOnly = new string(fileNameOnly.Where(char.IsDigit).ToArray());
                    return digitsOnly == numericInvoice;
                });

                if (matchedPdf != null)
                {
                    try
                    {
                        string safeName = string.Join("_", debtor.Name.Split(Path.GetInvalidFileNameChars()));
                        string targetName = $"{safeName}_{Path.GetFileName(matchedPdf)}";
                        string targetPath = Path.Combine(invoicesDir, targetName);

                        Directory.CreateDirectory(invoicesDir);
                        File.Copy(matchedPdf, targetPath, true);
                        debtor.InvoiceFilePath = targetPath;
                        attached++;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при копировании PDF:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            ImportedGrid.Items.Refresh();

            MessageBox.Show(
                attached > 0
                    ? $"Привязано {attached} PDF-инвойсов из выбранной папки."
                    : "Инвойсы не найдены. Проверьте совпадения по номеру.",
                "Автопривязка завершена", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AttachInvoice_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not Debtor debtor)
                return;

            var dlg = new OpenFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                Title = $"Выберите инвойс для {debtor.Name}"
            };

            if (dlg.ShowDialog() != true) return;

            Directory.CreateDirectory(invoicesDir);

            string safeName = string.Join("_", debtor.Name.Split(Path.GetInvalidFileNameChars()));
            string fileName = Path.GetFileName(dlg.FileName);
            string target = Path.Combine(invoicesDir, $"{safeName}_{fileName}");

            try
            {
                File.Copy(dlg.FileName, target, true);
                debtor.InvoiceFilePath = target;
                ImportedGrid.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при копировании PDF:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AttachFromExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                Title = "Выберите Excel-файл с должниками"
            };

            if (dlg.ShowDialog() != true)
                return;

            ImportedDebtors.Clear();
            EmailDatabase.Load();

            try
            {
                using var workbook = new XLWorkbook(dlg.FileName);
                var sheet = workbook.Worksheets.First();

                for (int i = 2; i <= sheet.LastRowUsed().RowNumber(); i++)
                {
                    var row = sheet.Row(i);

                    string name = row.Cell(1).GetValue<string>();
                    string invoiceNumber = row.Cell(2).GetValue<string>();
                    DateTime dueDate = DateTime.TryParse(row.Cell(3).GetString(), out var dt) ? dt : DateTime.Today;
                    decimal.TryParse(row.Cell(4).GetString(), out var totalDebt);
                    decimal.TryParse(row.Cell(5).GetString(), out var paid);
                    string email = row.Cell(6).GetValue<string>();

                    if (string.IsNullOrWhiteSpace(email))
                    {
                        email = EmailDatabase.GetEmail(name);
                    }

                    var debtor = new Debtor
                    {
                        Name = name,
                        InvoiceNumber = invoiceNumber,
                        DueDate = dueDate,
                        TotalDebt = totalDebt,
                        Paid = paid,
                        Email = email
                    };

                    ImportedDebtors.Add(debtor);
                }

                ImportedGrid.Items.Refresh();

                MessageBox.Show($"Успешно импортировано {ImportedDebtors.Count} записей.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при импорте:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BatchAttachInvoices_Click(object sender, RoutedEventArgs e)
        {
            AutoAttachInvoices_Click(sender, e);
        }

        private void MarkAsPaid_Click(object sender, RoutedEventArgs e)
        {
            foreach (var debtor in ImportedDebtors)
            {
                if (!debtor.IsPaid)
                {
                    debtor.Paid = debtor.TotalDebt;
                }
            }

            ImportedGrid.Items.Refresh();
            MessageBox.Show("Все должники отмечены как оплаченные.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MarkSingleAsPaid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is Debtor debtor)
            {
                debtor.Paid = debtor.TotalDebt;
                ImportedGrid.Items.Refresh();
            }
        }

        private void AddEmailManually_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is Debtor debtor)
            {
                var prompt = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Введите email для {debtor.Name}:", "Добавить Email", debtor.Email ?? "");

                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    debtor.Email = prompt.Trim();
                    EmailDatabase.AddIfNotExists(debtor.Name, debtor.Email);
                    EmailDatabase.Save();
                    ImportedGrid.Items.Refresh();
                }
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            foreach (var d in ImportedDebtors)
            {
                if (!string.IsNullOrWhiteSpace(d.Email))
                {
                    EmailDatabase.AddIfNotExists(d.Name, d.Email);
                }
            }
            EmailDatabase.Save();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
