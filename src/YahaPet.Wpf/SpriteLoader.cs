// src/YahaPet.Wpf/SpriteLoader.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YahaPet.Wpf;

/// Loads and scales PNG frames, preserving aspect ratio like the original's
/// QPixmap.scaled(..., KeepAspectRatio). Assumes files are pre-optimized by
/// YahaPet.AssetPipeline (Tasks 7-8), so decoding at native size is cheap.
public static class SpriteLoader
{
    public static BitmapSource LoadSingle(string filePath, int maxWidth, int maxHeight)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        double scale = Math.Min((double)maxWidth / bitmap.PixelWidth, (double)maxHeight / bitmap.PixelHeight);
        if (scale >= 1.0) return bitmap;

        var scaled = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }

    public static List<BitmapSource> LoadFrames(string animationFolder, int maxWidth, int maxHeight)
    {
        var files = Directory.GetFiles(animationFolder, "*.png")
            .OrderBy(f =>
            {
                string stem = Path.GetFileNameWithoutExtension(f);
                string leading = stem.Split('-')[0];
                return int.TryParse(leading, out int n) ? n : int.MaxValue;
            })
            .ToList();

        return files.Select(f => LoadSingle(f, maxWidth, maxHeight)).ToList();
    }

    public static BitmapSource Mirror(BitmapSource source)
    {
        var mirrored = new TransformedBitmap(source, new ScaleTransform(-1, 1));
        mirrored.Freeze();
        return mirrored;
    }
}
