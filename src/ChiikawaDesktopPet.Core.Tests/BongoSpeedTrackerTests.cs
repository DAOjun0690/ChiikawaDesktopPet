// src/ChiikawaDesktopPet.Core.Tests/BongoSpeedTrackerTests.cs
using System;
using Xunit;
using ChiikawaDesktopPet.Core;

namespace ChiikawaDesktopPet.Core.Tests;

public class BongoSpeedTrackerTests
{
    [Fact]
    public void DeterminePawForKey_LeftAndRightKeys_CorrectlyPartitioned()
    {
        // Left hand keys
        Assert.Equal(BongoPaw.Left, BongoSpeedTracker.DeterminePawForKey(0x51)); // Q
        Assert.Equal(BongoPaw.Left, BongoSpeedTracker.DeterminePawForKey(0x57)); // W
        Assert.Equal(BongoPaw.Left, BongoSpeedTracker.DeterminePawForKey(0x41)); // A
        Assert.Equal(BongoPaw.Left, BongoSpeedTracker.DeterminePawForKey(0x09)); // Tab

        // Right hand keys
        Assert.Equal(BongoPaw.Right, BongoSpeedTracker.DeterminePawForKey(0x4F)); // O
        Assert.Equal(BongoPaw.Right, BongoSpeedTracker.DeterminePawForKey(0x50)); // P
        Assert.Equal(BongoPaw.Right, BongoSpeedTracker.DeterminePawForKey(0x4B)); // K
        Assert.Equal(BongoPaw.Right, BongoSpeedTracker.DeterminePawForKey(0x4C)); // L
        Assert.Equal(BongoPaw.Right, BongoSpeedTracker.DeterminePawForKey(0x20)); // Space
        Assert.Equal(BongoPaw.Right, BongoSpeedTracker.DeterminePawForKey(0x0D)); // Enter
    }

    [Fact]
    public void RecordKeyPress_RapidKeyPress_AlternatesPaws()
    {
        double currentTime = 100.0;
        var tracker = new BongoSpeedTracker(() => currentTime);

        // Press 'Q' (Left)
        var paw1 = tracker.RecordKeyPress(0x51, alternateIfRapid: true);
        Assert.Equal(BongoPaw.Left, paw1);

        // Press 'W' (Left) 30ms later -> should alternate to Right for energetic drumming
        currentTime += 0.03;
        var paw2 = tracker.RecordKeyPress(0x57, alternateIfRapid: true);
        Assert.Equal(BongoPaw.Right, paw2);

        // Press 'E' (Left) 30ms later -> should alternate back to Left
        currentTime += 0.03;
        var paw3 = tracker.RecordKeyPress(0x45, alternateIfRapid: true);
        Assert.Equal(BongoPaw.Left, paw3);
    }

    [Fact]
    public void GetCurrentTier_SpeedTiers_TransitionCorrectly()
    {
        double currentTime = 0.0;
        var tracker = new BongoSpeedTracker(() => currentTime)
        {
            WindowSeconds = 3.0,
            IdleTimeoutSeconds = 1.0,
            FocusCpmThreshold = 100,
            OverdriveCpmThreshold = 250
        };

        // Initially Idle
        Assert.Equal(BongoSpeedTier.Idle, tracker.GetCurrentTier());

        // Single key press -> Normal
        currentTime = 1.0;
        tracker.RecordKeyPress(0x41);
        Assert.Equal(BongoSpeedTier.Normal, tracker.GetCurrentTier());

        // Fast typing: 8 presses in 2 seconds = 240 CPM -> Focused
        for (int i = 0; i < 7; i++)
        {
            currentTime += 0.25;
            tracker.RecordKeyPress(i % 2 == 0 ? 0x4A : 0x41);
        }
        Assert.Equal(BongoSpeedTier.Focused, tracker.GetCurrentTier());

        // Furious typing: 20 presses in 1 second = 600+ CPM -> Overdrive
        for (int i = 0; i < 20; i++)
        {
            currentTime += 0.05;
            tracker.RecordKeyPress(i % 2 == 0 ? 0x4A : 0x41);
        }
        Assert.Equal(BongoSpeedTier.Overdrive, tracker.GetCurrentTier());

        // Idle timeout: jump ahead 2 seconds with no typing
        currentTime += 2.0;
        Assert.Equal(BongoSpeedTier.Idle, tracker.GetCurrentTier());
        Assert.Equal(0.0, tracker.GetCurrentCpm());
    }

    [Fact]
    public void RecordMouseClick_ContributesToCpmAndSpeedTiers()
    {
        double currentTime = 0.0;
        var tracker = new BongoSpeedTracker(() => currentTime)
        {
            WindowSeconds = 3.0,
            IdleTimeoutSeconds = 1.0,
            FocusCpmThreshold = 100,
            OverdriveCpmThreshold = 250
        };

        // Click left button -> Normal tier
        currentTime = 1.0;
        tracker.RecordMouseClick(isLeftButton: true);
        Assert.Equal(BongoSpeedTier.Normal, tracker.GetCurrentTier());

        // Repeated mouse clicks in rapid succession -> Focused tier
        for (int i = 0; i < 7; i++)
        {
            currentTime += 0.25;
            tracker.RecordMouseClick(isLeftButton: i % 2 == 0);
        }
        Assert.Equal(BongoSpeedTier.Focused, tracker.GetCurrentTier());
    }
}

