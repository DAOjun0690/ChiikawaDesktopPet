// src/ChiikawaDesktopPet.Core.Tests/BongoKeyboardLayoutTests.cs
using System;
using Xunit;
using ChiikawaDesktopPet.Core;

namespace ChiikawaDesktopPet.Core.Tests;

public class BongoKeyboardLayoutTests
{
    [Fact]
    public void GetTargetOffset_Spacebar_PointsToTopCenter()
    {
        var (dx, dy) = BongoKeyboardLayout.GetTargetOffset(0x20); // Space
        Assert.Equal(0.0, dx);
        Assert.True(dy < 0, "In 180° layout, spacebar should be on the top row (negative Y).");
    }

    [Fact]
    public void GetTargetOffset_UnknownKey_DefaultsToCenter()
    {
        var (dx, dy) = BongoKeyboardLayout.GetTargetOffset(0xFF); // Unmapped key
        Assert.Equal(0.0, dx);
        Assert.Equal(0.0, dy);
    }

    [Fact]
    public void GetTargetOffset_EnterAndEsc_OppositeSidesIn180Layout()
    {
        var (enterDx, enterDy) = BongoKeyboardLayout.GetTargetOffset(0x0D); // Enter
        var (escDx, escDy) = BongoKeyboardLayout.GetTargetOffset(0x1B);     // Esc

        // In 180° layout, Enter is on the left side (dx < 0), Esc is on the right side (dx > 0)
        Assert.True(enterDx < 0, "Enter should be on the left side in 180° layout.");
        Assert.True(escDx > 0, "Esc should be on the right side in 180° layout.");
        Assert.True(escDy > 0, "Esc should be on the bottom row in 180° layout.");
    }

    [Fact]
    public void GetTargetOffset_LeftCtrl_PointsToTopRight()
    {
        var (dx, dy) = BongoKeyboardLayout.GetTargetOffset(0xA2); // VK_LCONTROL
        Assert.True(dx > 0, "Left Ctrl should be on the right side of the 180° keyboard (dx > 0).");
        Assert.True(dy < 0, "Left Ctrl should be on the top row of the 180° keyboard (dy < 0).");

        var (genericDx, genericDy) = BongoKeyboardLayout.GetTargetOffset(0x11); // VK_CONTROL
        Assert.Equal(dx, genericDx);
        Assert.Equal(dy, genericDy);
    }

    [Theory]
    [InlineData(0x51, true)]  // Q -> Left
    [InlineData(0x57, true)]  // W -> Left
    [InlineData(0x41, true)]  // A -> Left
    [InlineData(0x50, false)] // P -> Right
    [InlineData(0x4C, false)] // L -> Right
    [InlineData(0x0D, false)] // Enter -> Right
    [InlineData(0xA2, true)]  // VK_LCONTROL -> Left
    [InlineData(0x11, true)]  // VK_CONTROL -> Left
    [InlineData(0xA0, true)]  // VK_LSHIFT -> Left
    [InlineData(0x10, true)]  // VK_SHIFT -> Left
    [InlineData(0x5B, true)]  // VK_LWIN -> Left
    public void IsLeftHandKey_IdentifiesHandCorrectly(int vkCode, bool expectedLeft)
    {
        Assert.Equal(expectedLeft, BongoKeyboardLayout.IsLeftHandKey(vkCode));
    }
}
