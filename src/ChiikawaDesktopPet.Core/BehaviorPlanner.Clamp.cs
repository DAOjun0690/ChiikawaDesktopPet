using System;

namespace ChiikawaDesktopPet.Core;

public static partial class BehaviorPlanner
{
    /// Faithful port of the original's clamp_to_screen: keeps a 5px margin on the
    /// right/width axis and a 1px margin on the bottom/height axis, matching the
    /// original's asymmetric constants exactly (not a rounding artifact).
    public static PetPoint ClampToBounds(PetPoint point, PetBounds availableBounds, int width, int height)
    {
        int x = Math.Max(availableBounds.Left, Math.Min(point.X, availableBounds.Right - width + 5));
        int y = Math.Max(availableBounds.Top, Math.Min(point.Y, availableBounds.Bottom - height + 1));
        return new PetPoint(x, y);
    }
}
