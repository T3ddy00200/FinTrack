using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FinTrack.Views
{
    public static class EmailDatabase
    {
        private static readonly string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinTrack", "email_db.json");

        private static List<DebtorEmail> _emails;

        public static IReadOnlyList<DebtorEmail> Emails => _emails;

        static EmailDatabase()
        {
            Load();
        }

        public static void Load()
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _emails = JsonSerializer.Deserialize<List<DebtorEmail>>(json) ?? new List<DebtorEmail>();
            }
            else
            {
                _emails = new List<DebtorEmail>();
            }
        }

        public static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var json = JsonSerializer.Serialize(_emails, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public static string GetEmail(string name)
        {
            return _emails.FirstOrDefault(e => e.Name.Trim() == name.Trim())?.Email;
        }
        public static void AddOrUpdate(string name, string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            var existing = _emails.FirstOrDefault(e => e.Name.Trim() == name.Trim());
            if (existing != null)
                existing.Email = email;
            else
                _emails.Add(new DebtorEmail { Name = name, Email = email });
        }


        public static void AddIfNotExists(string name, string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            if (!_emails.Any(e => e.Name.Trim() == name.Trim()))
            {
                _emails.Add(new DebtorEmail { Name = name, Email = email });
            }
        }
    }
}
