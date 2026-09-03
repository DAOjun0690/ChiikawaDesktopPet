// src/ChiikawaDesktopPet.Wpf/OpacityInputDialog.xaml.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChiikawaDesktopPet.Wpf;

public partial class OpacityInputDialog : Window
{
    private readonly string _characterName;
    private bool _isUpdatingInternally;

    public const double MinPercentage = 10.0;
    public const double MaxPercentage = 100.0;
    public const double DefaultPercentage = 100.0;

    public double ResultOpacity { get; private set; } = 1.0;
    public bool ResultSyncBubble { get; private set; } = true;

    public event Action<double, bool>? PreviewChanged;

    public OpacityInputDialog(string characterName, double currentOpacity = 1.0, bool currentSyncBubble = true)
    {
        InitializeComponent();
        _characterName = characterName;

        string displayName = App.GetCharacterDisplayName(characterName);
        Title = $"設定【{displayName}】透明度";
        PromptLabel.Text = $"請調整【{displayName}】的顯示透明度（{MinPercentage:F0}% ~ {MaxPercentage:F0}%）：";

        double initialPercent = Math.Clamp(currentOpacity * 100.0, MinPercentage, MaxPercentage);
        OpacitySlider.Value = initialPercent;
        OpacityTextBox.Text = Math.Round(initialPercent).ToString(CultureInfo.InvariantCulture);
        ResultOpacity = initialPercent / 100.0;

        SyncBubbleCheckBox.IsChecked = currentSyncBubble;
        ResultSyncBubble = currentSyncBubble;

        Loaded += (_, _) =>
        {
            OpacityTextBox.Focus();
            OpacityTextBox.SelectAll();
        };
    }

    private void NotifyPreview()
    {
        if (_isUpdatingInternally || OpacitySlider == null || SyncBubbleCheckBox == null) return;
        ResultOpacity = Math.Clamp(OpacitySlider.Value / 100.0, MinPercentage / 100.0, 1.0);
        ResultSyncBubble = SyncBubbleCheckBox.IsChecked == true;
        PreviewChanged?.Invoke(ResultOpacity, ResultSyncBubble);
    }

    private void OnOpacitySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingInternally || OpacityTextBox == null) return;

        _isUpdatingInternally = true;
        OpacityTextBox.Text = Math.Round(e.NewValue).ToString(CultureInfo.InvariantCulture);
        _isUpdatingInternally = false;

        NotifyPreview();
    }

    private void OnOpacityTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInternally || OpacitySlider == null) return;

        if (double.TryParse(OpacityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            _isUpdatingInternally = true;
            OpacitySlider.Value = Math.Clamp(value, MinPercentage, MaxPercentage);
            _isUpdatingInternally = false;
            NotifyPreview();
        }
    }

    private void OnOpacityTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        NormalizeTextBoxValue();
    }

    private void OnOpacityTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnOkClicked(sender, e);
        }
    }

    private void OnPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && double.TryParse(tagStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
        {
            _isUpdatingInternally = true;
            OpacitySlider.Value = Math.Clamp(val, MinPercentage, MaxPercentage);
            OpacityTextBox.Text = Math.Round(OpacitySlider.Value).ToString(CultureInfo.InvariantCulture);
            _isUpdatingInternally = false;
            NotifyPreview();
        }
    }

    private void OnSyncBubbleChanged(object sender, RoutedEventArgs e)
    {
        NotifyPreview();
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _isUpdatingInternally = true;
        OpacitySlider.Value = DefaultPercentage;
        OpacityTextBox.Text = DefaultPercentage.ToString("F0", CultureInfo.InvariantCulture);
        SyncBubbleCheckBox.IsChecked = true;
        _isUpdatingInternally = false;

        NotifyPreview();
        OpacityTextBox.Focus();
        OpacityTextBox.SelectAll();
    }

    private void NormalizeTextBoxValue()
    {
        if (!double.TryParse(OpacityTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            value = OpacitySlider.Value;
        }
        value = Math.Clamp(value, MinPercentage, MaxPercentage);

        _isUpdatingInternally = true;
        OpacitySlider.Value = value;
        OpacityTextBox.Text = Math.Round(value).ToString(CultureInfo.InvariantCulture);
        _isUpdatingInternally = false;
        NotifyPreview();
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        NormalizeTextBoxValue();
        ResultOpacity = Math.Clamp(OpacitySlider.Value / 100.0, MinPercentage / 100.0, 1.0);
        ResultSyncBubble = SyncBubbleCheckBox.IsChecked == true;
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
