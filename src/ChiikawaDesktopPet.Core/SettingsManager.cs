// src/ChiikawaDesktopPet.Core/SettingsManager.cs
using System;
using System.IO;
using System.Text.Json;

namespace ChiikawaDesktopPet.Core;

public static class SettingsManager
{
    private static readonly object FileLock = new();

    public static string GetSettingsFilePath()
    {
        return ResolveFilePath("settings.json");
    }

    public static string GetAutoSaveProfilePath()
    {
        return ResolveFilePath("autosave_profile.json");
    }

    private static string ResolveFilePath(string fileName)
    {
        try
        {
            string appDir = AppContext.BaseDirectory;
            string primaryPath = Path.Combine(appDir, fileName);

            if (File.Exists(primaryPath))
            {
                return primaryPath;
            }

            // Test directory write permission with a probe file, then delete it immediately.
            string probePath = Path.Combine(appDir, $".probe_{Guid.NewGuid():N}.tmp");
            using (var fs = new FileStream(probePath, FileMode.CreateNew, FileAccess.ReadWrite))
            {
            }
            try { File.Delete(probePath); } catch { }
            return primaryPath;
        }
        catch
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(localAppData, "ChiikawaDesktopPet");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            return Path.Combine(appFolder, fileName);
        }
    }

    public static AppSettings Load(string? customPath = null)
    {
        string path = customPath ?? GetSettingsFilePath();
        if (!File.Exists(path)) return new AppSettings();

        try
        {
            lock (FileLock)
            {
                var fi = new FileInfo(path);
                if (fi.Length == 0)
                {
                    try { File.Delete(path); } catch { }
                    return new AppSettings();
                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    try { File.Delete(path); } catch { }
                    return new AppSettings();
                }

                return JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings, string? customPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string path = customPath ?? GetSettingsFilePath();
        try
        {
            lock (FileLock)
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(settings, ConfigJsonContext.Default.AppSettings);
                File.WriteAllText(path, json);
            }
        }
        catch
        {
            // Suppress secondary IO exception
        }
    }
}
