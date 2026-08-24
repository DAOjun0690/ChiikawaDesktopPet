namespace YahaPet.Core;

/// Pure, UI-free decision logic for autonomous pet behavior. Every method here
/// takes an IRandomSource explicitly so callers can test with fixed roll sequences.
public static partial class BehaviorPlanner
{
    /// Corrected weighted roll (jump 10% / walk 40% / other 45% / no-op 5%).
    /// ponytail: the shipped Python original has `roll <= 100` (always true for a
    /// 0-99 roll), so it only ever jumps autonomously — deliberately not reproduced,
    /// see spec User Story 8a.
    public static AutonomousAction ChooseAutonomousAction(IReadOnlyList<string> otherAnimationNames, IRandomSource random, bool allowJump = true)
    {
        int roll = random.Next(0, 100);
        if (roll < 10)
        {
            return allowJump ? new AutonomousAction(AutonomousActionKind.Jump) : new AutonomousAction(AutonomousActionKind.NoOp);
        }
        if (roll < 50) return new AutonomousAction(AutonomousActionKind.Walk);
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
