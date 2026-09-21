// src/ChiikawaDesktopPet.Core/BongoSkinManifest.cs
namespace ChiikawaDesktopPet.Core;

public class BongoSkinManifest
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Author { get; set; } = "Community";
    public string Description { get; set; } = string.Empty;

    public string BodyFile { get; set; } = "body.png";
    public string LeftUpFile { get; set; } = "left_up.png";
    public string LeftDownFile { get; set; } = "left_down.png";
    public string? LeftDownRightFile { get; set; } = "left_down_right.png";
    public string RightUpFile { get; set; } = "right_up.png";
    public string RightDownFile { get; set; } = "right_down.png";
    public string FaceNormalFile { get; set; } = "face_normal.png";
    public string? FaceFocusedFile { get; set; } = "face_focused.png";
    public string? FaceOverdriveFile { get; set; } = "face_overdrive.png";
    public string? OverdriveEffectFile { get; set; } = "effect_overdrive.png";
}
