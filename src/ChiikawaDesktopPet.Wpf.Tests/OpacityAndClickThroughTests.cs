// src/ChiikawaDesktopPet.Wpf.Tests/OpacityAndClickThroughTests.cs
using System;
using System.Threading;
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class OpacityAndClickThroughTests
{
    private static readonly object StaLock = new();

    private static void RunInSta(Action action)
    {
        lock (StaLock)
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
    }

    [Fact]
    public void SetOpacity_ClampsBetween10PercentAnd100Percent()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chiikawa", 1);

            double firedOpacity = 0;
            bool firedSync = false;
            window.OpacityChanged += (op, sync) =>
            {
                firedOpacity = op;
                firedSync = sync;
            };

            // Test normal value
            window.SetOpacity(0.5, true);
            Assert.Equal(0.5, window.PetOpacity);
            Assert.True(window.SyncBubbleOpacity);
            Assert.Equal(0.5, firedOpacity);
            Assert.True(firedSync);

            // Test below min
            window.SetOpacity(0.01, false);
            Assert.Equal(0.1, window.PetOpacity);
            Assert.False(window.SyncBubbleOpacity);
            Assert.Equal(0.1, firedOpacity);
            Assert.False(firedSync);

            // Test above max
            window.SetOpacity(1.5, true);
            Assert.Equal(1.0, window.PetOpacity);
            Assert.True(window.SyncBubbleOpacity);

            window.Close();
        });
    }

    [Fact]
    public void SetOpacity_SyncBubbleToggle_AppliesProperVisualOpacities()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("hachiware", 1);

            // Sync true: Window gets opacity
            window.SetOpacity(0.6, syncBubble: true);
            Assert.Equal(0.6, window.Opacity);
            Assert.Equal(1.0, window.SpriteImage.Opacity);
            Assert.Equal(1.0, window.BubbleContainer.Opacity);

            // Sync false: Sprite gets opacity, Window stays 1.0, Bubble stays 1.0
            window.SetOpacity(0.6, syncBubble: false);
            Assert.Equal(1.0, window.Opacity);
            Assert.Equal(0.6, window.SpriteImage.Opacity);
            Assert.Equal(1.0, window.BubbleContainer.Opacity);

            window.Close();
        });
    }

    [Fact]
    public void SetClickThrough_TogglesStateAndFiresEvent()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("usagi", 1);
            Assert.False(window.ClickThrough);

            bool? eventResult = null;
            window.ClickThroughChanged += val => eventResult = val;

            window.SetClickThrough(true);
            Assert.True(window.ClickThrough);
            Assert.True(eventResult);

            window.ToggleClickThrough();
            Assert.False(window.ClickThrough);
            Assert.False(eventResult);

            window.Close();
        });
    }

    [Fact]
    public void OpacityInputDialog_InitializationAndPreview_WorksProperly()
    {
        RunInSta(() =>
        {
            var dialog = new OpacityInputDialog("momonga", currentOpacity: 0.75, currentSyncBubble: false);

            Assert.Equal(75.0, dialog.OpacitySlider.Value);
            Assert.Equal("75", dialog.OpacityTextBox.Text);
            Assert.False(dialog.SyncBubbleCheckBox.IsChecked);
            Assert.Equal(0.75, dialog.ResultOpacity);
            Assert.False(dialog.ResultSyncBubble);

            double previewOp = 0;
            bool previewSync = true;
            dialog.PreviewChanged += (op, sync) =>
            {
                previewOp = op;
                previewSync = sync;
            };

            dialog.OpacitySlider.Value = 50.0;
            Assert.Equal(0.5, previewOp);
            Assert.False(previewSync);

            dialog.SyncBubbleCheckBox.IsChecked = true;
            Assert.True(previewSync);

            dialog.Close();
        });
    }

    [Theory]
    [InlineData(false, "全部角色啟用穿透")]
    [InlineData(true, "全部角色停用穿透")]
    public void GetToggleAllClickThroughDisplayName_ReturnsExpectedText(bool allEnabled, string expected)
    {
        Assert.Equal(expected, App.GetToggleAllClickThroughDisplayName(allEnabled));
    }

    [Fact]
    public void BatchClickThrough_StateConsistencyAndEvents_WorkCorrectly()
    {
        RunInSta(() =>
        {
            var w1 = new CharacterWindow("usagi", 1);
            var w2 = new CharacterWindow("hachiware", 1);

            Assert.False(w1.ClickThrough);
            Assert.False(w2.ClickThrough);

            var windows = new[] { w1, w2 };
            bool allEnabled = windows.All(w => w.ClickThrough);
            Assert.False(allEnabled);
            Assert.Equal("全部角色啟用穿透", App.GetToggleAllClickThroughDisplayName(allEnabled));

            // Batch enable all
            foreach (var w in windows) w.SetClickThrough(true);
            allEnabled = windows.All(w => w.ClickThrough);
            Assert.True(allEnabled);
            Assert.Equal("全部角色停用穿透", App.GetToggleAllClickThroughDisplayName(allEnabled));

            // Single window toggles off (mixed state)
            w1.SetClickThrough(false);
            allEnabled = windows.All(w => w.ClickThrough);
            Assert.False(allEnabled);
            Assert.Equal("全部角色啟用穿透", App.GetToggleAllClickThroughDisplayName(allEnabled));

            // Batch disable all
            foreach (var w in windows) w.SetClickThrough(false);
            allEnabled = windows.All(w => w.ClickThrough);
            Assert.False(allEnabled);
            Assert.Equal("全部角色啟用穿透", App.GetToggleAllClickThroughDisplayName(allEnabled));

            w1.Close();
            w2.Close();
        });
    }

    [Fact]
    public void ContextMenu_ClickThroughAndBoundsLifecycle_WorksCorrectly()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("usagi", 1);
            Assert.False(window.HasOpenContextMenu);
            Assert.False(window.IsPointInsideContextMenu(new NativeMethods.POINT { X = 100, Y = 100 }));

            // Test setting click-through true
            window.SetClickThrough(true);
            Assert.True(window.ClickThrough);

            // ContextMenu initial state
            Assert.Null(window.ContextMenu);
            Assert.False(window.HasOpenContextMenu);

            window.Close();
        });
    }
}
