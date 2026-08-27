// src/YahaPet.Core/ProfileManager.cs
using System;
using System.IO;
using System.Text.Json;

namespace YahaPet.Core;

public static class ProfileManager
{
    public static string Serialize(PetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, ConfigJsonContext.Default.PetProfile);
    }

    public static PetProfile? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize(json, ConfigJsonContext.Default.PetProfile);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void SaveToFile(string path, PetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string json = Serialize(profile);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, json);
    }

    public static PetProfile? LoadFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path);
            return Deserialize(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
