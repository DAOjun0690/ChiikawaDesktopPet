// src/ChiikawaDesktopPet.Wpf/CharacterAssetPackage.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Media.Imaging;

namespace ChiikawaDesktopPet.Wpf;

public abstract class CharacterAssetPackage : IDisposable
{
    public string CharacterName { get; }

    protected CharacterAssetPackage(string characterName)
    {
        CharacterName = characterName;
    }

    public abstract IReadOnlyList<string> DiscoverAnimationNames(IReadOnlySet<string> excluded);
    public abstract bool HasAnimation(string animationName);
    public abstract bool HasSprite(string spriteName);
    public abstract Dictionary<string, BitmapSource> LoadStaticSprites(int maxWidth, int maxHeight);
    public abstract List<BitmapSource> LoadAnimationFrames(string animationName, int maxWidth, int maxHeight);

    public bool HasDirectionalCapability(string leftName, string rightName)
    {
        bool hasEitherFolder = HasAnimation(leftName) || HasAnimation(rightName);
        bool hasBothSprites = HasSprite(leftName) && HasSprite(rightName);
        return hasEitherFolder || hasBothSprites;
    }

    public static CharacterAssetPackage Open(string characterName, string? baseDir = null)
    {
        baseDir ??= AppContext.BaseDirectory;

        // 1. Priority: loose directory for easy local editing/debugging
        string looseDir = Path.Combine(baseDir, "assets", characterName);
        if (Directory.Exists(looseDir))
        {
            return new DirectoryCharacterAssetPackage(characterName, looseDir);
        }

        // 2. Priority: assets/{character}.zip
        string zipFile = Path.Combine(baseDir, "assets", $"{characterName}.zip");
        if (File.Exists(zipFile))
        {
            return new ZipCharacterAssetPackage(characterName, zipFile);
        }

        // 3. Fallback: assets/packs/{character}.zip (during dev or alternate layout)
        string packsZipFile = Path.Combine(baseDir, "assets", "packs", $"{characterName}.zip");
        if (File.Exists(packsZipFile))
        {
            return new ZipCharacterAssetPackage(characterName, packsZipFile);
        }

        // 4. Default: directory package even if directory doesn't exist yet
        return new DirectoryCharacterAssetPackage(characterName, looseDir);
    }

    public virtual void Dispose() { }
}

public sealed class DirectoryCharacterAssetPackage : CharacterAssetPackage
{
    private readonly string _root;

    public DirectoryCharacterAssetPackage(string characterName, string root) : base(characterName)
    {
        _root = root;
    }

    public override IReadOnlyList<string> DiscoverAnimationNames(IReadOnlySet<string> excluded)
    {
        string animationsDir = Path.Combine(_root, "animations");
        if (!Directory.Exists(animationsDir)) return [];

        var list = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(animationsDir))
        {
            string name = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(name) && !excluded.Contains(name))
            {
                list.Add(name);
            }
        }
        return list;
    }

    public override bool HasAnimation(string animationName)
    {
        return Directory.Exists(Path.Combine(_root, "animations", animationName));
    }

    public override bool HasSprite(string spriteName)
    {
        return File.Exists(Path.Combine(_root, "sprites", $"{spriteName}.png"));
    }

    public override Dictionary<string, BitmapSource> LoadStaticSprites(int maxWidth, int maxHeight)
    {
        var sprites = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        string spritesDir = Path.Combine(_root, "sprites");
        if (!Directory.Exists(spritesDir)) return sprites;

        foreach (var file in Directory.EnumerateFiles(spritesDir, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            sprites[name] = SpriteLoader.LoadSingle(file, maxWidth, maxHeight);
        }
        return sprites;
    }

    public override List<BitmapSource> LoadAnimationFrames(string animationName, int maxWidth, int maxHeight)
    {
        string folder = Path.Combine(_root, "animations", animationName);
        return SpriteLoader.LoadFrames(folder, maxWidth, maxHeight);
    }
}

public sealed class ZipCharacterAssetPackage : CharacterAssetPackage
{
    private readonly FileStream _fileStream;
    private readonly ZipArchive _zipArchive;
    private readonly Dictionary<string, ZipArchiveEntry> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ZipArchiveEntry>> _animations = new(StringComparer.OrdinalIgnoreCase);

    public ZipCharacterAssetPackage(string characterName, string zipFilePath) : base(characterName)
    {
        _fileStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _zipArchive = new ZipArchive(_fileStream, ZipArchiveMode.Read, leaveOpen: false);

        IndexArchive();
    }

    private void IndexArchive()
    {
        foreach (var entry in _zipArchive.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');

            // Match sprites/{name}.png
            if (path.StartsWith("sprites/", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                _sprites[name] = entry;
                continue;
            }

            // Match animations/{animName}/{frame}.png
            if (path.StartsWith("animations/", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    string animName = parts[1];
                    if (!_animations.TryGetValue(animName, out var frameList))
                    {
                        frameList = [];
                        _animations[animName] = frameList;
                    }
                    frameList.Add(entry);
                }
            }
        }

        // Sort all animation frame entries by leading number
        foreach (var list in _animations.Values)
        {
            list.Sort(static (a, b) =>
                SpriteLoader.GetLeadingNumber(a.Name).CompareTo(SpriteLoader.GetLeadingNumber(b.Name)));
        }
    }

    public override IReadOnlyList<string> DiscoverAnimationNames(IReadOnlySet<string> excluded)
    {
        return _animations.Keys.Where(k => !excluded.Contains(k)).ToList();
    }

    public override bool HasAnimation(string animationName)
    {
        return _animations.ContainsKey(animationName);
    }

    public override bool HasSprite(string spriteName)
    {
        return _sprites.ContainsKey(spriteName);
    }

    public override Dictionary<string, BitmapSource> LoadStaticSprites(int maxWidth, int maxHeight)
    {
        var sprites = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, entry) in _sprites)
        {
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            ms.Position = 0;
            sprites[name] = SpriteLoader.LoadSingle(ms, maxWidth, maxHeight);
        }
        return sprites;
    }

    public override List<BitmapSource> LoadAnimationFrames(string animationName, int maxWidth, int maxHeight)
    {
        if (!_animations.TryGetValue(animationName, out var entries) || entries.Count == 0)
        {
            return [];
        }

        var frames = new List<BitmapSource>(entries.Count);
        foreach (var entry in entries)
        {
            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            ms.Position = 0;
            frames.Add(SpriteLoader.LoadSingle(ms, maxWidth, maxHeight));
        }
        return frames;
    }

    public override void Dispose()
    {
        _zipArchive.Dispose();
        _fileStream.Dispose();
        base.Dispose();
    }
}
