// src/ChiikawaDesktopPet.Core/BongoSpeedTracker.cs
using System;
using System.Collections.Generic;

namespace ChiikawaDesktopPet.Core;

public enum BongoPaw
{
    Left,
    Right
}

public enum BongoSpeedTier
{
    Idle,
    Normal,
    Focused,
    Overdrive
}

public class BongoSpeedTracker
{
    private readonly Queue<double> _keyTimestamps = new();
    private readonly Func<double> _timeSource;
    private double _lastKeyPressTime = -1;
    private BongoPaw _lastPaw = BongoPaw.Right;

    public double WindowSeconds { get; set; } = 3.0;
    public double IdleTimeoutSeconds { get; set; } = 1.5;
    public int FocusCpmThreshold { get; set; } = 120;
    public int OverdriveCpmThreshold { get; set; } = 300;

    public BongoSpeedTracker(Func<double>? timeSource = null)
    {
        _timeSource = timeSource ?? (() => Environment.TickCount64 / 1000.0);
    }

    /// <summary>
    /// Records a key press and returns the paw that should be pressed down.
    /// </summary>
    public BongoPaw RecordKeyPress(int virtualKey, bool alternateIfRapid = true)
    {
        double now = _timeSource();
        BongoPaw paw = DeterminePawForKey(virtualKey);

        if (alternateIfRapid && _lastKeyPressTime >= 0 && (now - _lastKeyPressTime) < 0.08)
        {
            // Rapid typing: alternate paws to create energetic drumming motion
            paw = (_lastPaw == BongoPaw.Left) ? BongoPaw.Right : BongoPaw.Left;
        }

        _lastPaw = paw;
        _lastKeyPressTime = now;
        _keyTimestamps.Enqueue(now);
        PruneOldKeys(now);

        return paw;
    }

    /// <summary>
    /// Records a mouse button click, updating CPM calculation and moving right paw.
    /// </summary>
    public void RecordMouseClick(bool isLeftButton)
    {
        double now = _timeSource();
        _lastPaw = BongoPaw.Right;
        _lastKeyPressTime = now;
        _keyTimestamps.Enqueue(now);
        PruneOldKeys(now);
    }


    /// <summary>
    /// Calculates the current CPM (Characters Per Minute).
    /// </summary>
    public double GetCurrentCpm()
    {
        double now = _timeSource();
        PruneOldKeys(now);

        if (_lastKeyPressTime < 0 || (now - _lastKeyPressTime) > IdleTimeoutSeconds || _keyTimestamps.Count == 0)
        {
            return 0.0;
        }

        double span = Math.Min(WindowSeconds, Math.Max(0.5, now - (_lastKeyPressTime - WindowSeconds)));
        if (span <= 0) span = 1.0;

        return (_keyTimestamps.Count / span) * 60.0;
    }

    /// <summary>
    /// Gets the current speed tier based on CPM thresholds.
    /// </summary>
    public BongoSpeedTier GetCurrentTier()
    {
        double now = _timeSource();
        if (_lastKeyPressTime < 0 || (now - _lastKeyPressTime) > IdleTimeoutSeconds)
        {
            return BongoSpeedTier.Idle;
        }

        double cpm = GetCurrentCpm();
        if (cpm >= OverdriveCpmThreshold) return BongoSpeedTier.Overdrive;
        if (cpm >= FocusCpmThreshold) return BongoSpeedTier.Focused;
        return BongoSpeedTier.Normal;
    }

    private void PruneOldKeys(double now)
    {
        double cutoff = now - WindowSeconds;
        while (_keyTimestamps.Count > 0 && _keyTimestamps.Peek() < cutoff)
        {
            _keyTimestamps.Dequeue();
        }
    }

    // ponytail: reuse BongoKeyboardLayout.IsLeftHandKey rather than duplicating a 50-line partition switch
    public static BongoPaw DeterminePawForKey(int virtualKey) =>
        BongoKeyboardLayout.IsLeftHandKey(virtualKey) ? BongoPaw.Left : BongoPaw.Right;
}
