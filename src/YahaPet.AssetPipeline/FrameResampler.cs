using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YahaPet.AssetPipeline;

public static class FrameResampler
{
    /// Keeps every `stride`-th frame, always preserving the first and last frame so an
    /// animation's start/end pose is never dropped. stride<=1 or <=2 frames is a no-op.
    public static IReadOnlyList<T> Resample<T>(IReadOnlyList<T> orderedFrames, int stride)
    {
        if (stride <= 1 || orderedFrames.Count <= 2) return orderedFrames;

        var kept = new List<T> { orderedFrames[0] };
        for (int i = stride; i < orderedFrames.Count - 1; i += stride)
            kept.Add(orderedFrames[i]);
        kept.Add(orderedFrames[^1]);
        return kept;
    }

    /// Matches the original's sort key: the integer before the first '-' in the filename
    /// stem (e.g. "9-stop.png" sorts as 9, "10.png" sorts as 10).
    public static List<string> SortFramesByLeadingNumber(IEnumerable<string> filePaths)
    {
        return filePaths
            .OrderBy(f =>
            {
                string stem = Path.GetFileNameWithoutExtension(f);
                string leading = stem.Split('-')[0];
                return int.TryParse(leading, out int n) ? n : int.MaxValue;
            })
            .ToList();
    }

    /// Applies frame resampling to every animation subfolder under `directory` (searched
    /// recursively, including `directory` itself), deleting files that resampling drops.
    /// A folder only counts as an "animation subfolder" if every PNG in it has a purely
    /// numeric leading filename (e.g. "1.png" .. "72.png") — this is what distinguishes a
    /// numbered frame sequence from a folder of distinctly-named static sprites (e.g.
    /// "grabbed.png", "spawn1.png"), which must never be thinned by stride resampling.
    /// Returns the total number of files removed.
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

            bool isFrameSequence = allFiles.All(f =>
                int.TryParse(Path.GetFileNameWithoutExtension(f).Split('-')[0], out _));
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
