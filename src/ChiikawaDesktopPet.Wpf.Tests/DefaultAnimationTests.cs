// src/ChiikawaDesktopPet.Wpf.Tests/DefaultAnimationTests.cs
using System;
using System.IO;
using System.Threading;
using ChiikawaDesktopPet.Core;
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class DefaultAnimationTests
{
    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw new Exception("STA thread failed", exception);
        }
    }

    [Fact]
    public void CharacterWindow_DefaultAnimation_DefaultsToNull()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chiikawa");
            Assert.Null(window.DefaultAnimation);
            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_SetDefaultAnimation_UpdatesPropertyAndFiresEvent()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chiikawa");
            string? eventValue = "not_set";
            window.DefaultAnimationChanged += val => eventValue = val;

            window.SetDefaultAnimation("dance");
            Assert.Equal("dance", window.DefaultAnimation);
            Assert.Equal("dance", eventValue);

            window.SetDefaultAnimation("   ");
            Assert.Null(window.DefaultAnimation);
            Assert.Null(eventValue);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_InPlaceAnimationNames_ExcludesWalkAndJump()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chiikawa");
            var inPlaceNames = window.InPlaceAnimationNames();

            Assert.DoesNotContain("walkleft", inPlaceNames, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("walkright", inPlaceNames, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("jumpleft", inPlaceNames, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("jumpright", inPlaceNames, StringComparer.OrdinalIgnoreCase);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_SpeechBubbleLayout_MeasuresAccurately()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chesthair_monkey");
            window.SetCustomText("PrPartnershipInf import BUSINESS_ENAME to Uppercase Yo");
            window.ShowSpeechBubble();

            var bubble = window.FindName("BubbleContainer") as System.Windows.Controls.Grid;
            var border = window.FindName("BubbleBorder") as System.Windows.Controls.Border;
            Assert.NotNull(bubble);
            Assert.NotNull(border);
            Assert.Equal(320, border.MaxWidth);

            Assert.True(window.Width >= border.DesiredSize.Width);
            Assert.True(window.Height >= bubble.DesiredSize.Height);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_SpeechBubble_AutoFlipsWhenSpaceInsufficient()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chesthair_monkey");
            window.Top = 10;
            window.SetCustomText("PrPartnershipInf import BUSINESS_ENAME to Uppercase Yo");
            window.ShowSpeechBubble();

            var bubble = window.FindName("BubbleContainer") as System.Windows.Controls.Grid;
            var sprite = window.FindName("SpriteImage") as System.Windows.Controls.Image;
            var pointerUp = window.FindName("BubblePointerUp") as System.Windows.Shapes.Path;
            var pointerDown = window.FindName("BubblePointerDown") as System.Windows.Shapes.Path;

            Assert.NotNull(bubble);
            Assert.NotNull(sprite);
            Assert.NotNull(pointerUp);
            Assert.NotNull(pointerDown);

            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Bottom, window.CurrentBubblePlacement);
            Assert.Equal(0, System.Windows.Controls.Grid.GetRow(sprite));
            Assert.Equal(1, System.Windows.Controls.Grid.GetRow(bubble));
            Assert.Equal(System.Windows.Visibility.Visible, pointerUp.Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, pointerDown.Visibility);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_SpeechBubble_StaysOnTopWhenSpaceSufficient()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chesthair_monkey");
            window.Top = 600;
            window.SetCustomText("Hello World");
            window.ShowSpeechBubble();

            var bubble = window.FindName("BubbleContainer") as System.Windows.Controls.Grid;
            var sprite = window.FindName("SpriteImage") as System.Windows.Controls.Image;
            var pointerUp = window.FindName("BubblePointerUp") as System.Windows.Shapes.Path;
            var pointerDown = window.FindName("BubblePointerDown") as System.Windows.Shapes.Path;

            Assert.NotNull(bubble);
            Assert.NotNull(sprite);
            Assert.NotNull(pointerUp);
            Assert.NotNull(pointerDown);

            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Top, window.CurrentBubblePlacement);
            Assert.Equal(0, System.Windows.Controls.Grid.GetRow(bubble));
            Assert.Equal(1, System.Windows.Controls.Grid.GetRow(sprite));
            Assert.Equal(System.Windows.Visibility.Visible, pointerDown.Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, pointerUp.Visibility);

            window.Close();
        });
    }

    private static string CreateTempPng(int width = 100, int height = 80)
    {
        string path = Path.Combine(Path.GetTempPath(), $"test_bubble_{Guid.NewGuid():N}.png");
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        encoder.Save(fs);
        return path;
    }

    [Fact]
    public void CharacterWindow_ShowSpeechBubble_WhenAlreadyVisible_RefreshesTimerWithoutModifyingPositionOrRecreatingContent()
    {
        RunInSta(() =>
        {
            string localImg = CreateTempPng(120, 100);
            try
            {
                var window = new CharacterWindow("chesthair_monkey");
                window.Top = 400;
                window.SetCustomText($"qq\n\n![test]({localImg})");
                window.ShowSpeechBubble();

                double topBefore = window.Top;
                double heightBefore = window.Height;

                // Repeated call while visible (e.g. left-clicking character)
                window.ShowSpeechBubble();

                Assert.Equal(topBefore, window.Top);
                Assert.Equal(heightBefore, window.Height);

                window.Close();
            }
            finally
            {
                if (File.Exists(localImg)) File.Delete(localImg);
            }
        });
    }

    [Fact]
    public void DialogueImageControl_LocalImage_LoadsSynchronouslyInConstructor()
    {
        RunInSta(() =>
        {
            string localImg = CreateTempPng(120, 100);
            try
            {
                var control = new DialogueImageControl(localImg, "alt");

                Assert.True(control.IsImageLoaded);
                var container = control.Child as System.Windows.Controls.Grid;
                Assert.NotNull(container);
                var img = container.Children.OfType<System.Windows.Controls.Image>().FirstOrDefault();
                var status = container.Children.OfType<System.Windows.Controls.TextBlock>().FirstOrDefault();
                Assert.NotNull(img);
                Assert.NotNull(status);
                Assert.Equal(System.Windows.Visibility.Visible, img.Visibility);
                Assert.Equal(System.Windows.Visibility.Collapsed, status.Visibility);
            }
            finally
            {
                if (File.Exists(localImg)) File.Delete(localImg);
            }
        });
    }

    [Fact]
    public void CharacterWindow_PlayJump_WithSpeechBubble_StartsJumpAnimation()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chesthair_monkey");
            window.Left = 500;
            window.Top = 400;
            window.SetCustomText("Jump test");
            window.ShowSpeechBubble();

            var playJumpMethod = typeof(CharacterWindow).GetMethod("PlayJump", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(playJumpMethod);

            playJumpMethod.Invoke(window, new object?[] { null });

            var isJumpingField = typeof(CharacterWindow).GetField("_isJumping", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(isJumpingField);
            Assert.True((bool)isJumpingField.GetValue(window)!);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_SpeechBubble_StaysAtBottomWhenRestingOnTaskbar()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chesthair_monkey");
            window.Left = 500;
            window.Top = 20; // Near top of screen
            window.SetCustomText("Testing bubble");
            window.ShowSpeechBubble();

            // Near top, bubble is placed at Bottom
            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Bottom, window.CurrentBubblePlacement);

            double dipScale = CharacterWindow.GetDipScale();
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            double taskbarBottom = (primary?.WorkingArea.Bottom ?? 1040) * dipScale;
            double bubbleH = window.BubbleContainer.ActualHeight > 0 ? window.BubbleContainer.ActualHeight : window.BubbleContainer.DesiredSize.Height;
            var spriteHeightField = typeof(CharacterWindow).GetField("_currentSpriteHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            spriteHeightField?.SetValue(window, 120);
            window.SpriteImage.Height = 120;
            double charH = 120;

            // Simulate window positioned so the bubble rests on the taskbar:
            // character head is at taskbarBottom - charH - bubbleH
            double restingHeadTop = taskbarBottom - charH - bubbleH;
            double deltaY = window.UpdateBubblePlacement(bubbleH, explicitCharHeadTop: restingHeadTop);

            // It should remain at Bottom (character standing on bubble resting on taskbar)
            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Bottom, window.CurrentBubblePlacement);
            Assert.Equal(0, deltaY);

            // Now simulate user dragging character body further down towards taskbar (past resting bubble)
            double draggedDownHeadTop = restingHeadTop + 30;
            deltaY = window.UpdateBubblePlacement(bubbleH, explicitCharHeadTop: draggedDownHeadTop);

            // It should flip to Top with deltaY = -bubbleH
            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Top, window.CurrentBubblePlacement);
            Assert.Equal(-bubbleH, deltaY);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_FallTo_WhenBubblePlacementIsBottom_LandsSmoothlyWithoutFlipping()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chesthair_monkey");
            window.Left = 500;
            window.Top = 50; // High up
            window.SetCustomText("Fall test");
            window.ShowSpeechBubble();

            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Bottom, window.CurrentBubblePlacement);

            var fallToMethod = typeof(CharacterWindow).GetMethod("FallTo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(fallToMethod);
            fallToMethod.Invoke(window, null);

            var isFallingField = typeof(CharacterWindow).GetField("_isFalling", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(isFallingField);
            Assert.True((bool)isFallingField.GetValue(window)!);

            // CurrentBubblePlacement must stay Bottom so the bubble lands on the taskbar cleanly
            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Bottom, window.CurrentBubblePlacement);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_HideSpeechBubble_WhenPlacementIsBottom_TriggersFall()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chesthair_monkey");
            window.Left = 500;
            window.Top = 20; // High up
            window.SetCustomText("Hide bubble test");
            window.ShowSpeechBubble();

            Assert.Equal(CharacterWindow.SpeechBubblePlacement.Bottom, window.CurrentBubblePlacement);

            window.HideSpeechBubble();

            var isFallingField = typeof(CharacterWindow).GetField("_isFalling", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(isFallingField);
            Assert.True((bool)isFallingField.GetValue(window)!);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_EnterIdleState_CanAutoPlayBounceForCapoo()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("capoo");
            var inPlace = window.InPlaceAnimationNames();
            Assert.Contains("bounce", inPlace, StringComparer.OrdinalIgnoreCase);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_EnterIdleState_CanAutoPlayBounceForArmi()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("armi");
            var inPlace = window.InPlaceAnimationNames();
            Assert.Contains("bounce", inPlace, StringComparer.OrdinalIgnoreCase);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_EnterIdleState_CanAutoPlayBounceForChestHairGoblin()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chesthair_goblin");
            var inPlace = window.InPlaceAnimationNames();
            Assert.Contains("bounce", inPlace, StringComparer.OrdinalIgnoreCase);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_Ketawan2_InitializesAndDiscoversAllAnimations()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("ketawan2");
            var allAnimations = window.AllAnimationNames();
            Assert.Contains("walkleft", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("walkright", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bounce", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("dance", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("butt", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("isolated", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("shy", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("hulahoop", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("towel", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("legcircle", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("sillydance", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("lookup", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("dash", allAnimations, StringComparer.OrdinalIgnoreCase);

            var inPlace = window.InPlaceAnimationNames();
            Assert.Contains("bounce", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("dance", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("butt", inPlace, StringComparer.OrdinalIgnoreCase);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_SkyRapper_InitializesAndDiscoversAllAnimations()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("sky_rapper");
            var allAnimations = window.AllAnimationNames();
            Assert.Contains("walkleft", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("walkright", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bounce", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("iine", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("kusao", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bro", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("smoke", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("explosion", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("money", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("beer", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("night", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("saikou", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("shirankedo", allAnimations, StringComparer.OrdinalIgnoreCase);

            var inPlace = window.InPlaceAnimationNames();
            Assert.Contains("bounce", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("iine", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("kusao", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bro", inPlace, StringComparer.OrdinalIgnoreCase);

            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_Shisa_InitializesAndDiscoversAllAnimations()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("shisa");
            var allAnimations = window.AllAnimationNames();
            Assert.Contains("walkleft", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("walkright", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("cheer", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("study", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("eat", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("drink", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("sleep", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("roar", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("ramen", allAnimations, StringComparer.OrdinalIgnoreCase);

            var inPlace = window.InPlaceAnimationNames();
            Assert.Contains("cheer", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("study", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("eat", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("drink", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("sleep", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("roar", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("ramen", inPlace, StringComparer.OrdinalIgnoreCase);

            window.Close();
        });
    }

    [Fact]
    public void BehaviorPlanner_PlanJump_GroundAnchor_LandsExactlyAtStartingGround()
    {
        int startingTop = 735;
        int characterHeight = 120;
        int minX = 0;
        int maxX = 1920;
        int effectiveLandingY = startingTop + characterHeight;

        var plan = BehaviorPlanner.PlanJump(
            new PetPoint(500, startingTop),
            characterHeight,
            minX,
            maxX,
            effectiveLandingY,
            SystemRandomSource.Shared);

        Assert.Equal(startingTop, plan.LandTarget.Y);
    }

    [Fact]
    public void CharacterWindow_RefreshVisualSurface_ExecutesWithoutException()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("hachiware");
            window.RefreshVisualSurface();
            Assert.NotNull(window.SpriteImage);
            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_ChestHairEmperor_InitializesAndDiscoversAllAnimations()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chesthair_emperor");
            var allAnimations = window.AllAnimationNames();
            Assert.Contains("walkleft", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("walkright", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bounce", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("angry", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("panic", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("stamp", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("dismiss", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("lazy", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("suspicious", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("reward", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("read", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("inspect", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("worship", allAnimations, StringComparer.OrdinalIgnoreCase);

            var inPlace = window.InPlaceAnimationNames();
            Assert.Contains("bounce", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("stamp", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("angry", inPlace, StringComparer.OrdinalIgnoreCase);

            Assert.NotNull(window.SpriteImage);
            window.Close();
        });
    }

    [Fact]
    public void CharacterWindow_NaiLong_InitializesAndDiscoversAllAnimations()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("nailong");
            var allAnimations = window.AllAnimationNames();
            Assert.Contains("walkleft", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("walkright", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bounce", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("fly", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("watermelon", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("chicken", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("sleep", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("tease", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("cry", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("snort", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("dance", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("laugh", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("drool", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("liondance", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("salute", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("pet", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bye", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("nod", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("bag", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("cny", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("hop", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("gasp", allAnimations, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("hi", allAnimations, StringComparer.OrdinalIgnoreCase);

            var inPlace = window.InPlaceAnimationNames();
            Assert.Contains("bounce", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("fly", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("watermelon", inPlace, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("snort", inPlace, StringComparer.OrdinalIgnoreCase);

            Assert.NotNull(window.SpriteImage);
            window.Close();
        });
    }
}
