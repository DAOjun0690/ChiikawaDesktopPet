// src/ChiikawaDesktopPet.Wpf.Tests/BackgroundSleepTests.cs
using System;
using System.IO;
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class BackgroundSleepTests
{
    [Fact]
    public void InteractionCoordinator_PauseAndResume_TogglesScanningState()
    {
        var coordinator = InteractionCoordinator.Instance;
        Assert.True(coordinator.IsScanningActive);

        coordinator.PauseScanning();
        Assert.False(coordinator.IsScanningActive);

        coordinator.ResumeScanning();
        Assert.True(coordinator.IsScanningActive);
    }

    [Fact]
    public void ClickThroughManager_PauseAndResume_UpdatesStateCorrectly()
    {
        var manager = ClickThroughManager.Instance;
        manager.PauseHook();
        Assert.False(manager.IsHookActive);

        manager.ResumeHook();
        // Since no windows are registered in test process, hook remains uninstalled
        Assert.False(manager.IsHookActive);
    }

    [Fact]
    public void CrashLogger_Log_WritesExceptionDetailsWithoutThrowing()
    {
        string logPath = CrashLogger.GetLogFilePath();
        var testEx = new InvalidOperationException("Test exception for unit testing", new ArgumentNullException("paramName"));

        CrashLogger.Log(testEx, "BackgroundSleepTests.UnitTest");

        Assert.True(File.Exists(logPath));
        string content = File.ReadAllText(logPath);
        Assert.Contains("Test exception for unit testing", content);
        Assert.Contains("BackgroundSleepTests.UnitTest", content);
        Assert.Contains("paramName", content);
    }
}
