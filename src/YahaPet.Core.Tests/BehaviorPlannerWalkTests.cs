using System.Collections.Generic;
using YahaPet.Core;
using Xunit;

public class BehaviorPlannerWalkTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;
        public FixedRandomSource(params int[] values) => _values = new Queue<int>(values);
        public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
    }

    [Fact]
    public void PlanWalk_RollLeft_TargetWithinZeroToCurrentMinusMargin()
    {
        // rollDirection=0 (left); target-x roll returns 200 (must be in [0, currentX-100)=[0,400))
        var random = new FixedRandomSource(0, 200);
        var plan = BehaviorPlanner.PlanWalk(new PetPoint(500, 800), minX: 0, maxX: 1920, characterWidth: 100, random);

        Assert.NotNull(plan);
        Assert.Equal(BehaviorPlanner.WalkDirection.Left, plan!.Direction);
        Assert.Equal(200, plan.TargetX);
        Assert.Equal(5 * 300, plan.DurationMs); // 5 * |200 - 500|
    }

    [Fact]
    public void PlanWalk_RollRight_TargetWithinCurrentPlusMarginToScreenEdge()
    {
        var random = new FixedRandomSource(1, 700);
        var plan = BehaviorPlanner.PlanWalk(new PetPoint(500, 800), minX: 0, maxX: 1920, characterWidth: 100, random);

        Assert.NotNull(plan);
        Assert.Equal(BehaviorPlanner.WalkDirection.Right, plan!.Direction);
        Assert.Equal(700, plan.TargetX);
    }

    [Fact]
    public void PlanWalk_TooCloseToLeftEdge_ReturnsNull()
    {
        // rollDirection=0 (left); startRange=0, endRange = currentX(50) - 100 = -50 -> infeasible
        var random = new FixedRandomSource(0);
        var plan = BehaviorPlanner.PlanWalk(new PetPoint(50, 800), minX: 0, maxX: 1920, characterWidth: 100, random);

        Assert.Null(plan);
    }

    [Fact]
    public void PlanWalk_OnSecondaryMonitorToTheLeft_CanWalkFurtherLeftPastZero()
    {
        // A secondary monitor positioned to the left of the primary spans negative X
        // (e.g. -1475..0). minX must reflect that combined virtual-desktop range, not
        // an assumed 0, or the character could never walk deeper into that monitor.
        var random = new FixedRandomSource(0, -1000);
        var plan = BehaviorPlanner.PlanWalk(new PetPoint(-500, 800), minX: -1475, maxX: 1920, characterWidth: 100, random);

        Assert.NotNull(plan);
        Assert.Equal(BehaviorPlanner.WalkDirection.Left, plan!.Direction);
        Assert.Equal(-1000, plan.TargetX);
    }

    [Fact]
    public void PlanWalk_OnSecondaryMonitorToTheLeft_CanWalkRightBackAcrossToPrimary()
    {
        // Walking right from the secondary monitor must be able to target anywhere up
        // to the far side of the primary monitor (maxX), not just the secondary's own
        // right edge (0).
        var random = new FixedRandomSource(1, 1500);
        var plan = BehaviorPlanner.PlanWalk(new PetPoint(-500, 800), minX: -1475, maxX: 1920, characterWidth: 100, random);

        Assert.NotNull(plan);
        Assert.Equal(BehaviorPlanner.WalkDirection.Right, plan!.Direction);
        Assert.Equal(1500, plan.TargetX);
    }
}
