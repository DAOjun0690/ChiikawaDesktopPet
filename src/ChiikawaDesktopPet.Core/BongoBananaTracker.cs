// src/ChiikawaDesktopPet.Core/BongoBananaTracker.cs
using System;

namespace ChiikawaDesktopPet.Core;

public enum BananaGrowthStage
{
    Empty = 0,
    Sprout = 1,
    Small = 2,
    Medium = 3,
    Ripe = 4
}

public class BongoBananaTracker
{
    public const int VkControl = 0x11;
    public const int VkLControl = 0xA2;
    public const int VkRControl = 0xA3;
    public const int VkC = 0x43;
    public const int VkV = 0x56;

    private bool _leftCtrlDown;
    private bool _rightCtrlDown;
    private bool _genericCtrlDown;
    private bool _cDown;
    private bool _vDown;

    public int Threshold { get; private set; }
    public int CurrentProgress { get; private set; }
    public int TotalCopyPasteCount { get; private set; }
    public int TotalBananasEaten { get; private set; }

    public bool IsCtrlDown => _leftCtrlDown || _rightCtrlDown || _genericCtrlDown;
    public bool IsRipe => CurrentProgress >= Threshold;

    public BananaGrowthStage Stage
    {
        get
        {
            if (CurrentProgress <= 0) return BananaGrowthStage.Empty;
            if (CurrentProgress >= Threshold) return BananaGrowthStage.Ripe;

            double ratio = (double)CurrentProgress / Threshold;
            if (ratio <= 1.0 / 3.0) return BananaGrowthStage.Sprout;
            if (ratio <= 2.0 / 3.0) return BananaGrowthStage.Small;
            return BananaGrowthStage.Medium;
        }
    }

    public BongoBananaTracker(int threshold = 20, int initialProgress = 0, int totalCopyPasteCount = 0, int totalBananasEaten = 0)
    {
        Threshold = Math.Max(1, threshold);
        CurrentProgress = Math.Clamp(initialProgress, 0, Threshold);
        TotalCopyPasteCount = Math.Max(0, totalCopyPasteCount);
        TotalBananasEaten = Math.Max(0, totalBananasEaten);
    }

    public static BongoBananaTracker FromConfig(BongoConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new BongoBananaTracker(
            threshold: config.BananaThreshold,
            initialProgress: config.BananaCopyPasteProgress,
            totalCopyPasteCount: config.TotalCopyPasteCount,
            totalBananasEaten: config.TotalBananasEaten);
    }

    /// <summary>
    /// Processes a key-down event. Returns true if a new (non-repeated) Ctrl+C or Ctrl+V was detected.
    /// </summary>
    public bool OnKeyDown(int vkCode)
    {
        switch (vkCode)
        {
            case VkLControl:
                _leftCtrlDown = true;
                return false;
            case VkRControl:
                _rightCtrlDown = true;
                return false;
            case VkControl:
                _genericCtrlDown = true;
                return false;
            case VkC:
                if (_cDown) return false;
                _cDown = true;
                if (IsCtrlDown)
                {
                    RecordCopyPaste();
                    return true;
                }
                return false;
            case VkV:
                if (_vDown) return false;
                _vDown = true;
                if (IsCtrlDown)
                {
                    RecordCopyPaste();
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// Processes a key-up event to release modifier and key-repeat latches.
    /// </summary>
    public void OnKeyUp(int vkCode)
    {
        switch (vkCode)
        {
            case VkLControl:
                _leftCtrlDown = false;
                break;
            case VkRControl:
                _rightCtrlDown = false;
                break;
            case VkControl:
                _genericCtrlDown = false;
                break;
            case VkC:
                _cDown = false;
                break;
            case VkV:
                _vDown = false;
                break;
        }
    }

    public void ResetKeyState()
    {
        _leftCtrlDown = false;
        _rightCtrlDown = false;
        _genericCtrlDown = false;
        _cDown = false;
        _vDown = false;
    }

    public void RecordCopyPaste()
    {
        TotalCopyPasteCount++;
        if (CurrentProgress < Threshold)
        {
            CurrentProgress++;
        }
    }

    /// <summary>
    /// Harvests the ripe banana, resetting tree progress to 0 and incrementing TotalBananasEaten.
    /// Returns false if the banana is not yet ripe.
    /// </summary>
    public bool TryHarvest()
    {
        if (!IsRipe) return false;

        CurrentProgress = 0;
        TotalBananasEaten++;
        return true;
    }

    public void SyncToConfig(BongoConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.BananaThreshold = Threshold;
        config.BananaCopyPasteProgress = CurrentProgress;
        config.TotalCopyPasteCount = TotalCopyPasteCount;
        config.TotalBananasEaten = TotalBananasEaten;
    }

    public string FormatTooltip(bool canEat)
    {
        string header = IsRipe
            ? (canEat
                ? "🍌 香蕉熟成了！（左鍵點擊餵猴子吃）"
                : "🍌 香蕉熟成了！（切換至猴子造型即可點擊享用）")
            : "🌴 香蕉樹";

        return $"{header}\nCtrl+C / Ctrl+V：{CurrentProgress} / {Threshold}\n累計複製貼上：{TotalCopyPasteCount} 次｜已吃掉：{TotalBananasEaten} 根";
    }
}
