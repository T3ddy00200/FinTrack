using System;
using System.IO;
using System.Text.Json.Serialization;

namespace FinTrack.Views
{
    public class Debtor
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public decimal TotalDebt { get; set; }
        public decimal Paid { get; set; } = 0;
        public DateTime DueDate { get; set; }

        public bool IsPaid => Paid >= TotalDebt;

        // Путь к PDF-инвойсу
        public string InvoiceFilePath { get; set; } = string.Empty;

        // Остаток долга
        public decimal Balance => TotalDebt - Paid;

        // Статус оплаты в текстовом виде
        public string PaymentStatus
        {
            get
            {
                if (Paid == 0) return "Не оплачено";
                if (Paid < TotalDebt) return "Частично оплачено";
                return "Оплачено";
            }
        }

        // Новые свойства для работы с PDF
        [JsonIgnore]
        public bool HasInvoice =>
            !string.IsNullOrWhiteSpace(InvoiceFilePath)
            && File.Exists(InvoiceFilePath);

        [JsonIgnore]
        public string InvoiceFileName => HasInvoice
            ? Path.GetFileName(InvoiceFilePath)
            : "(нет PDF)";
    }
}
