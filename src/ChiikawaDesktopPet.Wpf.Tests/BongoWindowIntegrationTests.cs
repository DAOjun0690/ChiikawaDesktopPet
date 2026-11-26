// src/ChiikawaDesktopPet.Wpf.Tests/BongoWindowIntegrationTests.cs
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ChiikawaDesktopPet.Core;
using Xunit;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class BongoWindowIntegrationTests
{
    [Fact]
    public void BongoWindow_Title_IsBongoTypingPet()
    {
        var thread = new Thread(() =>
        {
            var config = new BongoConfig();
            var skinManager = new BongoSkinManager();
            var keyboardHook = new GlobalKeyboardHook();
            var window = new BongoWindow(config, skinManager, keyboardHook);

            Assert.Equal("Bongo 打字pet", window.Title);
            window.Close();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void BongoWindow_ContextMenu_HasHideAllAtTop()
    {
        var thread = new Thread(() =>
        {
            var config = new BongoConfig();
            var skinManager = new BongoSkinManager();
            var keyboardHook = new GlobalKeyboardHook();
            var window = new BongoWindow(config, skinManager, keyboardHook);

            // Trigger RebuildContextMenu
            var rebuildMethod = typeof(BongoWindow).GetMethod("RebuildContextMenu",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(rebuildMethod);
            rebuildMethod.Invoke(window, null);

            var menu = window.ContextMenu;
            Assert.NotNull(menu);
            Assert.True(menu.Items.Count >= 3);

            // Item 0: 一鍵隱藏
            var firstItem = menu.Items[0] as MenuItem;
            Assert.NotNull(firstItem);
            Assert.Equal("一鍵隱藏", firstItem.Header);

            // Item 1: Separator
            Assert.IsType<Separator>(menu.Items[1]);

            // Item 2: Title Header containing "打字pet"
            var titleItem = menu.Items[2] as MenuItem;
            Assert.NotNull(titleItem);
            string titleHeader = titleItem.Header?.ToString() ?? string.Empty;
            Assert.Contains("打字pet", titleHeader);
            Assert.DoesNotContain("打字伴侶", titleHeader);

            // Check that nowhere in the menu does "伴侶" appear
            foreach (var item in menu.Items)
            {
                if (item is MenuItem mi && mi.Header is string h)
                {
                    Assert.DoesNotContain("伴侶", h);
                }
            }

            window.Close();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void BongoWindow_PawTransforms_InitializedAtZero()
    {
        var thread = new Thread(() =>
        {
            var config = new BongoConfig();
            var skinManager = new BongoSkinManager();
            var keyboardHook = new GlobalKeyboardHook();
            var window = new BongoWindow(config, skinManager, keyboardHook);

            Assert.NotNull(window.LeftPawTransform);
            Assert.NotNull(window.RightPawTransform);
            Assert.Equal(0.0, window.LeftPawTransform.X);
            Assert.Equal(0.0, window.LeftPawTransform.Y);
            Assert.Equal(0.0, window.RightPawTransform.X);
            Assert.Equal(0.0, window.RightPawTransform.Y);

            window.Close();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void BongoWindow_MouseMotion_MirrorsXWhenEnabled()
    {
        var thread = new Thread(() =>
        {
            var config = new BongoConfig { TrackMouseMotion = true, MirrorMouseMotion = true };
            var skinManager = new BongoSkinManager();
            var keyboardHook = new GlobalKeyboardHook();
            var window = new BongoWindow(config, skinManager, keyboardHook);
            window.Show();

            // Access private OnGlobalMouseMove via reflection
            var moveMethod = typeof(BongoWindow).GetMethod("OnGlobalMouseMove",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(moveMethod);

            // Move mouse to the RIGHT edge of the virtual screen
            // With MirrorMouseMotion = true, _targetMouseX should be NEGATIVE (moving paw to the left)
            int rightX = (int)(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth);
            moveMethod.Invoke(window, new object[] { rightX, 540 });

            var targetXField = typeof(BongoWindow).GetField("_targetMouseX",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(targetXField);

            double targetX = (double)targetXField.GetValue(window)!;
            Assert.True(targetX < 0, $"Expected negative targetMouseX when moving right in mirror mode, got {targetX}");

            // Move mouse to the LEFT edge of the virtual screen
            // With MirrorMouseMotion = true, _targetMouseX should be POSITIVE (moving paw to the right)
            int leftX = (int)SystemParameters.VirtualScreenLeft;
            moveMethod.Invoke(window, new object[] { leftX, 540 });
            targetX = (double)targetXField.GetValue(window)!;
            Assert.True(targetX > 0, $"Expected positive targetMouseX when moving left in mirror mode, got {targetX}");

            window.Close();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void BongoWindow_ImplementsIClickThroughWindow_AndHandlesClickThrough()
    {
        var thread = new Thread(() =>
        {
            var config = new BongoConfig();
            var skinManager = new BongoSkinManager();
            var keyboardHook = new GlobalKeyboardHook();
            var window = new BongoWindow(config, skinManager, keyboardHook);

            Assert.True(window is IClickThroughWindow);
            var ctWindow = (IClickThroughWindow)window;
            Assert.False(ctWindow.HasOpenContextMenu);
            Assert.False(window.IsPointInsideContextMenu(new NativeMethods.POINT { X = 0, Y = 0 }));

            bool eventFired = false;
            window.ClickThroughChanged += (val) => eventFired = val;
            window.SetClickThrough(true);

            Assert.True(window.Config.ClickThrough);
            Assert.True(eventFired);

            window.SetClickThrough(false);
            Assert.False(window.Config.ClickThrough);

            window.Close();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void BongoWindow_MultiMonitor_SupportsVirtualScreenBounds()
    {
        var thread = new Thread(() =>
        {
            var config = new BongoConfig();
            var skinManager = new BongoSkinManager();
            var keyboardHook = new GlobalKeyboardHook();
            var window = new BongoWindow(config, skinManager, keyboardHook);

            // Test clamping to virtual screen bounds
            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;

            window.Left = vLeft;
            window.Top = vTop;

            var clampMethod = typeof(BongoWindow).GetMethod("ClampToScreen",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(clampMethod);
            clampMethod.Invoke(window, null);

            Assert.True(window.Left >= vLeft, $"Expected Left >= VirtualScreenLeft ({vLeft}), got {window.Left}");
            Assert.True(window.Top >= vTop, $"Expected Top >= VirtualScreenTop ({vTop}), got {window.Top}");

            window.Close();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void BongoWindow_UnshownNaNCoordinates_SerializeSafelyAndRecoverOnClamp()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var config = new BongoConfig { IsEnabled = true, CurrentSkinKey = "chesthair_monkey" };
                var skinManager = new BongoSkinManager();
                using var keyboardHook = new GlobalKeyboardHook();
                using var mouseHook = new GlobalMouseHook();
                var window = new BongoWindow(config, skinManager, keyboardHook, mouseHook);

                // Before Show(), WPF Window.Left/Top default to double.NaN
                Assert.True(double.IsFinite(window.SafeLeft));
                Assert.True(double.IsFinite(window.SafeTop));

                var profile = new PetProfile
                {
                    Version = 1,
                    BongoState = new BongoProfileState
                    {
                        IsEnabled = true,
                        SkinKey = "chesthair_monkey",
                        PositionX = window.SafeLeft,
                        PositionY = window.SafeTop,
                        Scale = config.Scale
                    }
                };
                string json = ProfileManager.Serialize(profile);
                Assert.Contains("chesthair_monkey", json);

                // RefreshVisualSurface(clamp: true) must recover NaN to finite in-screen coordinates
                window.RefreshVisualSurface(clamp: true);
                Assert.True(double.IsFinite(window.Left));
                Assert.True(double.IsFinite(window.Top));

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void BongoWindow_BossKeyHideAndUnhideAfterMonitorDisconnect_ClampsMonkeyBackOnScreenAndTogglesHooks()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var config = new BongoConfig { IsEnabled = true, CurrentSkinKey = "chesthair_monkey" };
                var skinManager = new BongoSkinManager();
                using var keyboardHook = new GlobalKeyboardHook();
                using var mouseHook = new GlobalMouseHook();
                var window = new BongoWindow(config, skinManager, keyboardHook, mouseHook);

                window.ShowBongo();
                Assert.Equal(Visibility.Visible, window.Visibility);
                Assert.True(keyboardHook.IsActive);
                Assert.True(mouseHook.IsActive);

                // Simulate Boss Key hide while on a disconnected external monitor
                window.PauseForBossKey();
                Assert.Equal(Visibility.Collapsed, window.Visibility);
                Assert.False(keyboardHook.IsActive);
                Assert.False(mouseHook.IsActive);

                // Simulate coordinates left on a vanished external monitor
                window.Left = 50000;
                window.Top = 50000;

                // Resume from Boss Key -> must clamp back into current VirtualScreen and restore hooks
                window.ResumeFromBossKey();
                Assert.Equal(Visibility.Visible, window.Visibility);
                Assert.True(keyboardHook.IsActive);
                Assert.True(mouseHook.IsActive);

                double vLeft = SystemParameters.VirtualScreenLeft;
                double vTop = SystemParameters.VirtualScreenTop;
                double vRight = vLeft + SystemParameters.VirtualScreenWidth;
                double vBottom = vTop + SystemParameters.VirtualScreenHeight;
                Assert.InRange(window.Left, vLeft, vRight);
                Assert.InRange(window.Top, vTop, vBottom);

                // HideBongo must also stop hooks
                window.HideBongo();
                Assert.False(keyboardHook.IsActive);
                Assert.False(mouseHook.IsActive);

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void BongoWindow_SystemSuspendAndResume_StopsAndRestoresHooks()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var config = new BongoConfig { IsEnabled = true, CurrentSkinKey = "chesthair_monkey" };
                var skinManager = new BongoSkinManager();
                using var keyboardHook = new GlobalKeyboardHook();
                using var mouseHook = new GlobalMouseHook();
                var window = new BongoWindow(config, skinManager, keyboardHook, mouseHook);

                window.ShowBongo();
                Assert.True(keyboardHook.IsActive);
                Assert.True(mouseHook.IsActive);

                window.PauseForSystemSuspend();
                Assert.True(window.IsSystemSuspended);
                Assert.False(keyboardHook.IsActive);
                Assert.False(mouseHook.IsActive);

                window.ResumeFromSystemSuspend();
                Assert.False(window.IsSystemSuspended);
                Assert.Equal(Visibility.Visible, window.Visibility);
                Assert.True(keyboardHook.IsActive);
                Assert.True(mouseHook.IsActive);

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void BongoWindow_BaseVisualScale_100PercentIsHalfVisualSize_AndBubbleIsNotScaled()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var config = new BongoConfig { Scale = 1.0 };
                var skinManager = new BongoSkinManager();
                using var keyboardHook = new GlobalKeyboardHook();
                using var mouseHook = new GlobalMouseHook();
                var window = new BongoWindow(config, skinManager, keyboardHook, mouseHook);

                Assert.Equal(0.5, BongoWindow.BaseVisualScale, 3);
                Assert.Equal(0.5, window.WindowScaleTransform.ScaleX, 3);
                Assert.Equal(0.5, window.WindowScaleTransform.ScaleY, 3);

                window.ApplyScale(1.0);
                Assert.Equal(1.0, config.Scale, 3);
                Assert.Equal(0.5, window.WindowScaleTransform.ScaleX, 3);
                Assert.Equal(0.5, window.WindowScaleTransform.ScaleY, 3);

                window.ApplyScale(2.0);
                Assert.Equal(2.0, config.Scale, 3);
                Assert.Equal(1.0, window.WindowScaleTransform.ScaleX, 3);
                Assert.Equal(1.0, window.WindowScaleTransform.ScaleY, 3);

                // ScaleTransform must be on PetVisualContainer, not on RootContainer (so bubble is unscaled)
                Assert.Same(window.WindowScaleTransform, window.PetVisualContainer.LayoutTransform);
                Assert.NotSame(window.WindowScaleTransform, window.RootContainer.LayoutTransform);

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void BongoWindow_ContextMenu_ContainsDialogueBoardItems()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var config = new BongoConfig { AlwaysShowBubble = true };
                var skinManager = new BongoSkinManager();
                using var keyboardHook = new GlobalKeyboardHook();
                using var mouseHook = new GlobalMouseHook();
                var window = new BongoWindow(config, skinManager, keyboardHook, mouseHook);

                var rebuildMethod = typeof(BongoWindow).GetMethod("RebuildContextMenu",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(rebuildMethod);
                rebuildMethod.Invoke(window, null);

                MenuItem? alwaysShowItem = null;
                MenuItem? setQuoteItem = null;
                foreach (var item in window.ContextMenu!.Items)
                {
                    if (item is MenuItem mi && mi.Header is string h)
                    {
                        if (h == "永久顯示對話框") alwaysShowItem = mi;
                        if (h == "設定對話文字...") setQuoteItem = mi;
                    }
                }

                Assert.NotNull(alwaysShowItem);
                Assert.True(alwaysShowItem!.IsCheckable);
                Assert.True(alwaysShowItem.IsChecked);
                Assert.NotNull(setQuoteItem);

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void BongoWindow_DialogueBoard_SupportsMarkdownCheckboxSharedAcrossSkinsAndProfileRoundTrip()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var config = new BongoConfig { IsEnabled = true, CurrentSkinKey = "chiikawa" };
                var skinManager = new BongoSkinManager();
                using var keyboardHook = new GlobalKeyboardHook();
                using var mouseHook = new GlobalMouseHook();
                var window = new BongoWindow(config, skinManager, keyboardHook, mouseHook);

                // Without custom text, CurrentDialogueText falls back to skin default quote
                Assert.False(window.HasCustomDialogueText);
                Assert.Equal(CharacterQuotes.GetDefaultQuote("chiikawa"), window.CurrentDialogueText);

                window.ApplySkin("chesthair_monkey");
                Assert.Equal(CharacterQuotes.GetDefaultQuote("chesthair_monkey"), window.CurrentDialogueText);

                // Set custom markdown todo list and enable AlwaysShowBubble
                int profileChangedCount = 0;
                window.ProfileChanged += () => profileChangedCount++;

                window.SetCustomText("- [ ] 寫測試\n- [ ] 提交程式碼", TextAlignment.Left, 15.0, 280.0, 210.0);
                window.SetAlwaysShowBubble(true);

                Assert.True(window.HasCustomDialogueText);
                Assert.True(window.AlwaysShowBubble);
                Assert.True(window.IsBubbleVisible);
                Assert.Equal(TextAlignment.Left, window.DialogueAlignment);
                Assert.Equal(15.0, window.DialogueFontSize, 3);
                Assert.True(profileChangedCount >= 2);

                // Switching skins keeps the shared custom message board
                window.ApplySkin("chiikawa");
                Assert.Equal("- [ ] 寫測試\n- [ ] 提交程式碼", window.CurrentDialogueText);

                // Toggling checkbox updates stored markdown text
                var toggleMethod = typeof(BongoWindow).GetMethod("OnBubbleCheckboxToggled",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(toggleMethod);
                toggleMethod.Invoke(window, new object[] { 0 });
                Assert.StartsWith("- [x] 寫測試", window.CustomDialogueText);

                // Profile round-trip serialization + ApplyDialogueProfile
                var profile = new PetProfile
                {
                    Version = 1,
                    BongoState = new BongoProfileState
                    {
                        IsEnabled = true,
                        SkinKey = "chesthair_monkey",
                        Scale = 1.0,
                        DialogueText = config.DialogueText,
                        DialogueAlignment = config.DialogueAlignment,
                        DialogueFontSize = config.DialogueFontSize,
                        AlwaysShowBubble = config.AlwaysShowBubble,
                        DialogueImageMaxWidth = config.DialogueImageMaxWidth,
                        DialogueImageMaxHeight = config.DialogueImageMaxHeight
                    }
                };
                string json = ProfileManager.Serialize(profile);
                var restored = ProfileManager.Deserialize(json);
                Assert.NotNull(restored?.BongoState);
                Assert.Equal(config.DialogueText, restored!.BongoState!.DialogueText);
                Assert.Equal("Left", restored.BongoState.DialogueAlignment);
                Assert.Equal(15.0, restored.BongoState.DialogueFontSize, 3);
                Assert.True(restored.BongoState.AlwaysShowBubble);

                // Backward-compatibility: legacy JSON without dialogue fields deserializes with defaults
                string legacyJson = """{"Version":1,"Characters":[],"BongoState":{"IsEnabled":true,"SkinKey":"chiikawa","PositionX":100,"PositionY":200,"Scale":1.0}}""";
                var legacy = ProfileManager.Deserialize(legacyJson);
                Assert.NotNull(legacy?.BongoState);
                Assert.Null(legacy!.BongoState!.DialogueText);
                Assert.Equal("Center", legacy.BongoState.DialogueAlignment);
                Assert.Equal(13.0, legacy.BongoState.DialogueFontSize, 3);
                Assert.False(legacy.BongoState.AlwaysShowBubble);

                window.ApplyDialogueProfile(legacy.BongoState);
                Assert.False(window.HasCustomDialogueText);
                Assert.False(window.AlwaysShowBubble);
                Assert.False(window.IsBubbleVisible);

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void BongoWindow_BubblePlacement_FlipsToBottomNearTopEdge_AndStaysTopWithSpace()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var config = new BongoConfig { IsEnabled = true };
                var skinManager = new BongoSkinManager();
                using var keyboardHook = new GlobalKeyboardHook();
                using var mouseHook = new GlobalMouseHook();
                var window = new BongoWindow(config, skinManager, keyboardHook, mouseHook);

                window.SetCustomText("測試自動翻轉氣泡");
                window.SetAlwaysShowBubble(true);

                var workArea = SystemParameters.WorkArea;
                // Near top edge: insufficient space above -> flip to Bottom
                var nearTop = window.UpdateBubblePlacement(bubbleH: 120, petTopDip: workArea.Top + 10, petHeightDip: 120);
                Assert.Equal(CharacterWindow.SpeechBubblePlacement.Bottom, nearTop);
                Assert.Equal(Visibility.Visible, window.BubblePointerUp.Visibility);
                Assert.Equal(Visibility.Collapsed, window.BubblePointerDown.Visibility);

                // Near bottom edge: plenty of space above -> Top
                var nearBottom = window.UpdateBubblePlacement(bubbleH: 120, petTopDip: workArea.Bottom - 160, petHeightDip: 120);
                Assert.Equal(CharacterWindow.SpeechBubblePlacement.Top, nearBottom);
                Assert.Equal(Visibility.Collapsed, window.BubblePointerUp.Visibility);
                Assert.Equal(Visibility.Visible, window.BubblePointerDown.Visibility);

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
