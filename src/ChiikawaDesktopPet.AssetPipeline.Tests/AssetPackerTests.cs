using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using ChiikawaDesktopPet.AssetPipeline;
using Xunit;

public class AssetPackerTests
{
    private static string CreateTempPng(string folder, string relativeName, int width, int height)
    {
        string fullPath = Path.Combine(folder, relativeName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(128, 255, 100, 50));
        }
        bmp.Save(fullPath, ImageFormat.Png);
        return fullPath;
    }

    [Fact]
    public void PngQuantizer_Quantize_OutputsValidPngWithSameDimensions()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"quant-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string src = CreateTempPng(tempDir, "source.png", 64, 48);
            string dst = Path.Combine(tempDir, "quantized.png");

            PngQuantizer.Quantize(src, dst);

            Assert.True(File.Exists(dst));
            using var bmp = new Bitmap(dst);
            Assert.Equal(64, bmp.Width);
            Assert.Equal(48, bmp.Height);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AssetPacker_PackDirectoryToZip_CreatesZipWithEntries()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"pack-test-{Guid.NewGuid()}");
        string srcDir = Path.Combine(tempDir, "character_src");
        string targetZip = Path.Combine(tempDir, "character.zip");
        Directory.CreateDirectory(srcDir);

        try
        {
            CreateTempPng(srcDir, "sprites/spawn.png", 50, 50);
            CreateTempPng(srcDir, "animations/walk/0.png", 50, 50);
            CreateTempPng(srcDir, "animations/walk/1.png", 50, 50);
            File.WriteAllText(Path.Combine(srcDir, "meta.txt"), "hello");

            var (files, origBytes, packedBytes) = AssetPacker.PackDirectoryToZip(srcDir, targetZip);

            Assert.Equal(4, files);
            Assert.True(origBytes > 0);
            Assert.True(packedBytes > 0);
            Assert.True(File.Exists(targetZip));

            using var archive = ZipFile.OpenRead(targetZip);
            Assert.Equal(4, archive.Entries.Count);
            Assert.NotNull(archive.GetEntry("sprites/spawn.png"));
            Assert.NotNull(archive.GetEntry("animations/walk/0.png"));
            Assert.NotNull(archive.GetEntry("animations/walk/1.png"));
            Assert.NotNull(archive.GetEntry("meta.txt"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
