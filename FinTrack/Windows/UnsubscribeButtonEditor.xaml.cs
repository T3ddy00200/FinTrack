using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace FinTrack.Windows
{
    public partial class UnsubscribeButtonEditor : Window
    {
        public string ButtonText { get; private set; } = "Отписаться от рассылки";
        public string BgColor { get; private set; } = "#f44336";
        public string TextColor { get; private set; } = "#ffffff";
        public string Radius { get; private set; } = "4";

        public UnsubscribeButtonEditor()
        {
            InitializeComponent();
            ContentRendered += (_, _) => UpdatePreview(); // гарантируем, что элементы уже инициализированы
        }

        private void PickBgColor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Forms.ColorDialog();
            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                BgColor = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                UpdatePreview();
            }
        }

        private void PickTextColor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Forms.ColorDialog();
            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                TextColor = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                UpdatePreview();
            }
        }

        private void AnyChanged(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (PreviewButton == null) return;

            PreviewButton.Content = ButtonTextBox.Text.Trim();

            try
            {
                PreviewButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(BgColor);
                PreviewButton.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(TextColor);
            }
            catch { /* Игнорируем ошибки преобразования */ }

            if (int.TryParse(RadiusBox.Text.Trim(), out int radius))
                PreviewButton.Tag = new CornerRadius(radius);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ButtonText = ButtonTextBox.Text.Trim();
            Radius = RadiusBox.Text.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
