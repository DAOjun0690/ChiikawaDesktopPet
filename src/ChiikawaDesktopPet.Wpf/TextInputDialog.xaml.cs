// src/ChiikawaDesktopPet.Wpf/TextInputDialog.xaml.cs
using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ChiikawaDesktopPet.Wpf;

public partial class TextInputDialog : Window
{
    private readonly string _characterName;
    private readonly string _defaultQuote;
    private bool _isInitialized;

    public string ResultText { get; private set; } = string.Empty;
    public TextAlignment ResultAlignment { get; private set; } = TextAlignment.Center;
    public double ResultFontSize { get; private set; } = 13.0;
    public double ResultImageMaxWidth { get; private set; } = 260.0;
    public double ResultImageMaxHeight { get; private set; } = 200.0;

    public TextInputDialog(
        string characterName,
        string currentText,
        TextAlignment currentAlignment = TextAlignment.Center,
        double currentFontSize = 13.0,
        double currentImageMaxWidth = 260.0,
        double currentImageMaxHeight = 200.0)
    {
        InitializeComponent();
        _characterName = characterName;
        _defaultQuote = CharacterQuotes.GetDefaultQuote(characterName);

        string displayName = App.GetCharacterDisplayName(characterName);
        Title = $"設定【{displayName}】對話文字";
        PromptLabel.Text = $"請輸入【{displayName}】頭頂對話框要顯示的文字（支援 Markdown 與圖片語法）：";

        InputTextBox.Text = currentText;
        ResultAlignment = currentAlignment;
        AlignLeftRadio.IsChecked = currentAlignment == TextAlignment.Left;
        AlignCenterRadio.IsChecked = currentAlignment == TextAlignment.Center;
        AlignRightRadio.IsChecked = currentAlignment == TextAlignment.Right;

        FontSizeSlider.Value = Math.Clamp(currentFontSize, 9.0, 36.0);
        ResultFontSize = FontSizeSlider.Value;

        ImageWidthSlider.Value = Math.Clamp(currentImageMaxWidth, 100.0, 360.0);
        ImageHeightSlider.Value = Math.Clamp(currentImageMaxHeight, 80.0, 300.0);
        ResultImageMaxWidth = ImageWidthSlider.Value;
        ResultImageMaxHeight = ImageHeightSlider.Value;

        _isInitialized = true;
        UpdatePreview();

        InputTextBox.SelectAll();
        Loaded += (_, _) => InputTextBox.Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.DisableMinimizeButton(hwnd);
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
            Activate();
        }
    }

    private TextAlignment GetSelectedAlignment()
    {
        if (AlignLeftRadio.IsChecked == true) return TextAlignment.Left;
        if (AlignRightRadio.IsChecked == true) return TextAlignment.Right;
        return TextAlignment.Center;
    }

    private void UpdatePreview()
    {
        if (!_isInitialized || PreviewBubbleText == null || PreviewBubbleBorder == null) return;

        var alignment = GetSelectedAlignment();
        double fontSize = FontSizeSlider.Value;
        double maxW = ImageWidthSlider.Value;
        double maxH = ImageHeightSlider.Value;

        PreviewBubbleBorder.MaxWidth = Math.Max(280, maxW + 40);

        string text = InputTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            PreviewBubbleText.Inlines.Clear();
            PreviewBubbleText.Inlines.Add(new Run("(無內容)")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontStyle = FontStyles.Italic
            });
            return;
        }

        MarkdownBubbleRenderer.Render(
            PreviewBubbleText,
            text,
            fontSize,
            alignment,
            maxW,
            maxH,
            onImageLoaded: null,
            onCheckboxToggled: OnPreviewCheckboxToggled);
    }

    private void OnPreviewCheckboxToggled(int index)
    {
        string currentText = InputTextBox.Text;
        string newText = MarkdownBubbleRenderer.ToggleCheckboxAt(currentText, index);
        if (newText != currentText)
        {
            int selStart = InputTextBox.SelectionStart;
            int selLength = InputTextBox.SelectionLength;
            InputTextBox.Text = newText;
            InputTextBox.SelectionStart = Math.Min(selStart, newText.Length);
            InputTextBox.SelectionLength = Math.Min(selLength, newText.Length - InputTextBox.SelectionStart);
        }
    }

    private void OnInputTextBoxTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void OnAlignmentChanged(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void OnFontSizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePreview();
    }

    private void OnImageDimensionsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePreview();
    }

    private void OnInputTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            OnOkClicked(sender, e);
        }
    }

    private void OnBoldClicked(object sender, RoutedEventArgs e) => WrapSelection("**", "**", "粗體文字");
    private void OnItalicClicked(object sender, RoutedEventArgs e) => WrapSelection("*", "*", "斜體文字");
    private void OnStrikethroughClicked(object sender, RoutedEventArgs e) => WrapSelection("~~", "~~", "刪除線文字");
    private void OnHeadingClicked(object sender, RoutedEventArgs e) => InsertLinePrefix("### ");
    private void OnListClicked(object sender, RoutedEventArgs e) => InsertLinePrefix("- ");
    private void OnTaskListClicked(object sender, RoutedEventArgs e) => InsertLinePrefix("- [ ] ");
    private void OnQuoteClicked(object sender, RoutedEventArgs e) => InsertLinePrefix("> ");
    private void OnCodeClicked(object sender, RoutedEventArgs e) => WrapSelection("`", "`", "代碼");

    private void OnInsertLinkClicked(object sender, RoutedEventArgs e)
    {
        string title = InputTextBox.SelectionLength > 0 ? InputTextBox.SelectedText : "連結文字";
        InsertTextAtCursor($"[{title}](https://)");
    }

    private void OnInsertLocalImageClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "選取本機圖片",
            Filter = "圖片檔案 (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|所有檔案 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            string path = dialog.FileName;
            string alt = Path.GetFileNameWithoutExtension(path);
            string formattedPath = path.Contains(' ') ? $"<{path}>" : path;
            InsertTextAtCursor($"![{alt}]({formattedPath})");
        }
    }

    private void OnInsertWebImageClicked(object sender, RoutedEventArgs e)
    {
        InsertTextAtCursor("![圖片說明](https://example.com/image.png)");
    }

    private void WrapSelection(string prefix, string suffix, string fallbackText)
    {
        int selStart = InputTextBox.SelectionStart;
        int selLen = InputTextBox.SelectionLength;
        string selectedText = InputTextBox.SelectedText;

        if (selLen > 0)
        {
            InputTextBox.SelectedText = prefix + selectedText + suffix;
            InputTextBox.SelectionStart = selStart + prefix.Length;
            InputTextBox.SelectionLength = selectedText.Length;
        }
        else
        {
            InputTextBox.SelectedText = prefix + fallbackText + suffix;
            InputTextBox.SelectionStart = selStart + prefix.Length;
            InputTextBox.SelectionLength = fallbackText.Length;
        }
        InputTextBox.Focus();
    }

    private void InsertLinePrefix(string prefix)
    {
        int selStart = InputTextBox.SelectionStart;
        int selLen = InputTextBox.SelectionLength;
        string text = InputTextBox.Text;

        if (selLen > 0)
        {
            string selectedText = InputTextBox.SelectedText;
            var lines = selectedText.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = prefix + lines[i].TrimStart();
            }
            string replacement = string.Join('\n', lines);
            InputTextBox.SelectedText = replacement;
            InputTextBox.SelectionStart = selStart;
            InputTextBox.SelectionLength = replacement.Length;
        }
        else
        {
            int lineStart = text.LastIndexOf('\n', Math.Max(0, selStart - 1));
            int insertPos = lineStart < 0 ? 0 : lineStart + 1;

            InputTextBox.Text = text.Insert(insertPos, prefix);
            InputTextBox.SelectionStart = selStart + prefix.Length;
            InputTextBox.SelectionLength = 0;
        }
        InputTextBox.Focus();
    }

    private void InsertTextAtCursor(string text)
    {
        int selStart = InputTextBox.SelectionStart;
        InputTextBox.SelectedText = text;
        InputTextBox.SelectionStart = selStart + text.Length;
        InputTextBox.SelectionLength = 0;
        InputTextBox.Focus();
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        InputTextBox.Text = _defaultQuote;
        AlignCenterRadio.IsChecked = true;
        FontSizeSlider.Value = 13.0;
        ImageWidthSlider.Value = 260.0;
        ImageHeightSlider.Value = 200.0;
        InputTextBox.SelectAll();
        InputTextBox.Focus();
        UpdatePreview();
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        string text = InputTextBox.Text.Trim();
        ResultText = text;
        ResultFontSize = FontSizeSlider.Value;
        ResultAlignment = GetSelectedAlignment();
        ResultImageMaxWidth = ImageWidthSlider.Value;
        ResultImageMaxHeight = ImageHeightSlider.Value;

        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
