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
}
