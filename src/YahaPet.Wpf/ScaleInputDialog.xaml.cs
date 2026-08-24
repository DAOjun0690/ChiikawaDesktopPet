// src/YahaPet.Wpf/ScaleInputDialog.xaml.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YahaPet.Wpf;

public partial class ScaleInputDialog : Window
{
    private readonly string _characterName;
    private bool _isUpdatingInternally;

    public const double MinPercentage = 20.0;
    public const double MaxPercentage = 400.0;
    public const double DefaultPercentage = 100.0;

    public double ResultScaleRatio { get; private set; } = 1.0;

    public ScaleInputDialog(string characterName, double currentScaleRatio = 1.0)
    {
        InitializeComponent();
        _characterName = characterName;

        string displayName = App.GetCharacterDisplayName(characterName);
        Title = $"調整【{displayName}】比例";
        PromptLabel.Text = $"請調整【{displayName}】的顯示比例（{MinPercentage:F0}% ~ {MaxPercentage:F0}%）：";

        double initialPercent = Math.Clamp(currentScaleRatio * 100.0, MinPercentage, MaxPercentage);
        ScaleSlider.Value = initialPercent;
        ScaleTextBox.Text = Math.Round(initialPercent).ToString(CultureInfo.InvariantCulture);
        ResultScaleRatio = initialPercent / 100.0;

        Loaded += (_, _) =>
        {
            ScaleTextBox.Focus();
            ScaleTextBox.SelectAll();
        };
    }

    private void OnScaleSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingInternally || ScaleTextBox == null) return;

        _isUpdatingInternally = true;
        ScaleTextBox.Text = Math.Round(e.NewValue).ToString(CultureInfo.InvariantCulture);
        _isUpdatingInternally = false;
    }

    private void OnScaleTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInternally || ScaleSlider == null) return;

        if (double.TryParse(ScaleTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            _isUpdatingInternally = true;
            ScaleSlider.Value = Math.Clamp(value, MinPercentage, MaxPercentage);
            _isUpdatingInternally = false;
        }
    }

    private void OnScaleTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        NormalizeTextBoxValue();
    }

    private void OnScaleTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
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
            ScaleSlider.Value = Math.Clamp(val, MinPercentage, MaxPercentage);
            ScaleTextBox.Text = Math.Round(ScaleSlider.Value).ToString(CultureInfo.InvariantCulture);
            _isUpdatingInternally = false;
        }
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _isUpdatingInternally = true;
        ScaleSlider.Value = DefaultPercentage;
        ScaleTextBox.Text = DefaultPercentage.ToString("F0", CultureInfo.InvariantCulture);
        _isUpdatingInternally = false;
        ScaleTextBox.Focus();
        ScaleTextBox.SelectAll();
    }

    private void NormalizeTextBoxValue()
    {
        if (!double.TryParse(ScaleTextBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            value = ScaleSlider.Value;
        }
        value = Math.Clamp(value, MinPercentage, MaxPercentage);

        _isUpdatingInternally = true;
        ScaleSlider.Value = value;
        ScaleTextBox.Text = Math.Round(value).ToString(CultureInfo.InvariantCulture);
        _isUpdatingInternally = false;
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        NormalizeTextBoxValue();
        ResultScaleRatio = ScaleSlider.Value / 100.0;
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

