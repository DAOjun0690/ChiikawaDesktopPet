using System;
using YahaPet.AssetPipeline;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: YahaPet.AssetPipeline <sourceDir> <outputDir> [--max-dimension N] [--frame-stride N]");
    return 1;
}

string sourceDir = args[0];
string outputDir = args[1];
int maxDimension = 320;
int frameStride = 1;

for (int i = 2; i < args.Length - 1; i++)
{
    if (args[i] == "--max-dimension" && int.TryParse(args[i + 1], out var dim)) maxDimension = dim;
    if (args[i] == "--frame-stride" && int.TryParse(args[i + 1], out var stride)) frameStride = stride;
}

int resized = ImageOptimizer.OptimizeDirectory(sourceDir, outputDir, maxDimension);
Console.WriteLine($"Resized/copied {resized} PNG files into {outputDir}");

if (frameStride > 1)
{
    int removed = FrameResampler.ResampleDirectoryInPlace(outputDir, frameStride);
    Console.WriteLine($"Removed {removed} resampled-out frames (stride={frameStride})");
}

return 0;
