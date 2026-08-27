namespace ChiikawaDesktopPet.Core;

/// Pure, UI-free decision logic for autonomous pet behavior. Every method here
/// takes an IRandomSource explicitly so callers can test with fixed roll sequences.
public static partial class BehaviorPlanner
{
    /// Weighted roll (jump 10% / walk 35% / talk 15% / other 35% / no-op 5%).
    public static AutonomousAction ChooseAutonomousAction(IReadOnlyList<string> otherAnimationNames, IRandomSource random, bool allowJump = true)
    {
        int roll = random.Next(0, 100);
        if (roll < 10)
        {
            return allowJump ? new AutonomousAction(AutonomousActionKind.Jump) : new AutonomousAction(AutonomousActionKind.NoOp);
        }
        if (roll < 45) return new AutonomousAction(AutonomousActionKind.Walk);
        if (roll < 60) return new AutonomousAction(AutonomousActionKind.Talk);
        if (roll < 95)
        {
            if (otherAnimationNames.Count == 0) return new AutonomousAction(AutonomousActionKind.NoOp);
            int index = random.Next(0, otherAnimationNames.Count);
            return new AutonomousAction(AutonomousActionKind.PlayAnimation, otherAnimationNames[index]);
        }
        return new AutonomousAction(AutonomousActionKind.NoOp);
    }

    public static int NextIdleIntervalMs(IRandomSource random) => random.Next(3000, 10000);
}
