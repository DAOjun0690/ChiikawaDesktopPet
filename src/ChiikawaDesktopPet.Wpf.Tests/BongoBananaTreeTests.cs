// src/ChiikawaDesktopPet.Wpf.Tests/BongoBananaTreeTests.cs
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChiikawaDesktopPet.Core;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class BongoBananaTreeTests
{
    [Fact]
    public void SkinManager_ExcludesSharedFolder_AndLoadsBananaTreeAndMonkeyEatFrames()
    {
        var manager = new BongoSkinManager();
        var skins = manager.DiscoverSkins();

        Assert.DoesNotContain(skins, s => s.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase));

        var treeAssets = manager.LoadBananaTreeAssets();
        Assert.NotNull(treeAssets.TreeBase);
        Assert.NotNull(treeAssets.Stage1Sprout);
        Assert.NotNull(treeAssets.Stage2Small);
        Assert.NotNull(treeAssets.Stage3Medium);
        Assert.NotNull(treeAssets.Stage4Ripe);

        var monkey = manager.LoadSkin("chesthair_monkey");
        Assert.Equal(20, monkey.EatFrames.Count);

        var chiikawa = manager.LoadSkin("chiikawa");
        Assert.Empty(chiikawa.EatFrames);
    }

    [Fact]
    public void BongoWindow_BananaGrowthAndMonkeyEatingFlow_WorksEndToEnd()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var config = new BongoConfig
                {
                    IsEnabled = true,
                    CurrentSkinKey = "chiikawa",
                    BananaThreshold = 4
                };
                var skinManager = new BongoSkinManager();
                using var keyboardHook = new GlobalKeyboardHook();
                using var mouseHook = new GlobalMouseHook();
                var window = new BongoWindow(config, skinManager, keyboardHook, mouseHook);
                window.ApplySkin("chiikawa");

                int statsSavedCount = 0;
                window.BananaStatsChanged += () => statsSavedCount++;

                // Initial state: empty tree, fruit collapsed
                Assert.Equal(BananaGrowthStage.Empty, window.CurrentBananaStage);
                Assert.Equal(Visibility.Collapsed, window.BananaFruitImage.Visibility);
                Assert.False(window.CanCurrentSkinEatBanana);

                // Simulate 4 Ctrl+C / Ctrl+V presses (with a repeated C key-down that must be ignored)
                window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkLControl);
                window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkC);
                window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkC); // key-repeat ignored
                window.ProcessKeyUpOnUiThread(BongoBananaTracker.VkC);
                Assert.Equal(1, window.BananaTracker.CurrentProgress);
                Assert.Equal(BananaGrowthStage.Sprout, window.CurrentBananaStage);
                Assert.Equal(Visibility.Visible, window.BananaFruitImage.Visibility);

                window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkV);
                window.ProcessKeyUpOnUiThread(BongoBananaTracker.VkV);
                Assert.Equal(BananaGrowthStage.Small, window.CurrentBananaStage);

                window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkC);
                window.ProcessKeyUpOnUiThread(BongoBananaTracker.VkC);
                Assert.Equal(BananaGrowthStage.Medium, window.CurrentBananaStage);

                window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkV);
                window.ProcessKeyUpOnUiThread(BongoBananaTracker.VkV);
                window.ProcessKeyUpOnUiThread(BongoBananaTracker.VkLControl);
                Assert.Equal(BananaGrowthStage.Ripe, window.CurrentBananaStage);
                Assert.Equal(4, config.BananaCopyPasteProgress);
                Assert.Equal(4, config.TotalCopyPasteCount);

                // Non-monkey skin (chiikawa) cannot eat ripe banana
                Assert.False(window.TryEatRipeBanana());
                Assert.False(window.IsEatingBanana);
                Assert.Equal(BananaGrowthStage.Ripe, window.CurrentBananaStage);
                Assert.Contains("切換至猴子造型", window.BananaTreeContainer.ToolTip?.ToString() ?? string.Empty);

                // Switch to chesthair_monkey -> now eating works!
                window.ApplySkin("chesthair_monkey");
                Assert.True(window.CanCurrentSkinEatBanana);
                Assert.Contains("左鍵點擊餵猴子吃", window.BananaTreeContainer.ToolTip?.ToString() ?? string.Empty);

                Assert.True(window.TryEatRipeBanana());
                Assert.True(window.IsEatingBanana);
                Assert.Equal(BananaGrowthStage.Empty, window.CurrentBananaStage);
                Assert.Equal(0, config.BananaCopyPasteProgress);
                Assert.Equal(1, config.TotalBananasEaten);
                Assert.Equal(1, statsSavedCount);

                // While eating, eat image is visible and typing paws/face are hidden
                Assert.Equal(Visibility.Visible, window.EatAnimationImage.Visibility);
                Assert.Equal(Visibility.Collapsed, window.FaceImage.Visibility);
                Assert.Equal(Visibility.Collapsed, window.LeftPawImage.Visibility);
                Assert.Equal(Visibility.Collapsed, window.RightPawImage.Visibility);

                // Typing during eating must NOT interrupt the eating animation
                window.ProcessKeyDownOnUiThread(0x41); // 'A'
                window.ProcessKeyUpOnUiThread(0x41);
                Assert.True(window.IsEatingBanana);
                Assert.Equal(Visibility.Visible, window.EatAnimationImage.Visibility);

                // Advance through 2 full loops (20 frames * 2 = 40 ticks) -> returns to typing state
                for (int i = 0; i < 40; i++)
                {
                    window.AdvanceEatAnimationFrame();
                }

                Assert.False(window.IsEatingBanana);
                Assert.Equal(Visibility.Collapsed, window.EatAnimationImage.Visibility);
                Assert.Equal(Visibility.Visible, window.FaceImage.Visibility);
                Assert.Equal(Visibility.Visible, window.LeftPawImage.Visibility);
                Assert.Equal(Visibility.Visible, window.RightPawImage.Visibility);

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void BongoWindow_RenderVerificationScreenshots_ProducesNonEmptyImages()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                string outDir = Environment.GetEnvironmentVariable("BONGO_SCREENSHOT_DIR")
                    ?? Path.Combine(Path.GetTempPath(), "yaha_pet_banana_screenshots");
                Directory.CreateDirectory(outDir);

                var config = new BongoConfig
                {
                    IsEnabled = true,
                    CurrentSkinKey = "chesthair_monkey",
                    Scale = 2.0, // 1:1 visual scale (2.0 * BaseVisualScale 0.5 = 1.0) for crisp screenshots
                    BananaThreshold = 20
                };
                var skinManager = new BongoSkinManager();
                using var keyboardHook = new GlobalKeyboardHook();
                using var mouseHook = new GlobalMouseHook();
                var window = new BongoWindow(config, skinManager, keyboardHook, mouseHook);
                window.ApplyScale(2.0);
                window.ApplySkin("chesthair_monkey");

                // 1. Stage 0 (Empty tree)
                RenderVisualToPng(window.PetVisualContainer, Path.Combine(outDir, "01_stage_empty.png"));

                // 2. Stage 2 (Small banana: 10/20)
                for (int i = 0; i < 10; i++)
                {
                    window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkLControl);
                    window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkC);
                    window.ProcessKeyUpOnUiThread(BongoBananaTracker.VkC);
                    window.ProcessKeyUpOnUiThread(BongoBananaTracker.VkLControl);
                }
                RenderVisualToPng(window.PetVisualContainer, Path.Combine(outDir, "02_stage_small.png"));

                // 3. Stage 4 (Ripe glowing banana: 20/20)
                for (int i = 0; i < 10; i++)
                {
                    window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkLControl);
                    window.ProcessKeyDownOnUiThread(BongoBananaTracker.VkV);
                    window.ProcessKeyUpOnUiThread(BongoBananaTracker.VkV);
                    window.ProcessKeyUpOnUiThread(BongoBananaTracker.VkLControl);
                }
                Assert.Equal(BananaGrowthStage.Ripe, window.CurrentBananaStage);
                RenderVisualToPng(window.PetVisualContainer, Path.Combine(outDir, "03_stage_ripe.png"));

                // 4. Eating banana state (after clicking ripe banana)
                Assert.True(window.TryEatRipeBanana());
                RenderVisualToPng(window.PetVisualContainer, Path.Combine(outDir, "04_monkey_eating.png"));

                Assert.True(new FileInfo(Path.Combine(outDir, "01_stage_empty.png")).Length > 1000);
                Assert.True(new FileInfo(Path.Combine(outDir, "02_stage_small.png")).Length > 1000);
                Assert.True(new FileInfo(Path.Combine(outDir, "03_stage_ripe.png")).Length > 1000);
                Assert.True(new FileInfo(Path.Combine(outDir, "04_monkey_eating.png")).Length > 1000);

                window.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    private static void RenderVisualToPng(FrameworkElement element, string filePath)
    {
        element.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();

        int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Light neutral backdrop so transparency & glow read clearly in screenshots
            dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 246, 250)), null, new Rect(0, 0, width, height));
            var brush = new VisualBrush(element) { Stretch = Stretch.None };
            dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(filePath);
        encoder.Save(fs);
    }
}
