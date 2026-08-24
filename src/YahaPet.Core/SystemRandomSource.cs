namespace YahaPet.Core;

public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random = new();

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
