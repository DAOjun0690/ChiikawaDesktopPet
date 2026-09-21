// src/ChiikawaDesktopPet.Core/BongoConfig.cs
namespace ChiikawaDesktopPet.Core;

public enum BongoInputMode
{
    KeyboardAndMouse,
    PureKeyboard
}

public class BongoConfig
{
    public bool IsEnabled { get; set; } = false;
    public string CurrentSkinKey { get; set; } = "chiikawa";
    public double PositionX { get; set; } = -1;
    public double PositionY { get; set; } = -1;
    public double Scale { get; set; } = 1.0;
    public bool IsLocked { get; set; } = false;
    public bool ClickThrough { get; set; } = false;
    public int FocusCpmThreshold { get; set; } = 120;
    public int OverdriveCpmThreshold { get; set; } = 300;
    public BongoInputMode InputMode { get; set; } = BongoInputMode.KeyboardAndMouse;
    public bool TrackMouseMotion { get; set; } = true;
    public bool MirrorMouseMotion { get; set; } = true;
}

