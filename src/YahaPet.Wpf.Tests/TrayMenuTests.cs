// src/YahaPet.Wpf.Tests/TrayMenuTests.cs
using Xunit;
using YahaPet.Wpf;

namespace YahaPet.Wpf.Tests;

public class TrayMenuTests
{
    [Theory]
    [InlineData("chiikawa", "Chiikawa")]
    [InlineData("hachiware", "Hachiware")]
    [InlineData("usagi", "Usagi")]
    [InlineData("momonga", "Momonga")]
    [InlineData("jokebear", "JokeBear")]
    [InlineData("loverabbit", "LOVE RABBIT")]
    [InlineData("lai", "總統-賴")]
    [InlineData("poro", "普羅 (Poro)")]
    public void GetCharacterDisplayName_KnownCharacter_ReturnsExpectedDisplayName(string key, string expectedDisplayName)
    {
        string displayName = App.GetCharacterDisplayName(key);
        Assert.Equal(expectedDisplayName, displayName);
    }

    [Fact]
    public void GetCharacterDisplayName_UnknownCharacter_ReturnsKeyItself()
    {
        string displayName = App.GetCharacterDisplayName("custom_pet");
        Assert.Equal("custom_pet", displayName);
    }

    [Theory]
    [InlineData("walkleft", "向左走")]
    [InlineData("walkright", "向右走")]
    [InlineData("bounce", "原地彈跳")]
    [InlineData("dance", "狂歡跳舞")]
    [InlineData("eat", "吃拉麵")]
    [InlineData("cheer", "拍手歡呼")]
    [InlineData("heart", "發送愛心")]
    [InlineData("party", "派對狂歡")]
    public void GetAnimationDisplayName_KnownAnimation_ReturnsExpectedDisplayName(string key, string expectedDisplayName)
    {
        string displayName = App.GetAnimationDisplayName(key);
        Assert.Equal(expectedDisplayName, displayName);
    }

    [Fact]
    public void GetAnimationDisplayName_UnknownAnimation_ReturnsKeyItself()
    {
        string displayName = App.GetAnimationDisplayName("fly_away");
        Assert.Equal("fly_away", displayName);
    }
}
