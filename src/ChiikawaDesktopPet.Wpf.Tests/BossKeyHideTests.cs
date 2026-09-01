using System;
using System.Threading;
using System.Windows.Controls;
using ChiikawaDesktopPet.Wpf;
using Xunit;
using MenuItem = System.Windows.Controls.MenuItem;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class BossKeyHideTests
{
    [Fact]
    public void NativeMethods_HotkeyConstants_AreCorrect()
    {
        Assert.Equal((uint)0x0001, NativeMethods.MOD_ALT);
        Assert.Equal((uint)0x0002, NativeMethods.MOD_CONTROL);
        Assert.Equal((uint)0x0004, NativeMethods.MOD_SHIFT);
        Assert.Equal((uint)0x0008, NativeMethods.MOD_WIN);
        Assert.Equal((uint)0x4000, NativeMethods.MOD_NOREPEAT);
        Assert.Equal(0x0312, NativeMethods.WM_HOTKEY);
    }

    [Fact]
    public void CharacterWindow_HidePetAndShowPet_UpdatesStateCorrectly()
    {
        var thread = new Thread(() =>
        {
            var window = new CharacterWindow("chiikawa", 1);
            Assert.False(window.IsPetHidden);

            window.HidePet();
            Assert.True(window.IsPetHidden);

            window.ShowPet();
            Assert.False(window.IsPetHidden);

            window.Shutdown();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void CharacterWindow_ContextMenu_HasHideAllAtTop()
    {
        var thread = new Thread(() =>
        {
            var window = new CharacterWindow("chiikawa", 1);
            
            var method = typeof(CharacterWindow).GetMethod("ShowContextMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(window, null);

            Assert.NotNull(window.ContextMenu);
            Assert.True(window.ContextMenu.Items.Count >= 2);

            var firstItem = window.ContextMenu.Items[0] as MenuItem;
            Assert.NotNull(firstItem);
            Assert.Equal("一鍵隱藏", firstItem.Header);

            var secondItem = window.ContextMenu.Items[1] as Separator;
            Assert.NotNull(secondItem);

            window.Shutdown();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
