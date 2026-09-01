using System.Collections.Generic;
using System.IO;
using ChiikawaDesktopPet.Core;
using Xunit;

public class ConfigTests
{
    [Fact]
    public void GetFps_NullConfig_ReturnsDefault()
    {
        Assert.Equal(40, BehaviorPlanner.GetFps(null, "hachiware", "walkleft"));
    }

    [Fact]
    public void GetFps_CharacterNotPresent_ReturnsDefault()
    {
        var config = new Dictionary<string, CharacterConfig>();
        Assert.Equal(40, BehaviorPlanner.GetFps(config, "hachiware", "walkleft"));
    }

    [Fact]
    public void GetFps_AnimationPresent_ReturnsConfiguredValue()
    {
        var config = new Dictionary<string, CharacterConfig>
        {
            ["hachiware"] = new CharacterConfig
            {
                Animations = new Dictionary<string, AnimationConfig>
                {
                    ["walkleft"] = new AnimationConfig { Fps = 24 }
                }
            }
        };
        Assert.Equal(24, BehaviorPlanner.GetFps(config, "hachiware", "walkleft"));
    }

    [Fact]
    public void ConfigLoader_Load_MissingFile_ReturnsEmptyDictionary()
    {
        var result = ConfigLoader.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + System.Guid.NewGuid() + ".json"));
        Assert.Empty(result);
    }

    [Fact]
    public void ConfigLoader_Load_ValidFile_ParsesCharacterMap()
    {
        string path = Path.Combine(Path.GetTempPath(), "chiikawadesktoppet-config-" + System.Guid.NewGuid() + ".json");
        File.WriteAllText(path, """
        {
          "hachiware": { "animations": { "walkleft": { "fps": 24 } } }
        }
        """);
        try
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(24, BehaviorPlanner.GetFps(result, "hachiware", "walkleft"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConfigLoader_Load_MalformedJson_ReturnsEmptyDictionary()
    {
        string path = Path.Combine(Path.GetTempPath(), "chiikawadesktoppet-config-" + System.Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{ not valid json");
        try
        {
            var result = ConfigLoader.Load(path);
            Assert.Empty(result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConfigLoader_Load_ShippedConfig_ContainsLaiConfig()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChiikawaDesktopPet.Wpf", "config.default.json");
        if (File.Exists(path))
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "lai", "walkleft"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "lai", "bushi"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "lai", "cheer"));
        }
    }

    [Fact]
    public void ConfigLoader_Load_ShippedConfig_ContainsCapooConfig()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChiikawaDesktopPet.Wpf", "config.default.json");
        if (File.Exists(path))
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(8, BehaviorPlanner.GetFps(result, "capoo", "walkleft"));
            Assert.Equal(15, BehaviorPlanner.GetFps(result, "capoo", "bounce"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "capoo", "eat"));
            Assert.Equal(15, BehaviorPlanner.GetFps(result, "capoo", "roar"));
        }
    }

    [Fact]
    public void ConfigLoader_Load_ShippedConfig_ContainsChestHairMonkeyConfig()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChiikawaDesktopPet.Wpf", "config.default.json");
        if (File.Exists(path))
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "chesthair_monkey", "walkleft"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "chesthair_monkey", "bounce"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "chesthair_monkey", "eat"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "chesthair_monkey", "worship"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_monkey", "keyboard"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_monkey", "chair"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_monkey", "smash"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_monkey", "error"));
        }
    }

    [Fact]
    public void ConfigLoader_Load_ShippedConfig_ContainsArmiConfig()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChiikawaDesktopPet.Wpf", "config.default.json");
        if (File.Exists(path))
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "armi", "walkleft"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "armi", "bounce"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "armi", "cheer"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "armi", "fine"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "armi", "melt"));
            Assert.Equal(8, BehaviorPlanner.GetFps(result, "armi", "sleep"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "armi", "rich"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "armi", "muscle"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "armi", "laugh"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "armi", "pompom"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "armi", "heart"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "armi", "sparkle"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "armi", "yay"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "armi", "wave"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "armi", "hug"));
        }
    }

    [Fact]
    public void ConfigLoader_Load_ShippedConfig_ContainsChestHairGoblinConfig()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChiikawaDesktopPet.Wpf", "config.default.json");
        if (File.Exists(path))
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(1.0, result["chesthair_goblin"].Scale);
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_goblin", "walkleft"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_goblin", "bounce"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_goblin", "smash"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_goblin", "eat"));
            Assert.Equal(8, BehaviorPlanner.GetFps(result, "chesthair_goblin", "sleep"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "chesthair_goblin", "dance"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "chesthair_goblin", "cheer"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_goblin", "cry"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_goblin", "roar"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "chesthair_goblin", "scream"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_goblin", "worship"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "chesthair_goblin", "swing"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "chesthair_goblin", "sit"));
        }
    }

    [Fact]
    public void ConfigLoader_Load_ShippedConfig_ContainsKetawan2Config()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChiikawaDesktopPet.Wpf", "config.default.json");
        if (File.Exists(path))
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "ketawan2", "walkleft"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "ketawan2", "walkright"));
            Assert.Equal(15, BehaviorPlanner.GetFps(result, "ketawan2", "dash"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "ketawan2", "bounce"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "ketawan2", "dance"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "ketawan2", "butt"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "ketawan2", "isolated"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "ketawan2", "shy"));
            Assert.Equal(15, BehaviorPlanner.GetFps(result, "ketawan2", "hulahoop"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "ketawan2", "towel"));
            Assert.Equal(16, BehaviorPlanner.GetFps(result, "ketawan2", "legcircle"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "ketawan2", "sillydance"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "ketawan2", "lookup"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "ketawan2", "music"));
            Assert.Equal(8, BehaviorPlanner.GetFps(result, "ketawan2", "sleep"));
        }
    }

    [Fact]
    public void ConfigLoader_Load_ShippedConfig_ContainsSkyRapperConfig()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ChiikawaDesktopPet.Wpf", "config.default.json");
        if (File.Exists(path))
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "sky_rapper", "walkleft"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "sky_rapper", "walkright"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "sky_rapper", "bounce"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "sky_rapper", "iine"));
            Assert.Equal(15, BehaviorPlanner.GetFps(result, "sky_rapper", "kusao"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "sky_rapper", "bro"));
            Assert.Equal(10, BehaviorPlanner.GetFps(result, "sky_rapper", "smoke"));
            Assert.Equal(14, BehaviorPlanner.GetFps(result, "sky_rapper", "explosion"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "sky_rapper", "money"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "sky_rapper", "beer"));
            Assert.Equal(8, BehaviorPlanner.GetFps(result, "sky_rapper", "night"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "sky_rapper", "saikou"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "sky_rapper", "shirankedo"));
        }
    }
}
