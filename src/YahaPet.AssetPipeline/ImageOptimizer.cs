using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace YahaPet.AssetPipeline;

public static class ImageOptimizer
{
    public static int OptimizeDirectory(string sourceDir, string outputDir, int maxDimension)
    {
        int count = 0;
        foreach (var sourceFile in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            string outputFile = Path.Combine(outputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

            string ext = Path.GetExtension(sourceFile);
            if (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase))
            {
                OptimizeImage(sourceFile, outputFile, maxDimension);
                count++;
            }
            else
            {
                File.Copy(sourceFile, outputFile, overwrite: true);
            }
        }
        return count;
    }

    public static void OptimizeImage(string sourcePath, string outputPath, int maxDimension)
    {
        using var source = new Bitmap(sourcePath);
        int sourceWidth = source.Width;
        int sourceHeight = source.Height;

        int longSide = Math.Max(sourceWidth, sourceHeight);
        if (longSide <= maxDimension)
        {
            // ponytail: already within budget — copy through rather than re-encoding,
            // since GDI+'s PNG encoder isn't guaranteed to shrink an already-small file.
            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, outputPath, overwrite: true);
            }
            return;
        }

        double scale = (double)maxDimension / longSide;
        int newWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int newHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));

        using var resized = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(resized))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, 0, 0, newWidth, newHeight);
        }
        resized.Save(outputPath, ImageFormat.Png);
    }
}
