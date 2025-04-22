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

namespace FinTrack.Services
{
    public static class AutoNotifier
    {
        private static readonly string senderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "sender.json");

        private static readonly string debtorsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "debtors.json");

        private static readonly string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "autosend_config.json");

        private static readonly string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "autosend_log.json");

        public static void TryAutoSend()
        {
            try
            {
                if (!File.Exists(senderPath) || !File.Exists(debtorsPath) || !File.Exists(configPath))
                    return;

                // Десериализуем настройки отправителя и авторассылки
                var senderConfig = JsonSerializer.Deserialize<EmailSenderConfig>(File.ReadAllText(senderPath));
                var autoCfg = JsonSerializer.Deserialize<AutoSendSettings>(File.ReadAllText(configPath));

                if (senderConfig == null
                    || string.IsNullOrWhiteSpace(senderConfig.Email)
                    || string.IsNullOrWhiteSpace(senderConfig.Password)
                    || autoCfg == null
                    || !autoCfg.Enabled
                    || WasAlreadySentToday()
                    || !IsAutoSendDateTime(autoCfg))
                {
                    return;
                }

                // Загружаем должников
                var debtors = JsonSerializer.Deserialize<List<Debtor>>(File.ReadAllText(debtorsPath)) ?? new();

                using var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderConfig.Email, senderConfig.Password),
                    EnableSsl = true
                };

                foreach (var debtor in debtors
                                  .Where(d => d.PaymentStatus != "Оплачено" && d.DueDate < DateTime.Today))
                {
                    var debtStr = (debtor.TotalDebt - debtor.Paid).ToString("0.00");

                    // Подставляем плейсхолдеры в тему и тело
                    var subject = autoCfg.SubjectTemplate
                        .Replace("{Name}", debtor.Name)
                        .Replace("{Debt}", debtStr);

                    var body = autoCfg.BodyTemplate
                        .Replace("{Name}", debtor.Name)
                        .Replace("{Debt}", debtStr);

                    var mail = new MailMessage
                    {
                        From = new MailAddress(senderConfig.Email),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = false
                    };
                    mail.To.Add(debtor.Email);

                    // Прикладываем инвойс, если он есть
                    if (!string.IsNullOrEmpty(debtor.InvoiceFilePath) && File.Exists(debtor.InvoiceFilePath))
                        mail.Attachments.Add(new Attachment(debtor.InvoiceFilePath));

                    smtp.Send(mail);

                    // Задержка между письмами
                    System.Threading.Thread.Sleep(3000);
                }

                // Фиксируем дату отправки
                File.WriteAllText(logPath,
                    JsonSerializer.Serialize(new AutoSendLog { DateSent = DateTime.Today },
                                             new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                File.AppendAllText("autosend_errors.log",
                    $"[{DateTime.Now}] Ошибка авторассылки: {ex}\n");
            }
        }

        private static bool WasAlreadySentToday()
        {
            if (!File.Exists(logPath)) return false;
            var log = JsonSerializer.Deserialize<AutoSendLog>(File.ReadAllText(logPath));
            return log?.DateSent.Date == DateTime.Today;
        }

        private static bool IsAutoSendDateTime(AutoSendSettings cfg)
        {
            var now = DateTime.Now;
            if (TimeSpan.TryParse(cfg.Time, out var t))
            {
                var day = Math.Min(cfg.ScheduledDay, DateTime.DaysInMonth(now.Year, now.Month));
                return now.Date == new DateTime(now.Year, now.Month, day)
                       && now.TimeOfDay.Hours == t.Hours
                       && now.TimeOfDay.Minutes >= t.Minutes;
            }
            return false;
        }
    }

    public class AutoSendLog
    {
        public DateTime DateSent { get; set; }
    }
}
