using System.Collections.Generic;
using YahaPet.Core;
using Xunit;

public class BehaviorPlannerActionTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;
        public FixedRandomSource(params int[] values) => _values = new Queue<int>(values);
        public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
    }

    [Theory]
    [InlineData(0, AutonomousActionKind.Jump)]
    [InlineData(9, AutonomousActionKind.Jump)]
    [InlineData(10, AutonomousActionKind.Walk)]
    [InlineData(49, AutonomousActionKind.Walk)]
    [InlineData(95, AutonomousActionKind.NoOp)]
    [InlineData(99, AutonomousActionKind.NoOp)]
    public void ChooseAutonomousAction_UsesLayeredRollBoundaries(int roll, AutonomousActionKind expectedKind)
    {
        var random = new FixedRandomSource(roll);
        var result = BehaviorPlanner.ChooseAutonomousAction(new List<string> { "dance" }, random);
        Assert.Equal(expectedKind, result.Kind);
    }

    [Fact]
    public void ChooseAutonomousAction_MidRangeRoll_PicksNamedAnimation()
    {
        // roll=50 selects the "other animation" branch, then a second roll picks index 1 ("tapdance").
        var random = new FixedRandomSource(50, 1);
        var result = BehaviorPlanner.ChooseAutonomousAction(new List<string> { "dance", "tapdance" }, random);
        Assert.Equal(AutonomousActionKind.PlayAnimation, result.Kind);
        Assert.Equal("tapdance", result.AnimationName);
    }

    [Fact]
    public void ChooseAutonomousAction_MidRangeRoll_EmptyAnimationList_IsNoOp()
    {
        // Matches Hachiware's real asset set: no named animations besides walkleft/jump.
        var random = new FixedRandomSource(60);
        var result = BehaviorPlanner.ChooseAutonomousAction(new List<string>(), random);
        Assert.Equal(AutonomousActionKind.NoOp, result.Kind);
    }

    [Fact]
    public void NextIdleIntervalMs_ReturnsValueFromRandomSource()
    {
        var random = new FixedRandomSource(4321);
        Assert.Equal(4321, BehaviorPlanner.NextIdleIntervalMs(random));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void ChooseAutonomousAction_WhenJumpDisabled_ReturnsNoOpForJumpRolls(int roll)
    {
        var random = new FixedRandomSource(roll);
        var result = BehaviorPlanner.ChooseAutonomousAction(new List<string> { "dance" }, random, allowJump: false);
        Assert.Equal(AutonomousActionKind.NoOp, result.Kind);
    }
}

