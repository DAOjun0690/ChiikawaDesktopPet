using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChiikawaDesktopPet.Core;

/// Reads config.json, whose root object IS the character map (no wrapping key),
/// e.g. { "hachiware": { "animations": { "walkleft": { "fps": 24 } } } }.
public static class ConfigLoader
{
    public static Dictionary<string, CharacterConfig> Load(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, CharacterConfig>(StringComparer.OrdinalIgnoreCase);

        try
        {
            string json = File.ReadAllText(path);
            var result = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.DictionaryStringCharacterConfig);
            return result != null
                ? new Dictionary<string, CharacterConfig>(result, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, CharacterConfig>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, CharacterConfig>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
