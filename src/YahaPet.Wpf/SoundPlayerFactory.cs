// src/YahaPet.Wpf/SoundPlayerFactory.cs
using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace YahaPet.Wpf;

/// Plays a WAV file fire-and-forget on a background thread. A new SoundPlayer per call lets overlapping
/// sounds (e.g. a "grabbed" effect over an animation's own sound) play concurrently,
/// matching the original's per-instance QSoundEffect usage, while ensuring proper disposal upon completion.
public static class SoundPlayerFactory
{
    public static bool MuteAll { get; set; }

    public static void PlayIfExists(string filePath)
    {
        if (MuteAll || !File.Exists(filePath)) return;

        Task.Run(() =>
        {
            try
            {
                using var player = new SoundPlayer(filePath);
                player.PlaySync();
            }
            catch
            {
                // Suppress playback errors (e.g. unavailable sound device)
            }
        });
    }
}
