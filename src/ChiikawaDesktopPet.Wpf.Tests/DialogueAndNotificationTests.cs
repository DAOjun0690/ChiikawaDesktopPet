// src/ChiikawaDesktopPet.Wpf.Tests/DialogueAndNotificationTests.cs
using System;
using System.IO;
using System.Threading;
using System.Windows;
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class DialogueAndNotificationTests
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
    public void EnableWindowsNotifications_DefaultValue_IsFalse()
    {
        Assert.False(App.EnableWindowsNotifications);
    }

    [Fact]
    public void CharacterWindow_InitialDialogueState_HasNoCustomText()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("poro");
            Assert.False(window.HasCustomText);
            Assert.Equal(string.Empty, window.CurrentDialogueText);
            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_SetCustomText_UpdatesDialogueAndHasCustomText()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("poro");
            window.SetCustomText("自訂文字測試");
            Assert.True(window.HasCustomText);
            Assert.Equal("自訂文字測試", window.CurrentDialogueText);

            window.SetCustomText("   ");
            Assert.False(window.HasCustomText);
            Assert.Equal(string.Empty, window.CurrentDialogueText);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_ResetToDefaultQuote_LoadsDefaultQuote()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("poro");
            window.ResetToDefaultQuote();
            Assert.True(window.HasCustomText);
            Assert.Equal(CharacterQuotes.GetDefaultQuote("poro"), window.CurrentDialogueText);
            window.Close();
        });
    }

    [Fact]
    public void TextInputDialog_EmptyText_LeavesResultTextEmpty()
    {
        RunInSta(() =>
        {
            var dialog = new TextInputDialog("poro", "");
            Assert.Equal(string.Empty, dialog.InputTextBox.Text);
            dialog.Close();
        });
    }

    [Fact]
    public void UpdateBubblePlacement_BidirectionalFlipping_FlipsToBottomWhenNearTopAndFlipsBackToTopWhenLower()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("poro");
            window.SetCustomText("測試氣泡位置翻轉");
            window.BubbleContainer.Visibility = Visibility.Visible;

            double bubbleH = 150;
            // When charHeadTop is 20 (near the top of the screen), spaceAbove is 20 < 160 and spaceBelow is large:
            double deltaYDown = window.UpdateBubblePlacement(bubbleH, explicitCharHeadTop: 20);
            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Bottom, window.CurrentBubblePlacement);
            Assert.Equal(bubbleH, deltaYDown);

            // When charHeadTop is 600 (middle-lower screen), spaceAbove (600) >= 160:
            double deltaYUp = window.UpdateBubblePlacement(bubbleH, explicitCharHeadTop: 600);
            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Top, window.CurrentBubblePlacement);
            Assert.Equal(-bubbleH, deltaYUp);

            // When charHeadTop is near taskbar (e.g. 950 on 1040 work area), spaceBelow < spaceAbove:
            double deltaYTaskbar = window.UpdateBubblePlacement(bubbleH, explicitCharHeadTop: 950);
            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Top, window.CurrentBubblePlacement);
            Assert.Equal(0, deltaYTaskbar);

            window.Close();
        });
    }
}
