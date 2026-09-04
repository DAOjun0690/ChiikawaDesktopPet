// src/ChiikawaDesktopPet.Wpf/InteractionWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ChiikawaDesktopPet.Wpf;

public partial class InteractionWindow : Window
{
    private static List<BitmapSource>? _cachedFrames;
    private static readonly object _cacheLock = new();

    private readonly List<BitmapSource> _frames = [];
    private readonly DispatcherTimer _playTimer = new();
    private int _frameIndex;

    public event Action? Completed;

    public InteractionWindow(int fps = 15)
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeToolWindow(hwnd);
        };

        LoadFrames();

        int targetFps = fps > 0 ? fps : 15;
        _playTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / targetFps);
        _playTimer.Tick += OnPlayTimerTick;
    }

    private void LoadFrames()
    {
        lock (_cacheLock)
        {
            if (_cachedFrames == null)
            {
                var list = new List<BitmapSource>();
                string folder = Path.Combine(AppContext.BaseDirectory, "assets", "coanimations", "chiikawa_momonga");
                string zipPath = Path.Combine(AppContext.BaseDirectory, "assets", "coanimations.zip");
                string packsZipPath = Path.Combine(AppContext.BaseDirectory, "assets", "packs", "coanimations.zip");

                if (Directory.Exists(folder))
                {
                    var files = Directory.EnumerateFiles(folder, "*.png")
                        .OrderBy(f =>
                        {
                            string name = Path.GetFileNameWithoutExtension(f);
                            return int.TryParse(name, out int n) ? n : int.MaxValue;
                        })
                        .ToList();

                    foreach (var file in files)
                    {
                        list.Add(SpriteLoader.LoadSingle(file, int.MaxValue, int.MaxValue));
                    }
                }
                else
                {
                    string targetZip = File.Exists(zipPath) ? zipPath : File.Exists(packsZipPath) ? packsZipPath : "";
                    if (!string.IsNullOrEmpty(targetZip))
                    {
                        using var fs = new FileStream(targetZip, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
                        var entries = archive.Entries
                            .Where(e => e.FullName.Replace('\\', '/').StartsWith("chiikawa_momonga/", StringComparison.OrdinalIgnoreCase) &&
                                        e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(e => SpriteLoader.GetLeadingNumber(e.Name))
                            .ToList();

                        foreach (var entry in entries)
                        {
                            using var s = entry.Open();
                            using var ms = new MemoryStream();
                            s.CopyTo(ms);
                            ms.Position = 0;
                            list.Add(SpriteLoader.LoadSingle(ms, int.MaxValue, int.MaxValue));
                        }
                    }
                }
                _cachedFrames = list;
            }

            _frames.AddRange(_cachedFrames);
        }
    }

    public void Play(double left, double top)
    {
        Left = left;
        Top = top;
        _frameIndex = 0;

        if (_frames.Count == 0)
        {
            Completed?.Invoke();
            Close();
            return;
        }

        InteractionImage.Source = _frames[0];
        Show();
        _playTimer.Start();
    }

    private void OnPlayTimerTick(object? sender, EventArgs e)
    {
        _frameIndex++;
        if (_frameIndex >= _frames.Count)
        {
            _playTimer.Stop();
            Completed?.Invoke();
            Close();
            return;
        }

        InteractionImage.Source = _frames[_frameIndex];
    }
}
