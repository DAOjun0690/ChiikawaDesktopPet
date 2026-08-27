// src/YahaPet.Core.Tests/ProfileManagerTests.cs
using System;
using System.IO;
using Xunit;
using YahaPet.Core;

namespace YahaPet.Core.Tests;

public class ProfileManagerTests
{
    [Fact]
    public void SerializeAndDeserialize_ValidProfile_RoundTripsSuccessfully()
    {
        var original = new PetProfile
        {
            Version = 1,
            Characters =
            [
                new CharacterProfileItem
                {
                    CharacterName = "chiikawa",
                    DialogueText = "わ…！",
                    DialogueAlignment = "Center",
                    DialogueFontSize = 14.5,
                    AlwaysShowBubble = true,
                    ScaleRatio = 1.25,
                    DefaultAnimation = "dance",
                    RandomAnimationsEnabled = false,
                    JumpEnabled = true
                },
                new CharacterProfileItem
                {
                    CharacterName = "usagi",
                    DialogueText = "ウラ！ヤハ！",
                    DialogueAlignment = "Left",
                    DialogueFontSize = 13.0,
                    AlwaysShowBubble = false,
                    ScaleRatio = 0.8,
                    DefaultAnimation = null,
                    RandomAnimationsEnabled = true,
                    JumpEnabled = false
                }
            ]
        };

        string json = ProfileManager.Serialize(original);
        Assert.NotNull(json);
        Assert.Contains("chiikawa", json);
        Assert.Contains("usagi", json);

        var restored = ProfileManager.Deserialize(json);
        Assert.NotNull(restored);
        Assert.Equal(1, restored.Version);
        Assert.Equal(2, restored.Characters.Count);

        var c1 = restored.Characters[0];
        Assert.Equal("chiikawa", c1.CharacterName);
        Assert.Equal("わ…！", c1.DialogueText);
        Assert.Equal("Center", c1.DialogueAlignment);
        Assert.Equal(14.5, c1.DialogueFontSize);
        Assert.True(c1.AlwaysShowBubble);
        Assert.Equal(1.25, c1.ScaleRatio);
        Assert.Equal("dance", c1.DefaultAnimation);
        Assert.False(c1.RandomAnimationsEnabled);
        Assert.True(c1.JumpEnabled);

        var c2 = restored.Characters[1];
        Assert.Equal("usagi", c2.CharacterName);
        Assert.Equal("ウラ！ヤハ！", c2.DialogueText);
        Assert.Equal("Left", c2.DialogueAlignment);
        Assert.Equal(13.0, c2.DialogueFontSize);
        Assert.False(c2.AlwaysShowBubble);
        Assert.Equal(0.8, c2.ScaleRatio);
        Assert.Null(c2.DefaultAnimation);
        Assert.True(c2.RandomAnimationsEnabled);
        Assert.False(c2.JumpEnabled);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsNull()
    {
        string invalidJson = "{ this is not valid json }";
        var result = ProfileManager.Deserialize(invalidJson);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_NullOrWhitespace_ReturnsNull(string? input)
    {
        var result = ProfileManager.Deserialize(input!);
        Assert.Null(result);
    }

    [Fact]
    public void SaveAndLoadFromFile_RoundTripsCorrectly()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"yahapet_test_{Guid.NewGuid():N}.json");
        try
        {
            var profile = new PetProfile
            {
                Version = 1,
                Characters =
                [
                    new CharacterProfileItem
                    {
                        CharacterName = "capoo",
                        DialogueText = "肉肉！",
                        ScaleRatio = 1.5
                    }
                ]
            };

            ProfileManager.SaveToFile(tempFile, profile);
            Assert.True(File.Exists(tempFile));

            var loaded = ProfileManager.LoadFromFile(tempFile);
            Assert.NotNull(loaded);
            Assert.Single(loaded.Characters);
            Assert.Equal("capoo", loaded.Characters[0].CharacterName);
            Assert.Equal("肉肉！", loaded.Characters[0].DialogueText);
            Assert.Equal(1.5, loaded.Characters[0].ScaleRatio);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void LoadFromFile_NonExistentFile_ReturnsNull()
    {
        string nonExistent = Path.Combine(Path.GetTempPath(), $"non_existent_{Guid.NewGuid():N}.json");
        var result = ProfileManager.LoadFromFile(nonExistent);
        Assert.Null(result);
    }
}
