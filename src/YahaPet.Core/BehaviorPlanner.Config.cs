using System.Collections.Generic;

namespace YahaPet.Core;

public static partial class BehaviorPlanner
{
    public const int DefaultFps = 40;

    public static int GetFps(IReadOnlyDictionary<string, CharacterConfig>? config, string characterName, string animationName)
    {
        if (config is null) return DefaultFps;
        if (!config.TryGetValue(characterName, out var character)) return DefaultFps;
        if (!character.Animations.TryGetValue(animationName, out var anim)) return DefaultFps;
        return anim.Fps;
    }
}
