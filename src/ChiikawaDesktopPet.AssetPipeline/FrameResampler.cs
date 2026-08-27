// src/ChiikawaDesktopPet.AssetPipeline/FrameResampler.cs
using System;
using System.Collections.Generic;
using System.IO;

namespace ChiikawaDesktopPet.AssetPipeline;

public static class FrameResampler
{
    /// Keeps every `stride`-th frame, always preserving the first and last frame so an
    /// animation's start/end pose is never dropped. stride<=1 or <=2 frames is a no-op.
    public static IReadOnlyList<T> Resample<T>(IReadOnlyList<T> orderedFrames, int stride)
    {
        if (stride <= 1 || orderedFrames.Count <= 2) return orderedFrames;

        var kept = new List<T>((orderedFrames.Count / stride) + 2) { orderedFrames[0] };
        for (int i = stride; i < orderedFrames.Count - 1; i += stride)
            kept.Add(orderedFrames[i]);
        kept.Add(orderedFrames[^1]);
        return kept;
    }

    /// Matches the original's sort key: the integer before the first '-' in the filename
    /// stem (e.g. "9-stop.png" sorts as 9, "10.png" sorts as 10).
    public static List<string> SortFramesByLeadingNumber(IEnumerable<string> filePaths)
    {
        var list = new List<string>(filePaths);
        list.Sort(static (a, b) => GetLeadingNumber(a).CompareTo(GetLeadingNumber(b)));
        return list;
    }

    public static int GetLeadingNumber(string filePath)
    {
        ReadOnlySpan<char> fileName = Path.GetFileNameWithoutExtension(filePath.AsSpan());
        int dashIndex = fileName.IndexOf('-');
        ReadOnlySpan<char> leading = dashIndex >= 0 ? fileName[..dashIndex] : fileName;
        return int.TryParse(leading, out int n) ? n : int.MaxValue;
    }

    public static bool HasPureNumericLeading(string filePath)
    {
        ReadOnlySpan<char> fileName = Path.GetFileNameWithoutExtension(filePath.AsSpan());
        int dashIndex = fileName.IndexOf('-');
        ReadOnlySpan<char> leading = dashIndex >= 0 ? fileName[..dashIndex] : fileName;
        return int.TryParse(leading, out _);
    }

    /// Applies frame resampling to every animation subfolder under `directory` (searched
    /// recursively, including `directory` itself), deleting files that resampling drops.
    public static int ResampleDirectoryInPlace(string directory, int stride)
    {
        if (stride <= 1) return 0;

        int removed = 0;
        var folders = new List<string> { directory };
        folders.AddRange(Directory.GetDirectories(directory, "*", SearchOption.AllDirectories));

        foreach (var folder in folders)
        {
            var allFiles = Directory.GetFiles(folder, "*.png");
            if (allFiles.Length == 0) continue;

            bool isFrameSequence = true;
            foreach (var f in allFiles)
            {
                if (!HasPureNumericLeading(f))
                {
                    isFrameSequence = false;
                    break;
                }
            }
            if (!isFrameSequence) continue;

            var sorted = SortFramesByLeadingNumber(allFiles);
            var kept = new HashSet<string>(Resample(sorted, stride));

            foreach (var file in sorted)
            {
                if (!kept.Contains(file))
                {
                    File.Delete(file);
                    removed++;
                }
            }
        }
        return removed;
    }
}
