using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ChiikawaDesktopPet.AssetPipeline;
using Xunit;

public class ImageOptimizerTests
{
    private static string CreateTempPng(int width, int height)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pipeline-test-{Guid.NewGuid()}.png");
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.CornflowerBlue);
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    [Fact]
    public void OptimizeImage_LargerThanTarget_IsDownscaledPreservingAspectRatio()
    {
        string source = CreateTempPng(400, 300);
        string output = Path.Combine(Path.GetTempPath(), $"pipeline-out-{Guid.NewGuid()}.png");
        try
        {
            ImageOptimizer.OptimizeImage(source, output, maxDimension: 200);
            using var result = new Bitmap(output);
            Assert.Equal(200, result.Width);
            Assert.Equal(150, result.Height);
        }
        finally
        {
            File.Delete(source);
            File.Delete(output);
        }
    }

    [Fact]
    public void OptimizeImage_AlreadyWithinTarget_IsCopiedUnchanged()
    {
        string source = CreateTempPng(100, 80);
        string output = Path.Combine(Path.GetTempPath(), $"pipeline-out-{Guid.NewGuid()}.png");
        try
        {
            ImageOptimizer.OptimizeImage(source, output, maxDimension: 320);
            using var result = new Bitmap(output);
            Assert.Equal(100, result.Width);
            Assert.Equal(80, result.Height);
        }
        finally
        {
            File.Delete(source);
            File.Delete(output);
        }
    }

    [Fact]
    public void OptimizeDirectory_ProcessesAllPngsRecursively()
    {
        string sourceDir = Path.Combine(Path.GetTempPath(), $"pipeline-src-{Guid.NewGuid()}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"pipeline-dst-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(sourceDir, "walkleft"));
        try
        {
            File.Copy(CreateTempPng(400, 300), Path.Combine(sourceDir, "walkleft", "1.png"));
            File.Copy(CreateTempPng(400, 300), Path.Combine(sourceDir, "walkleft", "2.png"));

            int count = ImageOptimizer.OptimizeDirectory(sourceDir, outputDir, maxDimension: 200);

            Assert.Equal(2, count);
            Assert.True(File.Exists(Path.Combine(outputDir, "walkleft", "1.png")));
            Assert.True(File.Exists(Path.Combine(outputDir, "walkleft", "2.png")));
        }
        finally
        {
            Directory.Delete(sourceDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public void OptimizeDirectory_CopiesNonPngFilesRecursively()
    {
        string sourceDir = Path.Combine(Path.GetTempPath(), $"pipeline-src-{Guid.NewGuid()}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"pipeline-dst-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(sourceDir, "extra"));
        try
        {
            File.WriteAllText(Path.Combine(sourceDir, "extra", "test.txt"), "dummy text content");
            File.WriteAllText(Path.Combine(sourceDir, "icon.ico"), "dummy ico");

            int count = ImageOptimizer.OptimizeDirectory(sourceDir, outputDir, maxDimension: 200);

            Assert.Equal(0, count); // count only tracks PNGs processed
            Assert.True(File.Exists(Path.Combine(outputDir, "extra", "test.txt")));
            Assert.True(File.Exists(Path.Combine(outputDir, "icon.ico")));
            Assert.Equal("dummy text content", File.ReadAllText(Path.Combine(outputDir, "extra", "test.txt")));
        }
        finally
        {
            Directory.Delete(sourceDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }
}
