using System;
using System.Collections.Generic;

namespace ChiikawaDesktopPet.Core;

public sealed class CharacterConfig
{
    public double Scale { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;
    public Dictionary<string, AnimationConfig> Animations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
