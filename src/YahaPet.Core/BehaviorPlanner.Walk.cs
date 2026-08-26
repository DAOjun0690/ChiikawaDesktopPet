using System;

namespace YahaPet.Core;

public static partial class BehaviorPlanner
{
    public enum WalkDirection { Left, Right }

    public sealed record WalkPlan(WalkDirection Direction, int TargetX, int DurationMs);

    /// Returns null when no valid walk range exists (character too close to the
    /// rolled-direction edge), matching the original's silent no-op in that case.
    /// ponytail: unlike the original, this does not fall back to the opposite
    /// direction's frames when one animation folder is missing — the WPF port
    /// always auto-mirrors walkleft/walkright (see Task 10), so both directions
    /// are always available and that fallback branch has no work left to do.
    ///
    /// minX/maxX are the usable horizontal bounds (e.g. the full multi-monitor virtual
    /// desktop, not just the current monitor) — the original assumed a single screen
    /// starting at X=0, which stranded the character on one monitor of a multi-monitor
    /// setup since it could never plan a target below 0.
    public static WalkPlan? PlanWalk(PetPoint currentPos, int minX, int maxX, int characterWidth, IRandomSource random, WalkDirection? forcedDirection = null)
    {
        const int minMovementDistance = 100;
        bool canWalkLeft = minX < (currentPos.X - minMovementDistance);
        bool canWalkRight = (currentPos.X + minMovementDistance) < (maxX - characterWidth);

        if (!canWalkLeft && !canWalkRight) return null;

        WalkDirection direction;
        if (forcedDirection.HasValue)
        {
            direction = forcedDirection.Value;
            if (direction == WalkDirection.Left && !canWalkLeft) return null;
            if (direction == WalkDirection.Right && !canWalkRight) return null;
        }
        else
        {
            int rollDirection = random.Next(0, 2);
            if (rollDirection == 0)
            {
                direction = canWalkLeft ? WalkDirection.Left : WalkDirection.Right;
            }
            else
            {
                direction = canWalkRight ? WalkDirection.Right : WalkDirection.Left;
            }
        }

        int startRange, endRange;
        if (direction == WalkDirection.Left)
        {
            startRange = minX;
            endRange = currentPos.X - minMovementDistance;
        }
        else
        {
            startRange = currentPos.X + minMovementDistance;
            endRange = maxX - characterWidth;
        }

        if (startRange >= endRange) return null;

        int targetX = random.Next(startRange, endRange);
        int durationMs = 5 * Math.Abs(targetX - currentPos.X);
        return new WalkPlan(direction, targetX, durationMs);
    }
}
