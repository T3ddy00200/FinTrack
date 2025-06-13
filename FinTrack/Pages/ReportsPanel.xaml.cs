using FinTrack.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using FinTrack.Services;
using FinTrack.Views;
using Microsoft.Win32;  // Добавьте в начало файла

namespace FinTrack.Pages
{
    public partial class ReportsPanel : UserControl
    {
        private readonly string localFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json"
        );

        private ObservableCollection<Debtor> allDebtors = new();
        private List<string> allCompanyNames = new();

        public ReportsPanel()
        {
            InitializeComponent();
            LoadDebtors();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) { }

        private void SelectAllCompanies_Click(object sender, RoutedEventArgs e)
        {
            CompaniesListBox.SelectAll();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim().ToLower();
            CompaniesListBox.Items.Clear();

            var filtered = allCompanyNames
                .Where(name => name.ToLower().Contains(query))
                .OrderBy(name => name);

            foreach (var name in filtered)
                CompaniesListBox.Items.Add(name);
        }

        private void LoadDebtors()
        {
            if (!File.Exists(localFilePath)) return;

            try
            {
                string json = File.ReadAllText(localFilePath);
                var loaded = JsonSerializer.Deserialize<ObservableCollection<Debtor>>(json);
                if (loaded == null) return;

                allDebtors = loaded;

                allCompanyNames = allDebtors
                    .Select(d => d.Name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                CompaniesListBox.Items.Clear();
                foreach (var name in allCompanyNames)
                    CompaniesListBox.Items.Add(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки должников: " + ex.Message);
            }
        }

        private List<Debtor> GetSelectedDebtors()
        {
            var selectedCompanies = CompaniesListBox.SelectedItems.Cast<string>().ToList();
            return allDebtors
                .Where(d => selectedCompanies.Contains(d.Name?.Trim()))
                .ToList();
        }

       private void ExportToExcel_Click(object sender, RoutedEventArgs e)
{
    AuditLogger.Log("ExportToExcel_Click: старт экспорта отчёта в Excel");
    var selected = GetSelectedDebtors();
    if (!selected.Any())
    {
        MessageBox.Show("Выберите хотя бы одну компанию.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        AuditLogger.Log("ExportToExcel_Click: отменено — не выбраны компании");
        return;
    }

    try
    {
        // 1) Диалог сохранения
        var saveDlg = new SaveFileDialog
        {
            Title = "Сохранить отчёт как",
            FileName = $"Отчёт_{DateTime.Now:yyyyMMdd_HHmm}",     // имя файла по умолчанию
            DefaultExt = ".xlsx",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*"
        };
        bool? result = saveDlg.ShowDialog();
        if (result != true)
        {
            AuditLogger.Log("ExportToExcel_Click: отменено пользователем в диалоге сохранения");
            return;
        }
        string path = saveDlg.FileName;

        // 2) Формируем книгу и заполняем данными
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Должники");

        string[] headers = {
            "Компания", "Контактное лицо", "Email",
            "Номер инвойса", "Сумма", "Оплачено",
            "Остаток", "Дата", "Статус"
        };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        int row = 2;
        foreach (var d in selected)
        {
            ws.Cell(row, 1).Value = d.Name;
            ws.Cell(row, 2).Value = d.ContactName;
            ws.Cell(row, 3).Value = d.Email;
            ws.Cell(row, 4).Value = d.InvoiceNumber;
            ws.Cell(row, 5).Value = d.TotalDebt;
            ws.Cell(row, 6).Value = d.Paid;
            ws.Cell(row, 7).Value = d.TotalDebt - d.Paid;
            ws.Cell(row, 8).Value = d.DueDate.ToShortDateString();
            ws.Cell(row, 9).Value = d.PaymentStatus;
            row++;
        }

        // 3) Сохраняем по выбранному пути
        wb.SaveAs(path);

        AuditLogger.Log($"ExportToExcel_Click: отчёт успешно сохранён в Excel — {path}, записано {selected.Count} строк");
        MessageBox.Show($"Файл сохранён: {path}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ошибка при экспорте в Excel: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        AuditLogger.Log($"ExportToExcel_Click: ошибка экспорта — {ex.Message}");
    }
}
    }
}
