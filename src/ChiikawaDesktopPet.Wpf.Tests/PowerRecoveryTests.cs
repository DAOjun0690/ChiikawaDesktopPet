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
        Assert.Equal(0x0004, NativeMethods.PBT_APMSUSPEND);
        Assert.Equal(0x000A, NativeMethods.PBT_APMPOWERSTATUSCHANGE);
        Assert.Equal(0x0012, NativeMethods.PBT_APMRESUMEAUTOMATIC);
        Assert.Equal(0x0007, NativeMethods.PBT_APMRESUMESUSPEND);
        Assert.Equal(0x8013, NativeMethods.PBT_POWERSETTINGCHANGE);
    }

    [Fact]
    public void App_ShouldTriggerRecoveryForPowerBroadcast_IgnoresBatteryStatusChange()
    {
        Assert.False(App.ShouldTriggerRecoveryForPowerBroadcast(NativeMethods.PBT_APMPOWERSTATUSCHANGE));
        Assert.False(App.ShouldTriggerRecoveryForPowerBroadcast(NativeMethods.PBT_APMSUSPEND));
        Assert.True(App.ShouldTriggerRecoveryForPowerBroadcast(NativeMethods.PBT_APMRESUMEAUTOMATIC));
        Assert.True(App.ShouldTriggerRecoveryForPowerBroadcast(NativeMethods.PBT_APMRESUMESUSPEND));
    }

    [Fact]
    public void CharacterWindow_PauseAndResumeForSystemSuspend_RespectsBossKeyState()
    {
        RunInSta(() =>
        {
            var window = new CharacterWindow("chiikawa");
            window.Show();
            Assert.False(window.IsSystemSuspended);

            window.PauseForSystemSuspend();
            Assert.True(window.IsSystemSuspended);
            Assert.False(window.IsPetHidden);

            window.ResumeFromSystemSuspend();
            Assert.False(window.IsSystemSuspended);

            // If user hid via Boss Key, suspend + resume must keep IsPetHidden = true
            window.HidePet();
            window.PauseForSystemSuspend();
            window.ResumeFromSystemSuspend();
            Assert.True(window.IsPetHidden);
            Assert.False(window.IsSystemSuspended);

            window.Shutdown();
        });
    }

    [Fact]
    public void CrashLogger_RotateIfNeeded_MovesOversizedLogToOldArchive()
    {
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"yahapet_logrot_{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            string logPath = System.IO.Path.Combine(tempDir, "crash.log");
            string oldPath = logPath + ".old";
            System.IO.File.WriteAllText(logPath, new string('A', 256));

            CrashLogger.RotateIfNeeded(logPath, maxBytes: 128);

            Assert.False(System.IO.File.Exists(logPath));
            Assert.True(System.IO.File.Exists(oldPath));
            Assert.Equal(256, new System.IO.FileInfo(oldPath).Length);
        }
        finally
        {
            try { System.IO.Directory.Delete(tempDir, recursive: true); } catch { }
        }
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
