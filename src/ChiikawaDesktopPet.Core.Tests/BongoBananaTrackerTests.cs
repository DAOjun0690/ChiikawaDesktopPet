// src/ChiikawaDesktopPet.Core.Tests/BongoBananaTrackerTests.cs
using System;
using System.IO;
using ChiikawaDesktopPet.Core;
using Xunit;

namespace ChiikawaDesktopPet.Core.Tests;

public class BongoBananaTrackerTests
{
    [Fact]
    public void PlainCAndV_WithoutCtrl_DoNotIncrement()
    {
        var tracker = new BongoBananaTracker(threshold: 20);

        Assert.False(tracker.OnKeyDown(BongoBananaTracker.VkC));
        tracker.OnKeyUp(BongoBananaTracker.VkC);
        Assert.False(tracker.OnKeyDown(BongoBananaTracker.VkV));
        tracker.OnKeyUp(BongoBananaTracker.VkV);

        Assert.Equal(0, tracker.CurrentProgress);
        Assert.Equal(0, tracker.TotalCopyPasteCount);
        Assert.Equal(BananaGrowthStage.Empty, tracker.Stage);
    }

    [Fact]
    public void CtrlCAndCtrlV_DeduplicatesKeyRepeatUntilKeyUp()
    {
        var tracker = new BongoBananaTracker(threshold: 20);

        // Hold Left Ctrl + hold C (multiple repeated WM_KEYDOWN)
        Assert.False(tracker.OnKeyDown(BongoBananaTracker.VkLControl));
        Assert.True(tracker.OnKeyDown(BongoBananaTracker.VkC));
        Assert.False(tracker.OnKeyDown(BongoBananaTracker.VkC));
        Assert.False(tracker.OnKeyDown(BongoBananaTracker.VkC));
        tracker.OnKeyUp(BongoBananaTracker.VkC);

        // Press V while Ctrl still held, with key repeat
        Assert.True(tracker.OnKeyDown(BongoBananaTracker.VkV));
        Assert.False(tracker.OnKeyDown(BongoBananaTracker.VkV));
        tracker.OnKeyUp(BongoBananaTracker.VkV);
        tracker.OnKeyUp(BongoBananaTracker.VkLControl);

        // Right Ctrl + C also counts
        Assert.False(tracker.OnKeyDown(BongoBananaTracker.VkRControl));
        Assert.True(tracker.OnKeyDown(BongoBananaTracker.VkC));
        tracker.OnKeyUp(BongoBananaTracker.VkC);
        tracker.OnKeyUp(BongoBananaTracker.VkRControl);

        Assert.Equal(3, tracker.CurrentProgress);
        Assert.Equal(3, tracker.TotalCopyPasteCount);
    }

    [Fact]
    public void GrowthStages_ProgressThroughAllFiveStages_AndHarvestResets()
    {
        var tracker = new BongoBananaTracker(threshold: 20);

        Assert.Equal(BananaGrowthStage.Empty, tracker.Stage);
        Assert.False(tracker.IsRipe);
        Assert.False(tracker.TryHarvest());

        // 1..6 -> Sprout (1..33%)
        for (int i = 0; i < 6; i++) tracker.RecordCopyPaste();
        Assert.Equal(BananaGrowthStage.Sprout, tracker.Stage);

        // 7..13 -> Small (34..66%)
        tracker.RecordCopyPaste(); // 7
        Assert.Equal(BananaGrowthStage.Small, tracker.Stage);
        for (int i = 0; i < 6; i++) tracker.RecordCopyPaste(); // 13
        Assert.Equal(BananaGrowthStage.Small, tracker.Stage);

        // 14..19 -> Medium (67..99%)
        tracker.RecordCopyPaste(); // 14
        Assert.Equal(BananaGrowthStage.Medium, tracker.Stage);
        for (int i = 0; i < 5; i++) tracker.RecordCopyPaste(); // 19
        Assert.Equal(BananaGrowthStage.Medium, tracker.Stage);
        Assert.False(tracker.IsRipe);

        // 20 -> Ripe (100%)
        tracker.RecordCopyPaste(); // 20
        Assert.Equal(BananaGrowthStage.Ripe, tracker.Stage);
        Assert.True(tracker.IsRipe);

        // Extra copy-paste while ripe caps tree progress at 20, but still increments total count
        tracker.RecordCopyPaste(); // 21 total
        Assert.Equal(20, tracker.CurrentProgress);
        Assert.Equal(21, tracker.TotalCopyPasteCount);

        // Harvest resets tree progress to 0 and increments eaten count
        Assert.True(tracker.TryHarvest());
        Assert.Equal(0, tracker.CurrentProgress);
        Assert.Equal(BananaGrowthStage.Empty, tracker.Stage);
        Assert.Equal(1, tracker.TotalBananasEaten);
        Assert.Equal(21, tracker.TotalCopyPasteCount);
        Assert.False(tracker.TryHarvest());
    }

    [Fact]
    public void ResetKeyState_ClearsHeldModifierLatches()
    {
        var tracker = new BongoBananaTracker(threshold: 10);
        tracker.OnKeyDown(BongoBananaTracker.VkLControl);
        Assert.True(tracker.IsCtrlDown);

        tracker.ResetKeyState();
        Assert.False(tracker.IsCtrlDown);
        Assert.False(tracker.OnKeyDown(BongoBananaTracker.VkC));
    }

    [Fact]
    public void SettingsPersistence_RoundTripsBananaStats_AndSupportsLegacyJson()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"bongo_banana_settings_{Guid.NewGuid():N}.json");
        try
        {
            var settings = new AppSettings();
            var tracker = new BongoBananaTracker(threshold: 15, initialProgress: 12, totalCopyPasteCount: 42, totalBananasEaten: 2);
            tracker.SyncToConfig(settings.Bongo);

            SettingsManager.Save(settings, tempFile);
            var loaded = SettingsManager.Load(tempFile);

            Assert.Equal(15, loaded.Bongo.BananaThreshold);
            Assert.Equal(12, loaded.Bongo.BananaCopyPasteProgress);
            Assert.Equal(42, loaded.Bongo.TotalCopyPasteCount);
            Assert.Equal(2, loaded.Bongo.TotalBananasEaten);

            // Legacy JSON without banana properties falls back to defaults cleanly
            File.WriteAllText(tempFile, """{"Bongo":{"IsEnabled":true,"CurrentSkinKey":"chesthair_monkey"}}""");
            var legacy = SettingsManager.Load(tempFile);
            Assert.Equal(20, legacy.Bongo.BananaThreshold);
            Assert.Equal(0, legacy.Bongo.BananaCopyPasteProgress);
            Assert.Equal(0, legacy.Bongo.TotalCopyPasteCount);
            Assert.Equal(0, legacy.Bongo.TotalBananasEaten);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void FormatTooltip_ReflectsStateAndSkinCapability()
    {
        var tracker = new BongoBananaTracker(threshold: 2, initialProgress: 1, totalCopyPasteCount: 5, totalBananasEaten: 2);
        string unripeTip = tracker.FormatTooltip(canEat: true);
        Assert.Contains("1 / 2", unripeTip);
        Assert.Contains("5", unripeTip);
        Assert.Contains("2", unripeTip);

        tracker.RecordCopyPaste();
        Assert.Contains("左鍵點擊餵猴子吃", tracker.FormatTooltip(canEat: true));
        Assert.Contains("切換至猴子造型", tracker.FormatTooltip(canEat: false));
    }
}
