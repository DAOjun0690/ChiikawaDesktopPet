using System;

namespace YahaPet.Core;

public static partial class BehaviorPlanner
{
    /// Determines whether the pet's bottom edge is close enough to a window's top frame to snap onto it.
    public static bool ShouldSnapToWindow(int petBottomY, int windowTopY, int tolerance = 30)
    {
        return Math.Abs(petBottomY - windowTopY) <= tolerance;
    }

    /// Determines whether the pet has walked or stepped off either the left or right edge of a window.
    public static bool IsSteppedOffWindow(int petLeft, int petWidth, int windowLeft, int windowRight)
    {
        // When more than 50% of the character width is past either edge
        double centerX = petLeft + petWidth / 2.0;
        return centerX < windowLeft || centerX > windowRight;
    }

    /// Checks if the window is squeezed against the top of the monitor / screen.
    public static bool IsWindowSqueezed(int windowTopY, int minSafeTop = 0)
    {
        return windowTopY <= minSafeTop;
    }

    /// Calculates the world position of a pet attached to a window given the window's top-left and relative X offset.
    public static PetPoint CalculateAttachedPosition(int windowLeft, int windowTop, int relativeX, int characterHeight)
    {
        return new PetPoint(windowLeft + relativeX, windowTop - characterHeight);
    }
}
