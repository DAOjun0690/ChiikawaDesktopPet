using System.Collections.Generic;
using System.IO;
using System.Linq;
using YahaPet.AssetPipeline;
using Xunit;

public class FrameResamplerTests
{
    [Fact]
    public void Resample_StrideTwo_KeepsFirstLastAndEveryOther()
    {
        var frames = Enumerable.Range(1, 10).ToList();
        var result = FrameResampler.Resample(frames, stride: 2);
        Assert.Equal(new List<int> { 1, 3, 5, 7, 9, 10 }, result);
    }

    [Fact]
    public void Resample_StrideOne_ReturnsAllFrames()
    {
        var frames = Enumerable.Range(1, 5).ToList();
        var result = FrameResampler.Resample(frames, stride: 1);
        Assert.Equal(frames, result);
    }

    [Fact]
    public void Resample_TwoOrFewerFrames_ReturnsAllFrames()
    {
        var frames = new List<int> { 1, 2 };
        var result = FrameResampler.Resample(frames, stride: 5);
        Assert.Equal(frames, result);
    }

    [Fact]
    public void SortFramesByLeadingNumber_SortsNumericallyNotLexically()
    {
        // Matches the original's sort key: int(f.stem.split('-')[0]), so "10.png" sorts
        // after "9.png" (not before it, as plain string sort would place it).
        var files = new List<string> { "10.png", "2.png", "1.png", "9-stop.png" };
        var sorted = FrameResampler.SortFramesByLeadingNumber(files);
        Assert.Equal(new List<string> { "1.png", "2.png", "9-stop.png", "10.png" }, sorted);
    }

    [Fact]
    public void ResampleDirectoryInPlace_RemovesFilesNotKeptByStride()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"resample-{System.Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        try
        {
            for (int i = 1; i <= 10; i++)
                File.WriteAllBytes(Path.Combine(dir, $"{i}.png"), new byte[] { 1 });

            int removed = FrameResampler.ResampleDirectoryInPlace(dir, stride: 2);

            Assert.Equal(4, removed); // 10 frames -> 6 kept (1,3,5,7,9,10) -> 4 removed
            var remaining = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToList();
            Assert.Equal(6, remaining.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
