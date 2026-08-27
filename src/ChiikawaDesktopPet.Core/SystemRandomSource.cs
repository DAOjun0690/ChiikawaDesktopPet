namespace ChiikawaDesktopPet.Core;

public sealed class SystemRandomSource : IRandomSource
{
    public static readonly SystemRandomSource Shared = new();

    public int Next(int minInclusive, int maxExclusive) => Random.Shared.Next(minInclusive, maxExclusive);
}
