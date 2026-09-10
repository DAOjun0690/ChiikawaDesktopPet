// src/ChiikawaDesktopPet.Wpf.Tests/DialogueGroundAnchorTests.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class DialogueGroundAnchorTests
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

    private static void ConfigureIdleWindow(CharacterWindow window, double initialTop, int spriteW = 128, int spriteH = 128)
    {
        window.Show();
        window.Width = spriteW;
        window.Height = spriteH;
        window.SpriteImage.Width = spriteW;
        window.SpriteImage.Height = spriteH;
        window.GetType().GetField("_currentSpriteWidth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(window, spriteW);
        window.GetType().GetField("_currentSpriteHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(window, spriteH);
        window.GetType().GetField("_isFalling", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(window, false);
        window.Top = initialTop;
        window.Left = 300;
    }

    [Fact]
    public void SetCustomText_MultiLineChecklist_AnchorsPetFeetFirmly()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("poro");
            ConfigureIdleWindow(window, initialTop: 550, spriteW: 128, spriteH: 128);

            double topBefore = window.Top;
            double hBefore = window.Height;
            double feetBefore = window.Top + window.Height;

            string longOrderList =
                "- [ ] 六塊雞塊 A套餐 大玉米湯 \n" +
                "- [ ] 勁脆雞腿堡+E套餐 ( 大薯4雞醣醋 ) +可樂Zero \n" +
                "- [ ] 雙層四盎司牛肉堡 +大薯無糖紅)+6塊雞塊(糖醋醬) \n" +
                "- [ ] 雙層四盎司牛肉堡 + A套餐(中薯) 大玉米湯 \n" +
                "- [ ] 炭燒醬烤雞腿堡 中薯，不要番茄醬 玉米湯(小) \n" +
                "- [ ] 肯瓊勁脆雞腿堡 不要生菜(0)肯瓊醬加倍(2) 中薯 鮮奶 \n" +
                "- [ ] 煙燻勁脆雞腿堡 A套餐(中薯) 大玉米湯 ";

            window.SetCustomText(longOrderList);

            double feetAfter = window.Top + window.Height;
            Assert.Equal(Visibility.Visible, window.BubbleContainer.Visibility);
            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Top, window.CurrentBubblePlacement);
            Assert.True(window.Height > hBefore, "Window height should expand for long text");
            Assert.True(Math.Abs(feetAfter - feetBefore) < 1.0, $"Feet position drifted! feetBefore={feetBefore}, feetAfter={feetAfter}");

            // Update text while already visible
            window.SetCustomText("短文字測試");
            double feetAfterShort = window.Top + window.Height;
            Assert.True(Math.Abs(feetAfterShort - feetBefore) < 1.0, $"Feet drifted on text update! feetBefore={feetBefore}, feetAfterShort={feetAfterShort}");

            // Hide bubble: feet must still be anchored
            window.HideSpeechBubble();
            double feetAfterHide = window.Top + window.Height;
            Assert.True(Math.Abs(feetAfterHide - feetBefore) < 1.0, $"Feet drifted after hiding bubble! feetBefore={feetBefore}, feetAfterHide={feetAfterHide}");

            window.Close();
        });
    }

    [Fact]
    public void SetCustomText_NearScreenTop_PlacesBubbleBelowAndPreservesFeet()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("poro");
            // Position character close to top of screen so bubble must place below
            ConfigureIdleWindow(window, initialTop: 30, spriteW: 128, spriteH: 128);

            double feetBefore = window.Top + window.Height;

            window.SetCustomText("放在下方的氣泡對話測試");

            double feetAfter = window.Top + window.SpriteImage.Height; // In Bottom placement, sprite is row 0 so feet = Top + spriteH
            Assert.Equal(Visibility.Visible, window.BubbleContainer.Visibility);
            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Bottom, window.CurrentBubblePlacement);
            Assert.True(Math.Abs(feetAfter - feetBefore) < 1.0, $"Feet drifted when placed below! feetBefore={feetBefore}, feetAfter={feetAfter}");

            window.Close();
        });
    }

    [Fact]
    public void PlayJump_WithBubbleTimerExpiringMidAir_DefersHideUntilLandingAndPreservesFeet()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("poro");
            ConfigureIdleWindow(window, initialTop: 550, spriteW: 128, spriteH: 128);

            window.SetCustomText("跳躍氣泡對齊測試");
            window.ShowSpeechBubble();

            double feetBefore = window.Top + window.Height;
            Assert.Equal(Visibility.Visible, window.BubbleContainer.Visibility);

            var playJumpMethod = typeof(CharacterWindow).GetMethod("PlayJump", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(playJumpMethod);
            playJumpMethod.Invoke(window, new object?[] { null });

            var isJumpingField = typeof(CharacterWindow).GetField("_isJumping", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            Assert.True((bool)isJumpingField.GetValue(window)!);

            // Mid-air: bubble timer expires
            var onBubbleTimerTickMethod = typeof(CharacterWindow).GetMethod("OnBubbleTimerTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(onBubbleTimerTickMethod);
            onBubbleTimerTickMethod.Invoke(window, null);

            // Bubble must NOT close mid-air!
            Assert.Equal(Visibility.Visible, window.BubbleContainer.Visibility);
            var pendingHideField = typeof(CharacterWindow).GetField("_pendingHideBubbleAfterJump", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            Assert.True((bool)pendingHideField.GetValue(window)!);

            // Land: CompleteJumpLanding lands on the starting ground and consumes pending hide
            window.CompleteJumpLanding((int)window.Left);

            // Now bubble is hidden and feet are still anchored
            Assert.Equal(Visibility.Collapsed, window.BubbleContainer.Visibility);
            Assert.False((bool)pendingHideField.GetValue(window)!);
            double feetAfter = window.Top + window.Height;
            Assert.True(Math.Abs(feetAfter - feetBefore) < 1.0, $"Feet drifted after jump with bubble! feetBefore={feetBefore}, feetAfter={feetAfter}");

            window.Close();
        });
    }

    [Fact]
    public void PlayRandomAction_DoesNotInterruptMidAirJump()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("poro");
            ConfigureIdleWindow(window, initialTop: 550, spriteW: 128, spriteH: 128);

            var playJumpMethod = typeof(CharacterWindow).GetMethod("PlayJump", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(playJumpMethod);
            playJumpMethod.Invoke(window, new object?[] { null });

            var isJumpingField = typeof(CharacterWindow).GetField("_isJumping", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            Assert.True((bool)isJumpingField.GetValue(window)!);

            // Left clicking pet should NOT interrupt mid-air jump into walking in the sky
            window.PlayRandomAction();
            Assert.True((bool)isJumpingField.GetValue(window)!);

            window.Close();
        });
    }

    [Fact]
    public void PlayJump_WithBubbleOpenedMidAir_LandsFirmlyOnGround()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("poro");
            // Character starts idle on ground without bubble
            ConfigureIdleWindow(window, initialTop: 550, spriteW: 128, spriteH: 128);

            double feetBefore = window.Top + window.Height;
            Assert.Equal(Visibility.Collapsed, window.BubbleContainer.Visibility);

            var playJumpMethod = typeof(CharacterWindow).GetMethod("PlayJump", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(playJumpMethod);
            playJumpMethod.Invoke(window, new object?[] { null });

            var isJumpingField = typeof(CharacterWindow).GetField("_isJumping", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            Assert.True((bool)isJumpingField.GetValue(window)!);

            // Mid-air: user clicks or notification triggers speech bubble
            window.SetCustomText("空中突然冒出對話框");
            window.ShowSpeechBubble();
            Assert.Equal(Visibility.Visible, window.BubbleContainer.Visibility);

            // Land: CompleteJumpLanding must account for the new height and land feet firmly on ground
            window.CompleteJumpLanding((int)window.Left);

            double feetAfterLanding = window.Top + window.Height;
            Assert.True(Math.Abs(feetAfterLanding - feetBefore) < 1.0, $"Feet drifted after landing with mid-air bubble! feetBefore={feetBefore}, feetAfterLanding={feetAfterLanding}");

            // Later bubble closes: feet must still stay on ground
            window.HideSpeechBubble();
            double feetAfterHide = window.Top + window.Height;
            Assert.True(Math.Abs(feetAfterHide - feetBefore) < 1.0, $"Feet drifted after hiding bubble! feetBefore={feetBefore}, feetAfterHide={feetAfterHide}");

            window.Close();
        });
    }
}
