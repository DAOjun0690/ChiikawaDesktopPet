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
    [InlineData("pochita", "波奇塔 (Pochita)")]
    [InlineData("capoo", "貓貓蟲咖波 (Capoo)")]
    [InlineData("chesthair_monkey", "胸毛公寓 猴子朋友")]
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
    [InlineData("chainsaw", "鏈鋸狂飆")]
    [InlineData("spin", "旋轉狂舞")]
    [InlineData("bark", "汪汪叫")]
    [InlineData("roar", "張大嘴怒吼")]
    [InlineData("thunder", "小雞觸電")]
    [InlineData("squeeze", "胖到溢出來")]
    [InlineData("worship", "膜拜香蕉")]
    [InlineData("keyboard", "狂敲鍵盤")]
    [InlineData("chair", "辦公椅狂飆")]
    [InlineData("smash", "鐵鎚砸手機")]
    [InlineData("error", "筆電報錯")]
    [InlineData("toilet", "馬桶滑手機")]
    [InlineData("swing", "藤蔓擺盪")]
    [InlineData("flat", "趴平融化")]
    [InlineData("scream", "驚嚇尖叫")]
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
