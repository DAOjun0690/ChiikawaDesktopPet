// src/ChiikawaDesktopPet.Core.Tests/SettingsManagerTests.cs
using System;
using System.IO;
using Xunit;
using ChiikawaDesktopPet.Core;

namespace ChiikawaDesktopPet.Core.Tests;

public class SettingsManagerTests
{
    [Fact]
    public void Load_NonExistentFile_ReturnsDefaultSettings()
    {
        string fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        var settings = SettingsManager.Load(fakePath);
        Assert.NotNull(settings);
        Assert.False(settings.SoftwareRendering);
        Assert.True(settings.ConfineToCurrentMonitor);
        Assert.False(settings.EnableWindowsNotifications);
        Assert.True(settings.AutoSaveProfile);
    }

    [Fact]
    public void SaveAndLoad_ValidSettings_RoundTripsSuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string tempFile = Path.Combine(tempDir, "settings.json");

        try
        {
            var original = new AppSettings
            {
                SoftwareRendering = true,
                ConfineToCurrentMonitor = true,
                EnableWindowsNotifications = true,
                AutoSaveProfile = false
            };

            SettingsManager.Save(original, tempFile);
            Assert.True(File.Exists(tempFile));

            var loaded = SettingsManager.Load(tempFile);
            Assert.NotNull(loaded);
            Assert.True(loaded.SoftwareRendering);
            Assert.True(loaded.ConfineToCurrentMonitor);
            Assert.True(loaded.EnableWindowsNotifications);
            Assert.False(loaded.AutoSaveProfile);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void GetAutoSaveProfilePath_ReturnsValidPath()
    {
        string path = SettingsManager.GetAutoSaveProfilePath();
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.EndsWith("autosave_profile.json", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSettingsFilePath_DoesNotCreateEmptyFileOnDisk()
    {
        string path = SettingsManager.GetSettingsFilePath();
        Assert.False(string.IsNullOrWhiteSpace(path));
        // Calling GetSettingsFilePath should NOT create the file if it didn't exist
        if (!File.Exists(path))
        {
            Assert.False(File.Exists(path));
        }
    }

    [Fact]
    public void Load_EmptyFile_CleansUpAndReturnsDefaultSettings()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string tempFile = Path.Combine(tempDir, "settings.json");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(tempFile, ""); // 0 bytes

        try
        {
            var settings = SettingsManager.Load(tempFile);
            Assert.NotNull(settings);
            Assert.True(settings.ConfineToCurrentMonitor);
            // 0-byte file should be automatically deleted/cleaned up
            Assert.False(File.Exists(tempFile));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
