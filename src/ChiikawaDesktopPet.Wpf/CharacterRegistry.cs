// src/ChiikawaDesktopPet.Wpf/CharacterRegistry.cs
using System;
using System.Collections.Frozen;
using System.Linq;

namespace ChiikawaDesktopPet.Wpf;

/// <summary>
/// Single source of truth for known characters. Adding a character means adding one entry here
/// (plus its assets/config.json entry) - display names, auto-spawn eligibility, and the
/// cross-process interaction type IDs are all derived from this list.
/// </summary>
internal static class CharacterRegistry
{
    public static readonly (string Key, string DisplayName, bool AutoSpawn)[] All =
    [
        ("hachiware", "Hachiware", true),
        ("chiikawa", "Chiikawa", true),
        ("usagi", "Usagi", true),
        ("momonga", "Momonga", true),
        ("jokebear", "JokeBear", true),
        ("loverabbit", "LOVE RABBIT", true),
        ("lai", "總統-賴", false),
        ("poro", "普羅 (Poro)", true),
        ("pochita", "波奇塔 (Pochita)", true),
        ("capoo", "貓貓蟲咖波 (Capoo)", true),
        ("chesthair_monkey", "胸毛公寓 猴子朋友", true),
        ("chesthair_goblin", "胸毛公寓 哥布林喵喵怪", true),
        ("armi", "廢貓阿米 - 左手畫的", true),
        ("ketawan2", "けたわん (Ketawan2)", true),
        ("sky_rapper", "Sky Rapper (天空饒舌歌手)", true)
    ];

    public static readonly string[] AutoSpawnCandidates =
        All.Where(c => c.AutoSpawn).Select(c => c.Key).ToArray();

    private static readonly FrozenDictionary<string, string> DisplayNamesByKey =
        All.ToDictionary(c => c.Key, c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
           .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // Type IDs are derived from array position (1-based); stable as long as this build's
    // array order matches across the processes exchanging them, which it always does.
    private static readonly FrozenDictionary<string, int> TypeIdsByKey =
        All.Select((c, i) => (c.Key, TypeId: i + 1))
           .ToDictionary(x => x.Key, x => x.TypeId, StringComparer.OrdinalIgnoreCase)
           .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<int, string> KeysByTypeId =
        All.Select((c, i) => (TypeId: i + 1, c.Key))
           .ToDictionary(x => x.TypeId, x => x.Key)
           .ToFrozenDictionary();

    public static string GetDisplayName(string characterKey) =>
        DisplayNamesByKey.TryGetValue(characterKey, out var name) ? name : characterKey;

    public static int GetTypeId(string characterKey) =>
        TypeIdsByKey.TryGetValue(characterKey, out var typeId) ? typeId : 0;

    public static string? GetKeyFromTypeId(int typeId) =>
        KeysByTypeId.TryGetValue(typeId, out var key) ? key : null;
}
