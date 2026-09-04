using System;
using System.Diagnostics;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace ChiikawaDesktopPet.AssetPipeline;

public static class PngQuantizer
{
    private static readonly Lazy<string?> LazyPngquantPath = new(FindPngquantExecutable);

    public static string? PngquantPath => LazyPngquantPath.Value;
    public static bool HasPngquant => !string.IsNullOrEmpty(PngquantPath);

    public static void Quantize(string inputPath, string outputPath)
    {
        if (HasPngquant && TryQuantizeWithPngquant(inputPath, outputPath))
        {
            return;
        }

        QuantizeWithImageSharp(inputPath, outputPath);
    }

    public static byte[] Quantize(byte[] inputBytes)
    {
        using var inStream = new MemoryStream(inputBytes);
        using var outStream = new MemoryStream();
        Quantize(inStream, outStream);
        return outStream.ToArray();
    }

    public static void Quantize(Stream inputStream, Stream outputStream)
    {
        using var image = Image.Load<Rgba32>(inputStream);
        var encoder = new PngEncoder
        {
            ColorType = PngColorType.Palette,
            Quantizer = new WuQuantizer(new QuantizerOptions
            {
                MaxColors = 256
            }),
            CompressionLevel = PngCompressionLevel.BestCompression
        };
        image.Save(outputStream, encoder);
    }

    private static void QuantizeWithImageSharp(string inputPath, string outputPath)
    {
        using var inStream = File.OpenRead(inputPath);
        using var outStream = File.Create(outputPath);
        Quantize(inStream, outStream);
    }

    private static bool TryQuantizeWithPngquant(string inputPath, string outputPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = PngquantPath!,
                Arguments = $"--force --strip --speed 1 --output \"{outputPath}\" \"{inputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;
            process.WaitForExit(10000);
            return process.ExitCode == 0 && File.Exists(outputPath);
        }
        catch
        {
            return false;
        }
    }

    private static string? FindPngquantExecutable()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "pngquant.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "pngquant.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "tools", "pngquant.exe")
        ];

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        // Check system PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir, "pngquant.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                    // Ignore invalid path entries
                }
            }
        }

        return null;
    }
}
