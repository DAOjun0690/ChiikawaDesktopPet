using System;

namespace ChiikawaDesktopPet.Core;

public static partial class BehaviorPlanner
{
    public enum JumpDirection { Left, Right }

    public sealed record JumpPlan(
        JumpDirection Direction,
        int DurationMs,
        PetPoint RiseTarget,
        PetPoint LandTarget);

    /// Faithful port of the original's jump direction/edge-avoidance logic, including its
    /// known quirk: when rolled "left" but too close to the left edge, the animation still
    /// plays as "jumpleft" even though the computed movement goes right. This mirrors the
    /// shipped Python behavior exactly (chosen_direction always follows direction_roll,
    /// independent of which end_range_x branch actually ran).
    ///
    /// minX/maxX are the usable horizontal bounds (e.g. the full multi-monitor virtual
    /// desktop) — see PlanWalk's minX/maxX doc for why this isn't just a single screenWidth
    /// starting at 0.
    public static JumpPlan PlanJump(PetPoint currentPos, int characterHeight, int minX, int maxX, int landingY, IRandomSource random, JumpDirection? forcedDirection = null)
    {
        int directionRoll = forcedDirection.HasValue
            ? (forcedDirection.Value == JumpDirection.Left ? 0 : 1)
            : random.Next(0, 2);
        if (!forcedDirection.HasValue && directionRoll == 1 && currentPos.X >= maxX - 100)
            directionRoll = 0;

        int endRangeX;
        if (directionRoll == 0 && currentPos.X > minX + 100)
        {
            endRangeX = currentPos.X - random.Next(0, 101);
        }
        else
        {
            endRangeX = currentPos.X + random.Next(0, 101);
        }
        endRangeX = Math.Clamp(endRangeX, minX, maxX);

        var direction = directionRoll == 0 ? JumpDirection.Left : JumpDirection.Right;

        int jumpHeight = random.Next(50, 301);
        int firstHalfX = (currentPos.X + endRangeX) / 2;

        int durationMs = jumpHeight * 10;
        var riseTarget = new PetPoint(firstHalfX, currentPos.Y - jumpHeight - characterHeight);
        var landTarget = new PetPoint(endRangeX, landingY - characterHeight);

        return new JumpPlan(direction, durationMs, riseTarget, landTarget);
    }
}
