// src/YahaPet.Wpf/CharacterQuotes.cs
using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace YahaPet.Wpf;

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
        ["lai"] = "Team Taiwan！台灣加油！"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string GetDefaultQuote(string characterName) =>
        DefaultQuotes.TryGetValue(characterName, out var quote) ? quote : "嗨！";
}

