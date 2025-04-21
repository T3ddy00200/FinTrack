using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using FinTrack.Models;
using FinTrack.Pages;
using FinTrack.Views;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace FinTrack.Services
{
    public static class AutoNotifier
    {
        private static readonly string senderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "sender.json");

        private static readonly string debtorsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "debtors.json");

        private static readonly string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "autosend_log.json");

        public static void TryAutoSend()
        {
            try
            {
                if (!File.Exists(senderPath) || !File.Exists(debtorsPath)) return;

                var senderJson = File.ReadAllText(senderPath);
                var senderConfig = JsonSerializer.Deserialize<EmailSenderConfig>(senderJson);

                // Загружаем конфиг автоотправки
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinTrack", "autosend_config.json");
                var autoText = "Здравствуйте! У вас есть задолженность.";

                AutoSendSettings config = null;

                if (File.Exists(configPath))
                {
                    var configJson = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<AutoSendSettings>(configJson);

                    if (config != null && !string.IsNullOrWhiteSpace(config.MessageText))
                        autoText = config.MessageText;
                }

                if (senderConfig == null ||
                    string.IsNullOrWhiteSpace(senderConfig.Email) ||
                    string.IsNullOrWhiteSpace(senderConfig.Password) ||
                    config == null ||
                    !IsAutoSendDateTime(config) ||
                    WasAlreadySentToday())
                {
                    return;
                }

                var debtorsJson = File.ReadAllText(debtorsPath);
                var debtors = JsonSerializer.Deserialize<List<Debtor>>(debtorsJson) ?? new();

                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderConfig.Email, senderConfig.Password),
                    EnableSsl = true
                };

                foreach (var debtor in debtors)
                {
                    if (debtor.PaymentStatus != "Оплачено" && debtor.DueDate < DateTime.Today)
                    {
                        string body = autoText
                            .Replace("{Name}", debtor.Name)
                            .Replace("{Debt}", (debtor.TotalDebt - debtor.Paid).ToString("0.00"));

                        var mail = new MailMessage
                        {
                            From = new MailAddress(senderConfig.Email),
                            Subject = "Просроченная задолженность",
                            Body = body
                        };

                        mail.To.Add(debtor.Email);

                        if (!string.IsNullOrEmpty(debtor.InvoiceFilePath) && File.Exists(debtor.InvoiceFilePath))
                            mail.Attachments.Add(new Attachment(debtor.InvoiceFilePath));

                        smtp.Send(mail);

                        System.Threading.Thread.Sleep(3000); // пауза 3 секунды
                    }
                }

                // Сохраняем дату последней отправки
                var log = new AutoSendLog
                {
                    DateSent = DateTime.Today
                };

                File.WriteAllText(logPath, JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                File.AppendAllText("autosend_errors.log", $"[{DateTime.Now}] {ex}\n");
            }
        }


        private static bool WasAlreadySentToday()
        {
            if (!File.Exists(logPath)) return false;
            var log = JsonSerializer.Deserialize<AutoSendLog>(File.ReadAllText(logPath));
            return log?.DateSent.Date == DateTime.Today;
        }




   private static bool IsAutoSendDateTime(AutoSendSettings config)
{
    var now = DateTime.Now;

    // убираем этот блок:
    // if (!string.IsNullOrWhiteSpace(config.ScheduledDate)) { … }

    // Оставляем только ежемесячную проверку по дню:
    if (config.ScheduledDay > 0 &&
        TimeSpan.TryParse(config.Time, out var scheduledTime))
    {
        var dayInMonth = Math.Min(config.ScheduledDay, DateTime.DaysInMonth(now.Year, now.Month));
        var scheduledDate = new DateTime(now.Year, now.Month, dayInMonth);

        return now.Date == scheduledDate.Date
               && now.TimeOfDay.Hours   == scheduledTime.Hours
               && now.TimeOfDay.Minutes >= scheduledTime.Minutes;
    }

    return false;
}






    }

    public class AutoSendLog
    {
        public DateTime DateSent { get; set; }
    }

    public class EmailSenderConfig
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string ReadPassword { get; set; }
        public string AutoNotificationText { get; set; }

       

    }
}
