// src/ChiikawaDesktopPet.Core/PetProfile.cs
using System.Collections.Generic;

namespace ChiikawaDesktopPet.Core;

public class CharacterProfileItem
{
    public string CharacterName { get; set; } = string.Empty;
    public string? DialogueText { get; set; }
    public string? DialogueAlignment { get; set; } = "Center";
    public double DialogueFontSize { get; set; } = 13.0;
    public bool AlwaysShowBubble { get; set; } = false;
    public double ScaleRatio { get; set; } = 1.0;
    public string? DefaultAnimation { get; set; }
    public bool RandomAnimationsEnabled { get; set; } = true;
    public bool JumpEnabled { get; set; } = true;
}

public class PetProfile
{
    public int Version { get; set; } = 1;
    public List<CharacterProfileItem> Characters { get; set; } = [];
}
