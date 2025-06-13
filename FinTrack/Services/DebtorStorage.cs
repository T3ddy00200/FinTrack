using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using FinTrack.Models;
using FinTrack.Views;

namespace FinTrack.Services
{
    public static class DebtorStorage
    {
        public static List<Debtor> Load(string path)
        {
            if (!File.Exists(path))
                return new List<Debtor>();

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<Debtor>>(json) ?? new List<Debtor>();
            }
            catch
            {
                return new List<Debtor>();
            }
        }

        public static void Save(string path, IEnumerable<Debtor> debtors, bool createBackup = true)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                if (createBackup && File.Exists(path))
                {
                    var backupPath = path + ".bak";
                    File.Copy(path, backupPath, overwrite: true);
                }

                var json = JsonSerializer.Serialize(debtors, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении:\n" + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
