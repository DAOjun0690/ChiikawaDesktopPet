// src/ChiikawaDesktopPet.Core.Tests/InteractionPlannerTests.cs
using System;
using Xunit;

namespace ChiikawaDesktopPet.Core.Tests;

public class InteractionPlannerTests
{
    private sealed class FixedRandom(int nextResult) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => nextResult;
    }

    [Fact]
    public void IsInRange_WithinThreshold_ReturnsTrue()
    {
        var posA = new PetPoint(100, 100);
        var posB = new PetPoint(150, 100); // dist = 50 <= 150
        Assert.True(InteractionPlanner.IsInRange(posA, posB, threshold: 150));
    }

    [Fact]
    public void IsInRange_BeyondThreshold_ReturnsFalse()
    {
        var posA = new PetPoint(100, 100);
        var posB = new PetPoint(300, 100); // dist = 200 > 150
        Assert.False(InteractionPlanner.IsInRange(posA, posB, threshold: 150));
    }

    [Fact]
    public void IsOverlapping_WhenIntersecting_ReturnsTrue()
    {
        var posA = new PetPoint(100, 100);
        var posB = new PetPoint(150, 150);
        Assert.True(InteractionPlanner.IsOverlapping(posA, 100, 100, posB, 100, 100));
    }

    [Fact]
    public void IsOverlapping_WhenSeparate_ReturnsFalse()
    {
        var posA = new PetPoint(100, 100);
        var posB = new PetPoint(300, 300);
        Assert.False(InteractionPlanner.IsOverlapping(posA, 100, 100, posB, 100, 100));
    }

    [Theory]
    [InlineData(0, 40, true)]
    [InlineData(39, 40, true)]
    [InlineData(40, 40, false)]
    [InlineData(99, 40, false)]
    public void ShouldTrigger_RespectsProbabilityRoll(int roll, int probability, bool expected)
    {
        var random = new FixedRandom(roll);
        Assert.Equal(expected, InteractionPlanner.ShouldTrigger(random, probability));
    }

    [Fact]
    public void CalculateInteractionWindowPosition_CentersAndClampsToScreen()
    {
        var screenBounds = new PetBounds(0, 0, 1920, 1080);
        var posA = new PetPoint(400, 500);
        var posB = new PetPoint(600, 500);

        var windowPos = InteractionPlanner.CalculateInteractionWindowPosition(
            posA, widthA: 100, heightA: 100,
            posB, widthB: 100, heightB: 100,
            windowWidth: 400, windowHeight: 300,
            screenBounds);

        // Center A = (450, 550), Center B = (650, 550) -> MidCenter = (550, 550)
        // Window TopLeft = (550 - 200, 550 - 150) = (350, 400)
        Assert.Equal(350, windowPos.X);
        Assert.Equal(400, windowPos.Y);
    }

    [Fact]
    public void CalculateReappearPositions_SpacesAndPlacesOnScreenBottom()
    {
        var screenBounds = new PetBounds(0, 0, 1920, 1040); // 1040 bottom (e.g. 40px taskbar)
        var midPoint = new PetPoint(500, 500);

        var (posA, posB) = InteractionPlanner.CalculateReappearPositions(
            midPoint, spacing: 200, screenBounds, petWidthA: 80, petHeightA: 80, petWidthB: 90, petHeightB: 90);

        // A is left of center, B is right of center
        Assert.True(posA.X < posB.X);
        // Both Y coordinates rest precisely at screenBounds.Bottom - petHeight
        Assert.Equal(960, posA.Y); // 1040 - 80
        Assert.Equal(950, posB.Y); // 1040 - 90
    }

    [Fact]
    public void IsCooldownActive_CorrectlyTracksElapsedDuration()
    {
        var now = new DateTime(2026, 8, 25, 12, 0, 0);
        var triggerRecent = now.AddSeconds(-20);
        var triggerOld = now.AddSeconds(-60);

        Assert.True(InteractionPlanner.IsCooldownActive(triggerRecent, now, TimeSpan.FromSeconds(45)));
        Assert.False(InteractionPlanner.IsCooldownActive(triggerOld, now, TimeSpan.FromSeconds(45)));
    }
}
