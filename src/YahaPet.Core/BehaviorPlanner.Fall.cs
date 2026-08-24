namespace YahaPet.Core;

public static partial class BehaviorPlanner
{
    public sealed record FallOutcome(bool Crashed, PetPoint LandingPoint, int DurationMs);

    /// Crash odds match the original's random.randint(0,100) <= 30 (31/101 ≈ 30.7%,
    /// documented in the spec as "roughly 30%").
    public static FallOutcome PlanFall(PetPoint currentPos, int screenHeight, int landingY, int characterHeight, IRandomSource random)
    {
        int durationMs = (int)(1.5 * (screenHeight - currentPos.Y));
        bool crashed = random.Next(0, 101) <= 30;
        var landingPoint = new PetPoint(currentPos.X, landingY - characterHeight);
        return new FallOutcome(crashed, landingPoint, durationMs);
    }
}
