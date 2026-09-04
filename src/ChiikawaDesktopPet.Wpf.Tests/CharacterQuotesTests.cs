// src/ChiikawaDesktopPet.Wpf.Tests/CharacterQuotesTests.cs
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class CharacterQuotesTests
{
    [Theory]
    [InlineData("chiikawa", "わ…！（哇…！）")]
    [InlineData("hachiware", "なんとかなれーッ！（船到橋頭自然直！）")]
    [InlineData("usagi", "ウラ！ヤハ！プルャ！")]
    [InlineData("momonga", "褒めろッ！叱るな！（誇獎我！不准罵我！）")]
    [InlineData("jokebear", "（微笑凝視）")]
    [InlineData("loverabbit", "啾～❤️ 最喜歡你了！")]
    [InlineData("lai", "Team Taiwan！台灣加油！")]
    [InlineData("poro", "咕嚕嚕～❤️（吐舌頭嚼普羅點心）")]
    [InlineData("pochita", "汪！汪！（鏈鋸引擎轟鳴運轉）")]
    [InlineData("capoo", "肉肉！肉肉！（興奮扭動討肉吃）")]
    [InlineData("chesthair_monkey", "吱吱！吱吱吱！（翻譯蒟蒻：救命／想吃香蕉）")]
    [InlineData("chesthair_goblin", "嘎嘎！咕嚕嚕！（哥布林喵喵怪正在警戒中）")]
    [InlineData("armi", "爛成一坨了...不想努力了 (躺平)")]
    [InlineData("ketawan2", "汪！汪汪～！（興奮地搖著尾巴晃動）")]
    [InlineData("sky_rapper", "BRO...（從天空中比出大拇指凝視著你）")]
    [InlineData("shisa", "うれシーサー！（好開心獅薩！）")]
    [InlineData("linedog", "汪汪！今天也要開開心心！（搖尾巴）")]
    public void GetDefaultQuote_KnownCharacters_ReturnsExpectedQuote(string characterName, string expectedQuote)
    {
        string quote = CharacterQuotes.GetDefaultQuote(characterName);
        Assert.Equal(expectedQuote, quote);
    }

    [Theory]
    [InlineData("CHIIKAWA")]
    [InlineData("Hachiware")]
    [InlineData("USAGI")]
    public void GetDefaultQuote_CaseInsensitive_ReturnsQuote(string characterName)
    {
        string quote = CharacterQuotes.GetDefaultQuote(characterName);
        Assert.False(string.IsNullOrEmpty(quote));
        Assert.NotEqual("嗨！", quote);
    }

    [Fact]
    public void GetDefaultQuote_UnknownCharacter_ReturnsFallback()
    {
        string quote = CharacterQuotes.GetDefaultQuote("unknown_character_xyz");
        Assert.Equal("嗨！", quote);
    }
}

