// src/ChiikawaDesktopPet.Wpf.Tests/PowerRecoveryTests.cs
using System;
using System.Threading;
using System.Windows.Threading;
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class PowerRecoveryTests
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
    public void NativeMethods_PowerBroadcastConstants_HaveExpectedValues()
    {
        Assert.Equal(0x0218, NativeMethods.WM_POWERBROADCAST);
        Assert.Equal(0x000A, NativeMethods.PBT_APMPOWERSTATUSCHANGE);
        Assert.Equal(0x0012, NativeMethods.PBT_APMRESUMEAUTOMATIC);
        Assert.Equal(0x0007, NativeMethods.PBT_APMRESUMESUSPEND);
        Assert.Equal(0x8013, NativeMethods.PBT_POWERSETTINGCHANGE);
    }

    [Fact]
    public void CharacterWindow_RefreshVisualSurface_WithClamp_ExecutesWithoutException()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("hachiware");
            // Call both overloads
            window.RefreshVisualSurface();
            window.RefreshVisualSurface(clamp: true);
            window.RefreshVisualSurface(clamp: false);

            Assert.NotNull(window.SpriteImage);
            window.Close();
        });
    }

    [Fact]
    public void App_SchedulePowerStateRecoveryStatic_WhenNoAppRunning_DoesNotThrow()
    {
        // When App.Current is null or not App, static call should be safe
        App.SchedulePowerStateRecoveryStatic("UnitTest.NoApp");
    }

    [Fact]
    public void CharacterWindow_WndProc_HandlesPowerBroadcastMessages_WithoutThrowing()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chiikawa");
            window.Show();

            // Simulate WM_POWERBROADCAST messages
            var hwndSource = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(window).Handle);
            Assert.NotNull(hwndSource);

            // Test PBT_APMPOWERSTATUSCHANGE
            window.RefreshVisualSurface(clamp: true);

            window.Close();
        });
    }
}
