using System.Collections.Generic;
using YahaPet.Core;
using Xunit;

public class BehaviorPlannerJumpTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;
        public FixedRandomSource(params int[] values) => _values = new Queue<int>(values);
        public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
    }

    [Fact]
    public void PlanJump_MidScreen_GoingLeft_ComputesExpectedTargetsAndDuration()
    {
        // directionRoll=0 (left), offset roll=40, jumpHeight roll=100
        var random = new FixedRandomSource(0, 40, 100);
        var plan = BehaviorPlanner.PlanJump(
            currentPos: new PetPoint(500, 800),
            characterHeight: 100,
            minX: 0,
            maxX: 1920,
            landingY: 1000,
            random: random);

        Assert.Equal(BehaviorPlanner.JumpDirection.Left, plan.Direction);
        // endRangeX = 500 - 40 = 460; distanceX = 40; firstHalfX = |460 + 20| = 480
        Assert.Equal(480, plan.RiseTarget.X);
        Assert.Equal(800 - 100 - 100, plan.RiseTarget.Y); // currentY - jumpHeight - characterHeight
        Assert.Equal(1000 - 100, plan.LandTarget.Y);      // landingY - characterHeight
        Assert.Equal(460, plan.LandTarget.X);
        Assert.Equal(1000, plan.DurationMs);              // jumpHeight(100) * 10
    }

    [Fact]
    public void PlanJump_TooCloseToRightEdge_ForcesLeftInstead()
    {
        // directionRoll=1 (right), but position is within 100px of the right edge -> forced to 0 (left)
        var random = new FixedRandomSource(1, 10, 50);
        var plan = BehaviorPlanner.PlanJump(
            currentPos: new PetPoint(1850, 800),
            characterHeight: 100,
            minX: 0,
            maxX: 1920,
            landingY: 1000,
            random: random);

        Assert.Equal(BehaviorPlanner.JumpDirection.Left, plan.Direction);
        Assert.True(plan.LandTarget.X < 1850);
    }

    [Fact]
    public void PlanJump_TooCloseToLeftEdge_ReplicatesOriginalDirectionMismatchQuirk()
    {
        // directionRoll=0 (left) but currentPos.X <= 100 -> original falls into the "go right"
        // math branch for end_range_x while still labeling the animation "jumpleft". This is a
        // faithful port of that original quirk, not a new bug — see Task 3 notes.
        var random = new FixedRandomSource(0, 20, 60);
        var plan = BehaviorPlanner.PlanJump(
            currentPos: new PetPoint(50, 800),
            characterHeight: 100,
            minX: 0,
            maxX: 1920,
            landingY: 1000,
            random: random);

        Assert.Equal(BehaviorPlanner.JumpDirection.Left, plan.Direction); // animation label
        Assert.Equal(70, plan.LandTarget.X); // actual movement: 50 + 20 = 70 (rightward)
    }

    [Fact]
    public void PlanJump_OnSecondaryMonitorToTheLeft_DoesNotForceTowardPrimary()
    {
        // Before minX/maxX existed, "close to the left edge" was hardcoded as
        // currentPos.X <= 100, which described almost the ENTIRE span of a secondary
        // monitor positioned at negative X -- forcing every "jump left" roll there to
        // actually move right, back toward the primary monitor. With minX=-1475
        // reflecting that monitor's real left edge, a position deep into it (-1000)
        // should still be treated as "not near the edge" and jump further left.
        var random = new FixedRandomSource(0, 40, 100);
        var plan = BehaviorPlanner.PlanJump(
            currentPos: new PetPoint(-1000, 800),
            characterHeight: 100,
            minX: -1475,
            maxX: 1920,
            landingY: 1000,
            random: random);

        Assert.Equal(BehaviorPlanner.JumpDirection.Left, plan.Direction);
        Assert.True(plan.LandTarget.X < -1000); // actually moved further left/negative
        Assert.Equal(-1020, plan.RiseTarget.X); // must remain negative (-1020), not flip to positive (+1020)
        Assert.True(plan.RiseTarget.X >= -1475 && plan.RiseTarget.X <= 1920);
    }

    [Fact]
    public void PlanJump_RespectsBoundaryClamp_NeverExceedsMinXOrMaxX()
    {
        // currentPos is at maxX - 10, roll right (+50) -> must clamp to maxX
        var random = new FixedRandomSource(0, 50, 100);
        var plan = BehaviorPlanner.PlanJump(
            currentPos: new PetPoint(1910, 800),
            characterHeight: 100,
            minX: 0,
            maxX: 1920,
            landingY: 1000,
            random: random);

        Assert.True(plan.LandTarget.X <= 1920);
        Assert.True(plan.RiseTarget.X <= 1920);
        Assert.True(plan.LandTarget.X >= 0);
        Assert.True(plan.RiseTarget.X >= 0);
    }
}
