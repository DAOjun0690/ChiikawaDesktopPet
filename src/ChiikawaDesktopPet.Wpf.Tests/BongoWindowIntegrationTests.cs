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
}
