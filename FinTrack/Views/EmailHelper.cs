using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using FinTrack.Models;
using System.Text.Json;

public static class EmailHelper
{
    public static void Send(string from, string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder();

        // Найти все base64 изображения в HTML
        var matches = Regex.Matches(htmlBody, @"<img[^>]+src=""data:(?<mime>image\/[a-zA-Z]+);base64,(?<data>[^""]+)""", RegexOptions.IgnoreCase);

        int index = 0;
        foreach (Match match in matches)
        {
            string mimeType = match.Groups["mime"].Value;
            string base64Data = match.Groups["data"].Value;

            byte[] imageBytes = Convert.FromBase64String(base64Data);
            string contentId = Guid.NewGuid().ToString();

            // Создаём вложение
            var image = builder.LinkedResources.Add($"image{index}.{mimeType.Split('/')[1]}", imageBytes);
            image.ContentId = contentId;

            // Заменяем в HTML src
            htmlBody = htmlBody.Replace(match.Value, match.Value.Replace(match.Groups["data"].Value, $"cid:{contentId}")
                                                               .Replace("data:" + mimeType + ";base64,", ""));
            index++;
        }

        builder.HtmlBody = htmlBody;

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        client.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        client.Authenticate(from, LoadSenderPassword(from));
        client.Send(message);
        client.Disconnect(true);
    }

    private static string LoadSenderPassword(string email)
    {
        // Пример — можно читать из sender.json
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "sender.json");

        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<EmailSenderConfig>(json);

        return cfg?.Password ?? throw new Exception("Не найден пароль отправителя.");
    }
}
