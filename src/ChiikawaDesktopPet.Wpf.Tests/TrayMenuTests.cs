// src/ChiikawaDesktopPet.Wpf.Tests/TrayMenuTests.cs
using System;
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

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
    [InlineData("chesthair_goblin", "胸毛公寓 哥布林喵喵怪")]
    [InlineData("armi", "廢貓阿米 - 左手畫的")]
    [InlineData("ketawan2", "けたわん (Ketawan2)")]
    [InlineData("sky_rapper", "Sky Rapper (天空饒舌歌手)")]
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
    [InlineData("fine", "火海喝茶")]
    [InlineData("melt", "融化成史萊姆")]
    [InlineData("rich", "撒錢暴富")]
    [InlineData("muscle", "秀二頭肌")]
    [InlineData("laugh", "仰天狂笑")]
    [InlineData("pompom", "彩球應援")]
    [InlineData("sparkle", "水汪汪大眼")]
    [InlineData("yay", "好耶舉手")]
    [InlineData("wave", "揮手掰掰")]
    [InlineData("hug", "雙貓互蹭")]
    [InlineData("sit", "乖乖坐好")]
    [InlineData("dash", "急速橫移")]
    [InlineData("butt", "開心扭屁股")]
    [InlineData("isolated", "角落畫圈自閉")]
    [InlineData("shy", "害羞雙手摀臉")]
    [InlineData("hulahoop", "瘋狂搖呼拉圈")]
    [InlineData("towel", "雙手搓毛巾")]
    [InlineData("legcircle", "躺平雙腿畫圈")]
    [InlineData("sillydance", "魔性魔幻舞步")]
    [InlineData("lookup", "抬頭看上面")]
    [InlineData("music", "戴耳機聽音樂")]
    [InlineData("iine", "雙手比讚")]
    [InlineData("kusao", "魔性大笑(草)")]
    [InlineData("bro", "BRO兄弟深情")]
    [InlineData("smoke", "抽菸一服中")]
    [InlineData("explosion", "身後大爆炸")]
    [InlineData("money", "咬鈔票搖擺")]
    [InlineData("beer", "來喝一杯")]
    [InlineData("night", "晚安星空")]
    [InlineData("saikou", "太棒了最高")]
    [InlineData("shirankedo", "雖然我也不清楚啦")]
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

    [Theory]
    [InlineData("chiikawa", 1, "Chiikawa 1")]
    [InlineData("chiikawa", 2, "Chiikawa 2")]
    [InlineData("lai", 3, "總統-賴 3")]
    [InlineData("capoo", 10, "貓貓蟲咖波 (Capoo) 10")]
    [InlineData("custom_pet", 5, "custom_pet 5")]
    public void GetCharacterInstanceDisplayName_FormatsExpectedInstanceName(string key, int index, string expectedDisplayName)
    {
        string instanceDisplayName = App.GetCharacterInstanceDisplayName(key, index);
        Assert.Equal(expectedDisplayName, instanceDisplayName);
    }

    [Fact]
    public void CharacterWindow_InstanceProperties_InitializeCorrectly()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var window = new CharacterWindow("chiikawa", 3);
            Assert.Equal("chiikawa", window.CharacterName);
            Assert.Equal(3, window.InstanceIndex);
            Assert.Equal("Chiikawa 3", window.InstanceDisplayName);
            Assert.Equal("chiikawa_3", window.InstanceId);
            window.Close();
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
