using System;
using System.Collections.Generic;

namespace YahaPet.Core;

public sealed class CharacterConfig
{
    public Dictionary<string, AnimationConfig> Animations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
