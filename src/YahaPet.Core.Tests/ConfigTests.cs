using System.Collections.Generic;
using System.IO;
using YahaPet.Core;
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
        string path = Path.Combine(Path.GetTempPath(), "yahapet-config-" + System.Guid.NewGuid() + ".json");
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
        string path = Path.Combine(Path.GetTempPath(), "yahapet-config-" + System.Guid.NewGuid() + ".json");
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
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "YahaPet.Wpf", "config.default.json");
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
        string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "YahaPet.Wpf", "config.default.json");
        if (File.Exists(path))
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(8, BehaviorPlanner.GetFps(result, "capoo", "walkleft"));
            Assert.Equal(15, BehaviorPlanner.GetFps(result, "capoo", "bounce"));
            Assert.Equal(12, BehaviorPlanner.GetFps(result, "capoo", "eat"));
            Assert.Equal(15, BehaviorPlanner.GetFps(result, "capoo", "roar"));
        }
    }
}
