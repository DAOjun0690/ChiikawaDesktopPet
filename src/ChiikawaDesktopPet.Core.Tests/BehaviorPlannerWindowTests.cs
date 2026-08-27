using System.Collections.Generic;
using ChiikawaDesktopPet.Core;
using Xunit;

namespace ChiikawaDesktopPet.Core.Tests;

public sealed class BehaviorPlannerWindowTests
{
    [Theory]
    [InlineData(100, 100, 30, true)]
    [InlineData(120, 100, 30, true)]
    [InlineData(80, 100, 30, true)]
    [InlineData(130, 100, 30, true)]
    [InlineData(70, 100, 30, true)]
    [InlineData(131, 100, 30, false)]
    [InlineData(69, 100, 30, false)]
    [InlineData(500, 100, 30, false)]
    public void ShouldSnapToWindow_ChecksWithinTolerance(int petBottomY, int windowTopY, int tolerance, bool expected)
    {
        bool actual = BehaviorPlanner.ShouldSnapToWindow(petBottomY, windowTopY, tolerance);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(200, 100, 100, 500, false)] // center = 250 (inside [100, 500])
    [InlineData(40, 100, 100, 500, true)]   // center = 90 (left of 100)
    [InlineData(460, 100, 100, 500, true)]  // center = 510 (right of 500)
    [InlineData(100, 100, 100, 500, false)] // center = 150 (inside)
    public void IsSteppedOffWindow_DetectsWhenCenterCrossesBoundary(int petLeft, int petWidth, int windowLeft, int windowRight, bool expected)
    {
        bool actual = BehaviorPlanner.IsSteppedOffWindow(petLeft, petWidth, windowLeft, windowRight);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(200, 0, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, 0, true)]
    [InlineData(-10, 0, true)]
    public void IsWindowSqueezed_DetectsTopBoundary(int windowTopY, int minSafeTop, bool expected)
    {
        bool actual = BehaviorPlanner.IsWindowSqueezed(windowTopY, minSafeTop);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CalculateAttachedPosition_ComputesCorrectWorldPoint()
    {
        var point = BehaviorPlanner.CalculateAttachedPosition(windowLeft: 200, windowTop: 150, relativeX: 50, characterHeight: 80);
        Assert.Equal(250, point.X);
        Assert.Equal(70, point.Y);
    }
}
