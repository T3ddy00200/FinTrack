using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;               // для OpenFileDialog
using Forms = System.Windows.Forms;   // для ColorDialog
using MessageBox = System.Windows.MessageBox;
using MSHTML;
using FinTrack.Models;
using System.Net.NetworkInformation;
using System.Text.Json;
using FinTrack.Views;

namespace FinTrack.Windows
{
    public partial class MarketingWindow : Window
    {
        // --- 1) поля класса ---
        private readonly string _templatePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "FinTrack", "marketing_template.html");
        private string _originalHtml;
        private string _bgColor = "#FFFFFF";
        private string _bgImageUrl = null;

        public MarketingWindow()
        {
            InitializeComponent();
            Loaded += MarketingWindow_Loaded;
        }

        // --- 2) OnLoaded & инициализация ---
        private void MarketingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // читаем сохранённый шаблон
            _originalHtml = File.Exists(_templatePath)
                ? File.ReadAllText(_templatePath, Encoding.UTF8)
                : "<p>Начните вводить текст…</p>";

            ReloadEditor();

            // заполняем список шрифтов
            foreach (var ff in System.Windows.Media.Fonts.SystemFontFamilies)
                FontFamilyBox.Items.Add(ff.Source);
            FontFamilyBox.SelectedItem = "Segoe UI";
        }

        // --- 3) перезагрузка редактора с учётом фона ---
        private void ReloadEditor()
        {
            var html = WrapHtml(_originalHtml, _bgColor);
            EditorBrowser.NavigateToString(html);

            EditorBrowser.LoadCompleted += (s, e) =>
            {
                try
                {
                    var doc = EditorBrowser.Document as IHTMLDocument2;
                    if (doc != null)
                    {
                        doc.designMode = "On";

                        var timer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(150)
                        };
                        timer.Tick += (_, _) =>
                        {
                            timer.Stop();
                            try
                            {
                                if (doc.body != null)
                                {
                                    doc.body.innerHTML = _originalHtml;
                                    doc.body.style.setAttribute("cssText", $"background-color:{_bgColor};");
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Ошибка при установке HTML: " + ex.Message);
                            }
                        };
                        timer.Start();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при активации редактора: " + ex.Message);
                }
            };
        }

        // --- 4) обёртка execCommand и доступ к DOM ---
        private dynamic Document => EditorBrowser.Document;
        private void Exec(string cmd, object value = null)
        {
            try
            {
                var doc = EditorBrowser.Document as IHTMLDocument2;
                doc?.execCommand(cmd, false, value);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка команды '" + cmd + "': " + ex.Message);
            }
        }



        // --- 5) чтение/запись body HTML ---
        private void SetBodyHtml(string html)
        {
            try
            {
                dynamic doc = EditorBrowser.Document;
                dynamic body = doc?.body;

                if (body != null)
                {
                    body.innerHTML = html;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при установке HTML: " + ex.Message);
            }
        }

        private string GetBodyHtml()
        {
            var doc = EditorBrowser.Document as IHTMLDocument2;
            return doc?.body?.innerHTML ?? "";
        }


        // --- 6) кнопки форматирования ---
        private void Bold_Click(object s, RoutedEventArgs e) => Exec("Bold");
        private void Italic_Click(object s, RoutedEventArgs e) => Exec("Italic");
        private void Underline_Click(object s, RoutedEventArgs e) => Exec("Underline");
        private void Center_Click(object s, RoutedEventArgs e) => Exec("justifyCenter");
        private void Left_Click(object s, RoutedEventArgs e) => Exec("justifyLeft");

        private void FontFamilyBox_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (FontFamilyBox.SelectedItem is string fam)
                Exec("fontName", fam);
        }

        private void FontSizeBox_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (!int.TryParse(FontSizeBox.Text, out int px))
                return;

            try
            {
                var doc2 = EditorBrowser.Document as IHTMLDocument2;
                var doc3 = EditorBrowser.Document as IHTMLDocument3;

                if (doc2 == null || doc3 == null)
                    return;

                // Применяем временный размер
                doc2.execCommand("styleWithCSS", false, true);
                doc2.execCommand("fontSize", false, "7");

                var fonts = doc3.getElementsByTagName("font");

                for (int i = fonts.length - 1; i >= 0; i--)
                {
                    var obj = fonts.item(i);
                    if (obj is IHTMLElement fontElement)
                    {
                        var sizeAttr = fontElement.getAttribute("size");
                        if (sizeAttr != null && sizeAttr.ToString() == "7")
                        {
                            var span = doc2.createElement("span") as IHTMLElement;
                            if (span == null) continue;

                            span.innerHTML = fontElement.innerHTML;
                            span.style.fontSize = $"{px}px";

                            var fontNode = fontElement as IHTMLDOMNode;
                            var spanNode = span as IHTMLDOMNode;
                            var parentNode = fontElement.parentElement as IHTMLDOMNode;

                            parentNode?.replaceChild(spanNode, fontNode);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка изменения размера шрифта: " + ex.Message);
            }
        }


        // --- 7) цвета текста/заливки ---
        private void PickTextColor_Click(object s, RoutedEventArgs e)
        {
            var dlg = new Forms.ColorDialog();
            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                Exec("ForeColor", hex);
            }
        }

        private void PickHighlightColor_Click(object s, RoutedEventArgs e)
        {
            var dlg = new Forms.ColorDialog();
            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                Exec("hiliteColor", hex);
            }
        }

        // --- 8) фон редактора ---
        private void PickEditorBgColor_Click(object s, RoutedEventArgs e)
        {
            var dlg = new Forms.ColorDialog();
            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                _bgColor = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                ReloadEditor();
            }
        }
        private void InsertUnsubscribeButton_Click(object s, RoutedEventArgs e)
        {
            var dialog = new UnsubscribeButtonEditor
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                string buttonHtml = $@"
<div style=""text-align:center;margin-top:20px;margin-bottom:20px;"">
  <a href=""https://d54c-2a01-4f8-c17-57de-00-1.ngrok-free.app/unsubscribe-click?data={{UnsubscribeToken}}""
     style=""display:inline-block;
            padding:12px 24px;
            background-color:{dialog.BgColor};
            color:{dialog.TextColor};
            text-decoration:none;
            border-radius:{dialog.Radius}px;
            font-weight:bold;
            font-family:Segoe UI, Arial, sans-serif;"">
     {dialog.ButtonText}
  </a>
</div>";

                InsertHtml(buttonHtml);
            }
        }

        private void InsertHtml(string html)
        {
            try
            {
                var doc = EditorBrowser.Document as IHTMLDocument2;
                var sel = doc?.selection as IHTMLSelectionObject;

                if (sel != null && sel.type == "Text")
                {
                    var range = sel.createRange() as IHTMLTxtRange;
                    range?.pasteHTML(html);
                }
                else
                {
                    MessageBox.Show("Выделите место для вставки HTML.", "Нет выделения",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка вставки HTML: " + ex.Message);
            }
        }

        public static class AppState
        {
            public static Debtor? CurrentDebtor { get; set; }
        }

        private void PickEditorBgImage_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };
            if (dlg.ShowDialog() == true)
            {
                _bgImageUrl = new Uri(dlg.FileName).AbsoluteUri;
                ReloadEditor();
            }
        }

        private void ClearEditorBg_Click(object s, RoutedEventArgs e)
        {
            _bgColor = "#FFFFFF";
            _bgImageUrl = null;
            ReloadEditor();
        }


        // --- 9) вставка картинки внутрь текста ---
        private void InsertImage_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string mime = GetMimeType(dlg.FileName);
                    byte[] bytes = File.ReadAllBytes(dlg.FileName);
                    string base64 = Convert.ToBase64String(bytes);
                    string imgHtml = $"<img src=\"data:{mime};base64,{base64}\" style='max-width:100%;height:auto;' />";

                    var doc = EditorBrowser.Document as IHTMLDocument2;
                    if (doc != null && doc.body != null)
                    {
                        string current = doc.body.innerHTML;
                        doc.body.innerHTML = current + "<br/>" + imgHtml;
                    }
                    else
                    {
                        MessageBox.Show("Документ ещё не готов.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка вставки изображения: " + ex.Message);
                }
            }
        }

        private string GetMimeType(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };
        }

        // --- 10) сохранение / отмена ---
        private void Save_Click(object s, RoutedEventArgs e)
        {
            _originalHtml = GetBodyHtml();

            string wrappedHtml = WrapHtml(GetBodyHtml(), _bgColor);

            Directory.CreateDirectory(Path.GetDirectoryName(_templatePath)!);
            File.WriteAllText(_templatePath, wrappedHtml, Encoding.UTF8);

            MessageBox.Show("Шаблон сохранён вместе с фоном.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancel_Click(object s, RoutedEventArgs e)
        {
            ReloadEditor(); // просто перезагружаем оригинальный HTML
        }

        // --- 11) Preview desktop / mobile ---
        private void Preview_Click(object s, RoutedEventArgs e)
        {
            MainTab.SelectedIndex = 1;
            DesktopView_Click(null, null);
        }

        private void DesktopView_Click(object s, RoutedEventArgs e)
        {
            PreviewBrowser.Width = 800;
            PreviewBrowser.NavigateToString(WrapHtml(GetBodyHtml(), _bgColor));
        }

        private void MobileView_Click(object s, RoutedEventArgs e)
        {
            PreviewBrowser.Width = 375;
            PreviewBrowser.NavigateToString(WrapHtml(GetBodyHtml(), _bgColor));
        }

        // --- 12) универсальный генератор HTML-страницы ---
        private static string WrapHtml(string body, string bgColor)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
</head>
<body contenteditable='true' spellcheck='false' 
      style=""margin:0;padding:8px;font-family:Segoe UI, Arial, sans-serif;background-color:{bgColor};"">
{body}
<script>
  var shiftDown = false;

  document.addEventListener('keydown', function(e) {{
    if (e.key === 'Shift' || e.key === 'Control') shiftDown = true;
  }});
  document.addEventListener('keyup', function(e) {{
    if (e.key === 'Shift' || e.key === 'Control') shiftDown = false;
  }});

  document.addEventListener('mousedown', function(e) {{
    if (e.target.tagName === 'IMG') {{
      var img = e.target;
      var aspect = img.offsetWidth / img.offsetHeight;
      var startX = e.clientX;
      var startY = e.clientY;
      var startW = img.offsetWidth;
      var startH = img.offsetHeight;
      var hasMoved = false;

      function mousemove(ev) {{
        var dx = ev.clientX - startX;
        var dy = ev.clientY - startY;

        if (!hasMoved && (Math.abs(dx) > 5 || Math.abs(dy) > 5)) {{
          hasMoved = true;
        }}

        if (!hasMoved) return;

        var newWidth = startW + dx;
        var newHeight = shiftDown
          ? newWidth / aspect
          : startH + dy;

        img.style.width = newWidth + 'px';
        img.style.height = newHeight + 'px';
      }}

      function mouseup() {{
        document.removeEventListener('mousemove', mousemove);
        document.removeEventListener('mouseup', mouseup);
      }}

      document.addEventListener('mousemove', mousemove);
      document.addEventListener('mouseup', mouseup);
    }}
  }});
</script>
</body>
</html>";
        }

    }
}
