// src/ChiikawaDesktopPet.Wpf/CharacterQuotes.cs
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace ChiikawaDesktopPet.Wpf;

public static class CharacterQuotes
{
    private static readonly FrozenDictionary<string, string> DefaultQuotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["chiikawa"] = "わ…！（哇…！）",
        ["hachiware"] = "なんとかなれーッ！（船到橋頭自然直！）",
        ["usagi"] = "ウラ！ヤハ！プルャ！",
        ["momonga"] = "褒めろッ！叱るな！（誇獎我！不准罵我！）",
        ["jokebear"] = "（微笑凝視）",
        ["loverabbit"] = "啾～❤️ 最喜歡你了！",
        ["lai"] = "Team Taiwan！台灣加油！",
        ["poro"] = "咕嚕嚕～❤️（吐舌頭嚼普羅點心）",
        ["pochita"] = "汪！汪！（鏈鋸引擎轟鳴運轉）",
        ["capoo"] = "肉肉！肉肉！（興奮扭動討肉吃）",
        ["chesthair_monkey"] = "吱吱！吱吱吱！（翻譯蒟蒻：救命／想吃香蕉）",
        ["chesthair_goblin"] = "嘎嘎！咕嚕嚕！（哥布林喵喵怪正在警戒中）",
        ["armi"] = "爛成一坨了...不想努力了 (躺平)",
        ["ketawan2"] = "汪！汪汪～！（興奮地搖著尾巴晃動）",
        ["sky_rapper"] = "BRO...（從天空中比出大拇指凝視著你）",
        ["shisa"] = "うれシーサー！（好開心獅薩！）"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string GetDefaultQuote(string characterName) =>
        DefaultQuotes.TryGetValue(characterName, out var quote) ? quote : "嗨！";
}

