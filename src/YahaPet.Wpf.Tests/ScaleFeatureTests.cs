// src/YahaPet.Wpf.Tests/ScaleFeatureTests.cs
using System;
using System.Threading;
using Xunit;
using YahaPet.Wpf;

namespace YahaPet.Wpf.Tests;

public class ScaleFeatureTests
{
    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw new Exception("STA thread failed", exception);
        }
    }

    [Fact]
    public void ScaleInputDialog_Constants_AreConfiguredProperly()
    {
        Assert.Equal(20.0, ScaleInputDialog.MinPercentage);
        Assert.Equal(400.0, ScaleInputDialog.MaxPercentage);
        Assert.Equal(100.0, ScaleInputDialog.DefaultPercentage);
    }

    [Fact]
    public void CharacterWindow_ScaleRatio_DefaultIsOne()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chiikawa");
            Assert.Equal(1.0, window.ScaleRatio);
            window.Close();
        });
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(0.75, 0.75)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 1.5)]
    [InlineData(2.0, 2.0)]
    [InlineData(4.0, 4.0)]
    [InlineData(0.1, 0.2)] // Clamped to min 0.2 (20%)
    [InlineData(0.0, 0.2)] // Clamped to min 0.2
    [InlineData(5.0, 4.0)] // Clamped to max 4.0 (400%)
    [InlineData(10.0, 4.0)] // Clamped to max 4.0
    public void CharacterWindow_SetScaleRatio_ClampsToValidRange(double inputRatio, double expectedRatio)
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chiikawa");
            window.SetScaleRatio(inputRatio);
            Assert.Equal(expectedRatio, window.ScaleRatio, 3);
            window.Close();
        });
    }

    [Fact]
    public void ScaleInputDialog_InitializesWithPassedScaleRatio()
    {
        RunInSta(() =>
        {
            var dialog = new ScaleInputDialog("chiikawa", 1.5);
            Assert.Equal(1.5, dialog.ResultScaleRatio, 3);
            Assert.Equal(150.0, dialog.ScaleSlider.Value);
            Assert.Equal("150", dialog.ScaleTextBox.Text);
            dialog.Close();
        });
    }

    [Fact]
    public void ScaleInputDialog_ClampsInitialRatioOutOfBounds()
    {
        RunInSta(() =>
        {
            var dialog = new ScaleInputDialog("chiikawa", 0.05); // 5% -> clamped to 20%
            Assert.Equal(0.2, dialog.ResultScaleRatio, 3);
            Assert.Equal(20.0, dialog.ScaleSlider.Value);
            Assert.Equal("20", dialog.ScaleTextBox.Text);
            dialog.Close();
        });
    }
}

