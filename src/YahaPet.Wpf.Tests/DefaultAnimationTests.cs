// src/YahaPet.Wpf.Tests/DefaultAnimationTests.cs
using System;
using System.Threading;
using Xunit;
using YahaPet.Wpf;

namespace YahaPet.Wpf.Tests;

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
}
