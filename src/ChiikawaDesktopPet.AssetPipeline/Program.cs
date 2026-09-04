using System;
using System.IO;
using ChiikawaDesktopPet.AssetPipeline;

if (args.Length > 0 && (args[0] == "--pack" || args[0] == "pack"))
{
    string repoRoot = FindRepoRoot();
    string optimizedDir = Path.Combine(repoRoot, "assets", "optimized");
    string coanimationsDir = Path.Combine(repoRoot, "assets", "coanimations");
    string outputDir = Path.Combine(repoRoot, "assets", "packs");

    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--optimized-dir" && i + 1 < args.Length) optimizedDir = args[++i];
        else if (args[i] == "--coanimations-dir" && i + 1 < args.Length) coanimationsDir = args[++i];
        else if (args[i] == "--output-dir" && i + 1 < args.Length) outputDir = args[++i];
    }

    Console.WriteLine($"[AssetPacker] Starting quantization & packaging...");
    Console.WriteLine($"  Source optimized:    {optimizedDir}");
    Console.WriteLine($"  Source coanimations: {coanimationsDir}");
    Console.WriteLine($"  Output packs:        {outputDir}");
    Console.WriteLine($"  Quantizer engine:    {(PngQuantizer.HasPngquant ? $"pngquant ({PngQuantizer.PngquantPath})" : "ImageSharp (WuQuantizer 8-bit)")}");

    var report = AssetPacker.PackAll(optimizedDir, coanimationsDir, outputDir);
    double origMb = report.OriginalBytes / 1048576.0;
    double packMb = report.PackedBytes / 1048576.0;
    double savedRatio = report.OriginalBytes > 0 ? (1.0 - (double)report.PackedBytes / report.OriginalBytes) * 100 : 0;

    Console.WriteLine($"\n[AssetPacker] Finished in {report.Elapsed.TotalSeconds:F1}s!");
    Console.WriteLine($"  Total files:    {report.TotalFiles}");
    Console.WriteLine($"  Original size:  {origMb:F2} MB");
    Console.WriteLine($"  Packed size:    {packMb:F2} MB");
    Console.WriteLine($"  Space reduced:  -{savedRatio:F1}% (Saved {(origMb - packMb):F2} MB)");
    return 0;
}

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  ChiikawaDesktopPet.AssetPipeline --pack [--optimized-dir <dir>] [--coanimations-dir <dir>] [--output-dir <dir>]");
    Console.Error.WriteLine("  ChiikawaDesktopPet.AssetPipeline <sourceDir> <outputDir> [--max-dimension N] [--frame-stride N]");
    return 1;
}

string srcDir = args[0];
string outDir = args[1];
int maxDimension = 320;
int frameStride = 1;

for (int i = 2; i < args.Length - 1; i++)
{
    if (args[i] == "--max-dimension" && int.TryParse(args[i + 1], out var dim)) maxDimension = dim;
    if (args[i] == "--frame-stride" && int.TryParse(args[i + 1], out var stride)) frameStride = stride;
}

int resized = ImageOptimizer.OptimizeDirectory(srcDir, outDir, maxDimension);
Console.WriteLine($"Resized/copied {resized} PNG files into {outDir}");

if (frameStride > 1)
{
    int removed = FrameResampler.ResampleDirectoryInPlace(outDir, frameStride);
    Console.WriteLine($"Removed {removed} resampled-out frames (stride={frameStride})");
}

return 0;

static string FindRepoRoot()
{
    string? dir = Directory.GetCurrentDirectory();
    while (!string.IsNullOrEmpty(dir))
    {
        if (Directory.Exists(Path.Combine(dir, "assets")))
        {
            return dir;
        }
        dir = Path.GetDirectoryName(dir);
    }

    dir = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(dir))
    {
        if (Directory.Exists(Path.Combine(dir, "assets")))
        {
            return dir;
        }
        dir = Path.GetDirectoryName(dir);
    }

    return Directory.GetCurrentDirectory();
}
