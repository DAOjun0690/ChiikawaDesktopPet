using System;
using System.Windows;
using System.Windows.Input;

namespace YahaPet.Wpf;

public partial class TextInputDialog : Window
{
    private readonly string _characterName;
    private readonly string _defaultQuote;

    public string ResultText { get; private set; } = string.Empty;
    public TextAlignment ResultAlignment { get; private set; } = TextAlignment.Center;
    public double ResultFontSize { get; private set; } = 13.0;

    public TextInputDialog(string characterName, string currentText, TextAlignment currentAlignment = TextAlignment.Center, double currentFontSize = 13.0)
    {
        InitializeComponent();
        _characterName = characterName;
        _defaultQuote = CharacterQuotes.GetDefaultQuote(characterName);

        string displayName = App.GetCharacterDisplayName(characterName);
        Title = $"設定【{displayName}】對話文字";
        PromptLabel.Text = $"請輸入【{displayName}】頭頂對話框要顯示的文字：";

        InputTextBox.Text = currentText;
        ResultAlignment = currentAlignment;
        AlignLeftRadio.IsChecked = currentAlignment == TextAlignment.Left;
        AlignCenterRadio.IsChecked = currentAlignment == TextAlignment.Center;
        AlignRightRadio.IsChecked = currentAlignment == TextAlignment.Right;

        FontSizeSlider.Value = Math.Clamp(currentFontSize, 9.0, 36.0);
        ResultFontSize = FontSizeSlider.Value;

        InputTextBox.SelectAll();
        Loaded += (_, _) => InputTextBox.Focus();
    }

    private void OnInputTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            OnOkClicked(sender, e);
        }
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        InputTextBox.Text = _defaultQuote;
        AlignCenterRadio.IsChecked = true;
        FontSizeSlider.Value = 13.0;
        InputTextBox.SelectAll();
        InputTextBox.Focus();
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        string text = InputTextBox.Text.Trim();
        ResultText = string.IsNullOrEmpty(text) ? _defaultQuote : text;
        ResultFontSize = FontSizeSlider.Value;

        if (AlignLeftRadio.IsChecked == true)
        {
            ResultAlignment = TextAlignment.Left;
        }
        else if (AlignRightRadio.IsChecked == true)
        {
            ResultAlignment = TextAlignment.Right;
        }
        else
        {
            ResultAlignment = TextAlignment.Center;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

