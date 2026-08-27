using System.Collections.Generic;
using ChiikawaDesktopPet.Core;
using Xunit;

public class BehaviorPlannerClampAndFallTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;
        public FixedRandomSource(params int[] values) => _values = new Queue<int>(values);
        public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
    }

    [Fact]
    public void ClampToBounds_PointInsideBounds_IsUnchanged()
    {
        var bounds = new PetBounds(Left: 0, Top: 0, Right: 1920, Bottom: 1040);
        var result = BehaviorPlanner.ClampToBounds(new PetPoint(500, 500), bounds, width: 100, height: 100);
        Assert.Equal(new PetPoint(500, 500), result);
    }

    [Fact]
    public void ClampToBounds_PointBeyondRightEdge_IsClampedWithFivePixelMargin()
    {
        var bounds = new PetBounds(Left: 0, Top: 0, Right: 1920, Bottom: 1040);
        var result = BehaviorPlanner.ClampToBounds(new PetPoint(2000, 500), bounds, width: 100, height: 100);
        Assert.Equal(1920 - 100 + 5, result.X);
    }

    [Fact]
    public void ClampToBounds_PointBeyondBottomEdge_IsClampedWithOnePixelMargin()
    {
        var bounds = new PetBounds(Left: 0, Top: 0, Right: 1920, Bottom: 1040);
        var result = BehaviorPlanner.ClampToBounds(new PetPoint(500, 2000), bounds, width: 100, height: 100);
        Assert.Equal(1040 - 100 + 1, result.Y);
    }

    [Fact]
    public void PlanFall_LowRoll_IsCrash()
    {
        var random = new FixedRandomSource(30);
        var outcome = BehaviorPlanner.PlanFall(new PetPoint(500, 200), screenHeight: 1080, landingY: 1040, characterHeight: 100, random);
        Assert.True(outcome.Crashed);
        Assert.Equal(new PetPoint(500, 940), outcome.LandingPoint);
        Assert.Equal((int)(1.5 * (1080 - 200)), outcome.DurationMs);
    }

    [Fact]
    public void PlanFall_HighRoll_IsNormalLanding()
    {
        var random = new FixedRandomSource(31);
        var outcome = BehaviorPlanner.PlanFall(new PetPoint(500, 200), screenHeight: 1080, landingY: 1040, characterHeight: 100, random);
        Assert.False(outcome.Crashed);
    }
}
