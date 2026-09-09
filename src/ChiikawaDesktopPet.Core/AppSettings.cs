// src/ChiikawaDesktopPet.Core/AppSettings.cs
namespace ChiikawaDesktopPet.Core;

public class AppSettings
{
    public bool SoftwareRendering { get; set; } = false;
    public bool ConfineToCurrentMonitor { get; set; } = true;
    public bool EnableWindowsNotifications { get; set; } = false;
    public bool AutoSaveProfile { get; set; } = true;
}
