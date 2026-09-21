// src/ChiikawaDesktopPet.Wpf.Tests/BongoSkinManagerTests.cs
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class BongoSkinManagerTests
{
    [Fact]
    public void DiscoverSkins_FindsChiikawaHachiwareUsagiAndMonkey()
    {
        var manager = new BongoSkinManager();
        var skins = manager.DiscoverSkins();

        Assert.NotNull(skins);
        Assert.True(skins.Count >= 4, $"Expected at least 4 skins, found {skins.Count}");

        var keys = skins.Select(s => s.Key.ToLowerInvariant()).ToList();
        Assert.Contains("chiikawa", keys);
        Assert.Contains("hachiware", keys);
        Assert.Contains("usagi", keys);
        Assert.Contains("chesthair_monkey", keys);
    }

    [Fact]
    public void LoadSkin_Chiikawa_LoadsAllLayers()
    {
        var manager = new BongoSkinManager();
        var skin = manager.LoadSkin("chiikawa");

        Assert.NotNull(skin);
        Assert.Equal("chiikawa", skin.Info.Key);
        Assert.NotNull(skin.Body);
        Assert.NotNull(skin.LeftPawUp);
        Assert.NotNull(skin.LeftPawDown);
        Assert.NotNull(skin.LeftPawDownRight);
        Assert.NotNull(skin.RightPawUp);
        Assert.NotNull(skin.RightPawDown);
        Assert.NotNull(skin.FaceNormal);
        Assert.NotNull(skin.FaceFocused);
        Assert.NotNull(skin.FaceOverdrive);
        Assert.NotNull(skin.OverdriveEffect);
    }

    [Theory]
    [InlineData("hachiware")]
    [InlineData("usagi")]
    [InlineData("chesthair_monkey")]
    public void LoadSkin_OtherSkins_LoadsAllLayers(string key)
    {
        var manager = new BongoSkinManager();
        var skin = manager.LoadSkin(key);

        Assert.NotNull(skin);
        Assert.Equal(key, skin.Info.Key);
        Assert.NotNull(skin.Body);
        Assert.NotNull(skin.LeftPawUp);
        Assert.NotNull(skin.LeftPawDown);
        Assert.NotNull(skin.LeftPawDownRight);
        Assert.NotNull(skin.RightPawUp);
        Assert.NotNull(skin.RightPawDown);
        Assert.NotNull(skin.FaceNormal);
    }
}

