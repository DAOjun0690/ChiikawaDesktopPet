using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using ChiikawaDesktopPet.Core;

namespace ChiikawaDesktopPet.Wpf;

public class BongoSkinInfo
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Author { get; init; } = "Community";
    public string Description { get; init; } = string.Empty;
    public string DirectoryPath { get; init; } = string.Empty;
}

public class BongoSkin
{
    public BongoSkinInfo Info { get; init; } = new();

    public BitmapSource? Body { get; set; }
    public BitmapSource? LeftPawUp { get; set; }
    public BitmapSource? LeftPawDown { get; set; }
    public BitmapSource? LeftPawDownRight { get; set; }
    public BitmapSource? RightPawUp { get; set; }
    public BitmapSource? RightPawDown { get; set; }
    public BitmapSource? FaceNormal { get; set; }
    public BitmapSource? FaceFocused { get; set; }
    public BitmapSource? FaceOverdrive { get; set; }
    public BitmapSource? OverdriveEffect { get; set; }
}

public class BongoSkinManager
{
    private readonly string _bongoRootDir;
    private readonly Dictionary<string, BongoSkin> _loadedSkins = new(StringComparer.OrdinalIgnoreCase);

    public BongoSkinManager(string? baseDir = null)
    {
        baseDir ??= AppContext.BaseDirectory;
        string direct = Path.Combine(baseDir, "assets", "bongo");
        if (Directory.Exists(direct))
        {
            _bongoRootDir = direct;
        }
        else
        {
            string current = baseDir;
            while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "LICENSE.md")))
            {
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            string candidate = Path.Combine(current, "assets", "bongo");
            _bongoRootDir = Directory.Exists(candidate) ? candidate : direct;
        }
    }

    public IReadOnlyList<BongoSkinInfo> DiscoverSkins()
    {
        var list = new List<BongoSkinInfo>();
        if (!Directory.Exists(_bongoRootDir))
        {
            return list;
        }

        foreach (var dir in Directory.EnumerateDirectories(_bongoRootDir))
        {
            string key = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(key)) continue;

            string manifestPath = Path.Combine(dir, "manifest.json");
            BongoSkinManifest? manifest = null;
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    manifest = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.BongoSkinManifest);
                }
                catch
                {
                    // Fallback to defaults on parse error
                }
            }

            string displayName = manifest?.DisplayName ?? FormatDefaultDisplayName(key);
            string author = manifest?.Author ?? "Official";
            string description = manifest?.Description ?? string.Empty;

            list.Add(new BongoSkinInfo
            {
                Key = key,
                DisplayName = displayName,
                Author = author,
                Description = description,
                DirectoryPath = dir
            });
        }

        return list;
    }

    public BongoSkin LoadSkin(string key)
    {
        if (_loadedSkins.TryGetValue(key, out var cached))
        {
            return cached;
        }

        string skinDir = Path.Combine(_bongoRootDir, key);
        if (!Directory.Exists(skinDir))
        {
            // Fallback to first available skin if requested key doesn't exist
            var available = DiscoverSkins();
            if (available.Count > 0)
            {
                skinDir = available[0].DirectoryPath;
                key = available[0].Key;
            }
        }

        string manifestPath = Path.Combine(skinDir, "manifest.json");
        BongoSkinManifest manifest = new() { Key = key };
        if (File.Exists(manifestPath))
        {
            try
            {
                string json = File.ReadAllText(manifestPath);
                var parsed = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.BongoSkinManifest);
                if (parsed != null) manifest = parsed;
            }
            catch { }
        }

        var info = new BongoSkinInfo
        {
            Key = key,
            DisplayName = manifest.DisplayName ?? FormatDefaultDisplayName(key),
            Author = manifest.Author ?? "Official",
            Description = manifest.Description ?? string.Empty,
            DirectoryPath = skinDir
        };

        var skin = new BongoSkin
        {
            Info = info,
            Body = LoadImage(Path.Combine(skinDir, manifest.BodyFile)),
            LeftPawUp = LoadImage(Path.Combine(skinDir, manifest.LeftUpFile)),
            LeftPawDown = LoadImage(Path.Combine(skinDir, manifest.LeftDownFile)),
            LeftPawDownRight = manifest.LeftDownRightFile != null ? LoadImage(Path.Combine(skinDir, manifest.LeftDownRightFile)) : null,
            RightPawUp = LoadImage(Path.Combine(skinDir, manifest.RightUpFile)),
            RightPawDown = LoadImage(Path.Combine(skinDir, manifest.RightDownFile)),
            FaceNormal = LoadImage(Path.Combine(skinDir, manifest.FaceNormalFile)),
            FaceFocused = manifest.FaceFocusedFile != null ? LoadImage(Path.Combine(skinDir, manifest.FaceFocusedFile)) : null,
            FaceOverdrive = manifest.FaceOverdriveFile != null ? LoadImage(Path.Combine(skinDir, manifest.FaceOverdriveFile)) : null,
            OverdriveEffect = manifest.OverdriveEffectFile != null ? LoadImage(Path.Combine(skinDir, manifest.OverdriveEffectFile)) : null
        };

        _loadedSkins[key] = skin;
        return skin;
    }

    private static BitmapSource? LoadImage(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatDefaultDisplayName(string key) => key.ToLowerInvariant() switch
    {
        "chiikawa" => "Chiikawa (吉伊卡哇)",
        "hachiware" => "Hachiware (小八貓)",
        "usagi" => "Usagi (兔兔烏薩奇)",
        "chesthair_monkey" => "胸毛公寓 猴子朋友",
        _ => char.ToUpperInvariant(key[0]) + key[1..]
    };
}
