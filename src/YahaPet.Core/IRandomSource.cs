namespace YahaPet.Core;

/// Abstraction over System.Random.Next(int,int) so behavior logic can be
/// tested with fixed roll sequences instead of real randomness.
public interface IRandomSource
{
    /// Returns a value in [minInclusive, maxExclusive), matching System.Random.Next(int,int).
    int Next(int minInclusive, int maxExclusive);
}
