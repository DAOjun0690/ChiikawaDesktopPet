// src/ChiikawaDesktopPet.Wpf.Tests/DefaultAnimationTests.cs
using System;
using System.IO;
using System.Threading;
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
}
