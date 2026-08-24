// src/YahaPet.Wpf.Tests/SoundPlayerFactoryTests.cs
using System;
using System.IO;
using YahaPet.Wpf;
using Xunit;

namespace YahaPet.Wpf.Tests;

public class SoundPlayerFactoryTests
{
    [Fact]
    public void PlayIfExists_MissingFile_DoesNotThrow()
    {
        SoundPlayerFactory.MuteAll = false;
        string missingPath = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".wav");
        var exception = Record.Exception(() => SoundPlayerFactory.PlayIfExists(missingPath));
        Assert.Null(exception);
    }

    [Fact]
    public void PlayIfExists_WhenMuteAllIsTrue_DoesNotThrow()
    {
        SoundPlayerFactory.MuteAll = true;
        string dummyPath = Path.GetTempFileName();
        try
        {
            var exception = Record.Exception(() => SoundPlayerFactory.PlayIfExists(dummyPath));
            Assert.Null(exception);
        }
        finally
        {
            SoundPlayerFactory.MuteAll = false;
            if (File.Exists(dummyPath)) File.Delete(dummyPath);
        }
    }
}
