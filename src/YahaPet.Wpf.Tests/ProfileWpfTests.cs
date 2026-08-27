// src/YahaPet.Wpf.Tests/ProfileWpfTests.cs
using System;
using System.Threading;
using System.Windows;
using Xunit;
using YahaPet.Core;
using YahaPet.Wpf;

namespace YahaPet.Wpf.Tests;

public class ProfileWpfTests
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
    public void CharacterWindow_ToProfileItem_CapturesCurrentState()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("momonga", 2);
            window.SetCustomText("誇獎我！", TextAlignment.Right, 16.0);
            window.SetAlwaysShowBubble(true);
            window.SetScaleRatio(1.5);
            window.SetDefaultAnimation("cheer");
            window.SetRandomAnimationsEnabled(false);
            window.SetJumpEnabled(false);

            var profileItem = window.ToProfileItem();

            Assert.NotNull(profileItem);
            Assert.Equal("momonga", profileItem.CharacterName);
            Assert.Equal("誇獎我！", profileItem.DialogueText);
            Assert.Equal("Right", profileItem.DialogueAlignment);
            Assert.Equal(16.0, profileItem.DialogueFontSize);
            Assert.True(profileItem.AlwaysShowBubble);
            Assert.Equal(1.5, profileItem.ScaleRatio);
            Assert.Equal("cheer", profileItem.DefaultAnimation);
            Assert.False(profileItem.RandomAnimationsEnabled);
            Assert.False(profileItem.JumpEnabled);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_ApplyProfile_RestoresAllConfiguredProperties()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("hachiware", 1);
            var item = new CharacterProfileItem
            {
                CharacterName = "hachiware",
                DialogueText = "なんとかなれーッ！",
                DialogueAlignment = "Left",
                DialogueFontSize = 15.0,
                AlwaysShowBubble = true,
                ScaleRatio = 1.25,
                DefaultAnimation = "dance",
                RandomAnimationsEnabled = false,
                JumpEnabled = false
            };

            window.ApplyProfile(item);

            Assert.Equal("なんとかなれーッ！", window.CurrentDialogueText);
            Assert.Equal(TextAlignment.Left, window.DialogueAlignment);
            Assert.Equal(15.0, window.DialogueFontSize);
            Assert.True(window.AlwaysShowBubble);
            Assert.Equal(1.25, window.ScaleRatio);
            Assert.Equal("dance", window.DefaultAnimation);
            Assert.False(window.RandomAnimationsEnabled);
            Assert.False(window.JumpEnabled);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_ApplyProfile_WithEmptyText_ClearsDialogue()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chiikawa", 1);
            window.SetCustomText("原本有文字");

            var item = new CharacterProfileItem
            {
                CharacterName = "chiikawa",
                DialogueText = "",
                ScaleRatio = 1.0
            };

            window.ApplyProfile(item);

            Assert.False(window.HasCustomText);
            Assert.Equal(string.Empty, window.CurrentDialogueText);

            window.Close();
        });
    }
}
