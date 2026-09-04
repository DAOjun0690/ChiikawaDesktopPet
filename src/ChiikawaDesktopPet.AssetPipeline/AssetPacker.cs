using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace ChiikawaDesktopPet.AssetPipeline;

public static class AssetPacker
{
    public sealed record PackReport(
        int TotalFiles,
        long OriginalBytes,
        long PackedBytes,
        TimeSpan Elapsed);

    public static PackReport PackAll(string sourceOptimizedDir, string sourceCoanimationsDir, string outputPacksDir)
    {
        var sw = Stopwatch.StartNew();
        Directory.CreateDirectory(outputPacksDir);

        int totalFiles = 0;
        long totalOriginalBytes = 0;
        long totalPackedBytes = 0;

        // 1. Pack individual characters in parallel
        if (Directory.Exists(sourceOptimizedDir))
        {
            var charDirs = Directory.GetDirectories(sourceOptimizedDir);
            Parallel.ForEach(charDirs, charDir =>
            {
                string charName = Path.GetFileName(charDir);
                string zipPath = Path.Combine(outputPacksDir, $"{charName}.zip");
                var report = PackDirectoryToZip(charDir, zipPath);

                Interlocked.Add(ref totalFiles, report.Files);
                Interlocked.Add(ref totalOriginalBytes, report.OriginalBytes);
                Interlocked.Add(ref totalPackedBytes, report.PackedBytes);

                double origMb = report.OriginalBytes / 1048576.0;
                double packMb = report.PackedBytes / 1048576.0;
                double ratio = report.OriginalBytes > 0 ? (1.0 - (double)report.PackedBytes / report.OriginalBytes) * 100 : 0;
                Console.WriteLine($"[Pack] {charName}.zip: {report.Files} files, {origMb:F2} MB -> {packMb:F2} MB (-{ratio:F1}%)");
            });
        }

        // 2. Pack co-animations
        if (Directory.Exists(sourceCoanimationsDir))
        {
            string coanimZipPath = Path.Combine(outputPacksDir, "coanimations.zip");
            var report = PackDirectoryToZip(sourceCoanimationsDir, coanimZipPath);

            Interlocked.Add(ref totalFiles, report.Files);
            Interlocked.Add(ref totalOriginalBytes, report.OriginalBytes);
            Interlocked.Add(ref totalPackedBytes, report.PackedBytes);

            double origMb = report.OriginalBytes / 1048576.0;
            double packMb = report.PackedBytes / 1048576.0;
            double ratio = report.OriginalBytes > 0 ? (1.0 - (double)report.PackedBytes / report.OriginalBytes) * 100 : 0;
            Console.WriteLine($"[Pack] coanimations.zip: {report.Files} files, {origMb:F2} MB -> {packMb:F2} MB (-{ratio:F1}%)");
        }

        sw.Stop();
        return new PackReport(totalFiles, totalOriginalBytes, totalPackedBytes, sw.Elapsed);
    }

    public static (int Files, long OriginalBytes, long PackedBytes) PackDirectoryToZip(string sourceDir, string targetZipPath)
    {
        string tempZip = targetZipPath + ".tmp";
        if (File.Exists(tempZip)) File.Delete(tempZip);

        int filesCount = 0;
        long origBytes = 0;

        using (var zipStream = new FileStream(tempZip, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            foreach (var filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                filesCount++;
                var fileInfo = new FileInfo(filePath);
                origBytes += fileInfo.Length;

                string relPath = Path.GetRelativePath(sourceDir, filePath).Replace('\\', '/');
                var entry = archive.CreateEntry(relPath, CompressionLevel.SmallestSize);

                using var entryStream = entry.Open();
                if (string.Equals(fileInfo.Extension, ".png", StringComparison.OrdinalIgnoreCase))
                {
                    using var inStream = File.OpenRead(filePath);
                    PngQuantizer.Quantize(inStream, entryStream);
                }
                else
                {
                    using var inStream = File.OpenRead(filePath);
                    inStream.CopyTo(entryStream);
                }
            }
        }

        if (File.Exists(targetZipPath)) File.Delete(targetZipPath);
        File.Move(tempZip, targetZipPath);

        long packedBytes = new FileInfo(targetZipPath).Length;
        return (filesCount, origBytes, packedBytes);
    }
}
