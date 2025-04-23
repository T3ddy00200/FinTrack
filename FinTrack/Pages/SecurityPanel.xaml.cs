using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FinTrack.Services;

namespace FinTrack.Controls
{
    public partial class SecurityPanel : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<LogEntry> Entries { get; } = new();

        public SecurityPanel()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLog();
        }

        private void LoadLog()
        {
            Entries.Clear();
            foreach (var line in AuditLogger.ReadAll().Reverse()) // показываем сверху свежие
            {
                var parts = line.Split('|', 2);
                if (parts.Length == 2
                 && DateTime.TryParse(parts[0].Trim(), out var ts))
                {
                    Entries.Add(new LogEntry { Timestamp = ts, Message = parts[1].Trim() });
                }
            }
            LogDataGrid.ItemsSource = Entries;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
    }
}
