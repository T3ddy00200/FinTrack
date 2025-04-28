using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FinTrack.Models;
using FinTrack.Views;

namespace FinTrack.Windows
{
    public partial class ImportedDebtorsWindow : Window
    {
        // Коллекция, переданная из DebtorsPanel
        public ObservableCollection<Debtor> ImportedDebtors { get; }

        private readonly string invoicesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "Invoices");

        public ImportedDebtorsWindow(ObservableCollection<Debtor> imported)
        {
            InitializeComponent();
            ImportedDebtors = imported;
            DataContext = this;
        }

        // Нажали 📎 — привязать PDF к конкретному Debtor
        // ImportedDebtorsWindow.xaml.cs
        private void AttachInvoice_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not Debtor debtor) return;

            var dlg = new OpenFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                // теперь показываем компанию и контактное лицо
                Title = $"Выберите инвойс для {debtor.Name} ({debtor.ContactName})"
            };
            if (dlg.ShowDialog() != true) return;

            Directory.CreateDirectory(invoicesDir);
            var fn = Path.GetFileName(dlg.FileName);
            var target = Path.Combine(invoicesDir, $"{debtor.Name}_{fn}");
            File.Copy(dlg.FileName, target, true);
            debtor.InvoiceFilePath = target;

            ImportedGrid.Items.Refresh();
        }


        // Кнопка «Подтвердить»
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        // Кнопка «Отмена»
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
