using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using Xceed.Words.NET;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using FinTrack.Views;
using FinTrack.Models;
using FinTrack.Services;

namespace FinTrack.Pages
{
    public partial class ReportsPanel : UserControl
    {
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //LocalizationManager.LocalizeUI(this);
        }
        private List<string> allCompanyNames = new(); // Все компании (для фильтра)
        private void SelectAllCompanies_Click(object sender, RoutedEventArgs e)
        {
            CompaniesListBox.SelectAll();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim().ToLower();

            //string query = SearchBox.Text.Trim().ToLower();

            CompaniesListBox.Items.Clear();

            var filtered = allCompanyNames
                .Where(name => name.ToLower().Contains(query))
                .OrderBy(name => name);

            foreach (var company in filtered)
                CompaniesListBox.Items.Add(company);
        }

        private readonly string localFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json"
        );

        private ObservableCollection<Debtor> allDebtors = new();

        public ReportsPanel()
        {
            InitializeComponent();
            LoadDebtors();
        }

        private void LoadDebtors()
        {
            if (File.Exists(localFilePath))
            {
                string json = File.ReadAllText(localFilePath);
                var loaded = JsonSerializer.Deserialize<ObservableCollection<Debtor>>(json);
                if (loaded != null)
                {
                    allDebtors = loaded;

                    allCompanyNames = allDebtors
                        .Select(d => d.Name)
                        .Distinct()
                        .ToList();

                    foreach (var company in allCompanyNames.OrderBy(n => n))
                        CompaniesListBox.Items.Add(company);
                }
            }
        }


        private List<Debtor> GetSelectedDebtors()
        {
            var selectedCompanies = CompaniesListBox.SelectedItems.Cast<string>().ToList();
            return allDebtors.Where(d => selectedCompanies.Contains(d.Name)).ToList();
        }

        // 1) ExportToExcel_Click
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
                var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Должники");

                string[] headers = { "Компания", "Email", "Телефон", "Сумма", "Оплачено", "Остаток", "Дата", "Статус" };
                for (int i = 0; i < headers.Length; i++)
                    ws.Cell(1, i + 1).Value = headers[i];

                int row = 2;
                foreach (var d in selected)
                {
                    ws.Cell(row, 1).Value = d.Name;
                    ws.Cell(row, 2).Value = d.Email;
                    ws.Cell(row, 3).Value = d.Phone;
                    ws.Cell(row, 4).Value = d.TotalDebt;
                    ws.Cell(row, 5).Value = d.Paid;
                    ws.Cell(row, 6).Value = d.TotalDebt - d.Paid;
                    ws.Cell(row, 7).Value = d.DueDate.ToShortDateString();
                    ws.Cell(row, 8).Value = d.PaymentStatus;
                    row++;
                }

                string path = $"Отчёт_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                wb.SaveAs(path);

                AuditLogger.Log($"ExportToExcel_Click: отчёт успешно сохранён в Excel — {path}, записано {selected.Count} строк");
                MessageBox.Show($"Файл сохранён: {path}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Excel: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                AuditLogger.Log($"ExportToExcel_Click: ошибка экспорта в Excel — {ex.Message}");
            }
        }




        // 2) ExportToPdf_Click
        private void ExportToPdf_Click(object sender, RoutedEventArgs e)
        {
            AuditLogger.Log("ExportToPdf_Click: старт экспорта отчёта в PDF");
            var selected = GetSelectedDebtors();
            if (!selected.Any())
            {
                MessageBox.Show("Выберите хотя бы одну компанию.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                AuditLogger.Log("ExportToPdf_Click: отменено — не выбраны компании");
                return;
            }

            try
            {
                string path = $"Отчёт_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                var doc = new PdfDocument();
                var page = doc.AddPage();
                var gfx = XGraphics.FromPdfPage(page);
                var font = new XFont("Verdana", 12);

                int y = 40;
                gfx.DrawString("Отчёт по должникам", new XFont("Verdana", 16, XFontStyle.Bold), XBrushes.Black, new XPoint(40, y));
                y += 40;

                foreach (var d in selected)
                {
                    string line = $"{d.Name} — {d.TotalDebt}₽, Оплачено: {d.Paid}₽, Остаток: {d.TotalDebt - d.Paid}₽, Статус: {d.PaymentStatus}, Дата: {d.DueDate:d}";
                    gfx.DrawString(line, font, XBrushes.Black, new XPoint(40, y));
                    y += 25;
                    if (y > page.Height - 40)
                    {
                        page = doc.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        y = 40;
                    }
                }

                using (var stream = File.Create(path))
                    doc.Save(stream);

                AuditLogger.Log($"ExportToPdf_Click: отчёт успешно сохранён в PDF — {path}, записано {selected.Count} строк");
                MessageBox.Show($"Файл сохранён: {path}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в PDF: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                AuditLogger.Log($"ExportToPdf_Click: ошибка экспорта в PDF — {ex.Message}");
            }
        }

    }
}
