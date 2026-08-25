// src/YahaPet.Core/InteractionPlanner.cs
using System;

namespace YahaPet.Core;

public static class InteractionPlanner
{
    public const int DefaultDistanceThreshold = 150;
    public const int DefaultTriggerProbabilityPercent = 40;
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Checks if two pet positions are within the interaction distance threshold.
    /// </summary>
    public static bool IsInRange(PetPoint posA, PetPoint posB, int threshold = DefaultDistanceThreshold)
    {
        long dx = posA.X - posB.X;
        long dy = posA.Y - posB.Y;
        return (dx * dx + dy * dy) <= ((long)threshold * threshold);
    }

    /// <summary>
    /// Checks if two pet bounding boxes overlap.
    /// </summary>
    public static bool IsOverlapping(PetPoint posA, int widthA, int heightA, PetPoint posB, int widthB, int heightB)
    {
        return posA.X < posB.X + widthB &&
               posA.X + widthA > posB.X &&
               posA.Y < posB.Y + heightB &&
               posA.Y + heightA > posB.Y;
    }

    /// <summary>
    /// Rolls for interaction trigger probability [0, 100).
    /// </summary>
    public static bool ShouldTrigger(IRandomSource random, int probabilityPercent = DefaultTriggerProbabilityPercent)
    {
        if (probabilityPercent <= 0) return false;
        if (probabilityPercent >= 100) return true;
        return random.Next(0, 100) < probabilityPercent;
    }

    /// <summary>
    /// Calculates the top-left placement of the interaction popup window centered between two pets,
    /// clamped within available screen bounds.
    /// </summary>
    public static PetPoint CalculateInteractionWindowPosition(
        PetPoint posA,
        int widthA,
        int heightA,
        PetPoint posB,
        int widthB,
        int heightB,
        int windowWidth,
        int windowHeight,
        PetBounds screenBounds)
    {
        int centerAx = posA.X + widthA / 2;
        int centerAy = posA.Y + heightA / 2;
        int centerBx = posB.X + widthB / 2;
        int centerBy = posB.Y + heightB / 2;

        int midCenterX = (centerAx + centerBx) / 2;
        int midCenterY = (centerAy + centerBy) / 2;

        int targetX = midCenterX - windowWidth / 2;
        int targetY = midCenterY - windowHeight / 2;

        return BehaviorPlanner.ClampToBounds(new PetPoint(targetX, targetY), screenBounds, windowWidth, windowHeight);
    }

    /// <summary>
    /// Calculates the landing/reappear positions for the two characters after the interaction finishes,
    /// placed directly resting on the taskbar/screen bottom and separated horizontally.
    /// </summary>
    public static (PetPoint PosA, PetPoint PosB) CalculateReappearPositions(
        PetPoint interactionCenter,
        int spacing,
        PetBounds screenBounds,
        int petWidthA,
        int petHeightA,
        int petWidthB,
        int petHeightB)
    {
        int ax = interactionCenter.X - spacing / 2 - petWidthA / 2;
        int ay = screenBounds.Bottom - petHeightA;
        int bx = interactionCenter.X + spacing / 2 - petWidthB / 2;
        int by = screenBounds.Bottom - petHeightB;

        var clampedA = BehaviorPlanner.ClampToBounds(new PetPoint(ax, ay), screenBounds, petWidthA, petHeightA);
        var clampedB = BehaviorPlanner.ClampToBounds(new PetPoint(bx, by), screenBounds, petWidthB, petHeightB);

        return (clampedA, clampedB);
    }

    /// <summary>
    /// Checks whether an interaction cooldown is still in effect.
    /// </summary>
    public static bool IsCooldownActive(DateTime lastTriggerTime, DateTime now, TimeSpan cooldownDuration)
    {
        if (lastTriggerTime == DateTime.MinValue) return false;
        return (now - lastTriggerTime) < cooldownDuration;
    }
}
