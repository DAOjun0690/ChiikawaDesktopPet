// src/ChiikawaDesktopPet.Wpf/DialogueImageControl.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ChiikawaDesktopPet.Wpf;

public class DialogueImageControl : Border
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly ConcurrentDictionary<string, byte[]> ImageMemoryCache = new();

    private readonly Image _imageControl;
    private readonly TextBlock _statusTextBlock;
    private DispatcherTimer? _gifTimer;
    private List<(BitmapFrame Frame, int DelayMs)>? _gifFrames;
    private int _currentFrameIndex;

    public string SourceUrl { get; }
    public string AltText { get; }

    public bool IsImageLoaded { get; private set; }

    private Action? _imageLoaded;
    public event Action? ImageLoaded
    {
        add
        {
            _imageLoaded += value;
            if (IsImageLoaded)
            {
                value?.Invoke();
            }
        }
        remove
        {
            _imageLoaded -= value;
        }
    }

    public DialogueImageControl(string sourceUrl, string altText, double maxWidth = 260, double maxHeight = 200, Action? onImageLoaded = null)
    {
        SourceUrl = sourceUrl;
        AltText = altText;
        if (onImageLoaded != null)
        {
            ImageLoaded += onImageLoaded;
        }

        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0));
        Margin = new Thickness(0, 4, 0, 4);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        MaxWidth = Math.Max(50, maxWidth);
        MaxHeight = Math.Max(50, maxHeight);

        _imageControl = new Image
        {
            Stretch = Stretch.Uniform,
            MaxWidth = MaxWidth,
            MaxHeight = MaxHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        RenderOptions.SetBitmapScalingMode(_imageControl, BitmapScalingMode.HighQuality);

        _statusTextBlock = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(AltText) ? "圖片載入中..." : $"載入中: {AltText}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 6, 10, 6),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        };

        var container = new Grid();
        container.Children.Add(_statusTextBlock);
        container.Children.Add(_imageControl);
        Child = container;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        if (!TryLoadSync())
        {
            _ = LoadImageAsync();
        }
    }

    private bool TryLoadSync()
    {
        if (string.IsNullOrWhiteSpace(SourceUrl)) return false;

        string cleaned = SourceUrl.Trim().Trim('<', '>', '"', '\'').Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return false;

        byte[]? data = null;
        if (ImageMemoryCache.TryGetValue(cleaned, out var cached))
        {
            data = cached;
        }
        else if (ImageMemoryCache.TryGetValue(SourceUrl, out var cachedRaw))
        {
            data = cachedRaw;
        }
        else
        {
            string localPath = cleaned;
            if (localPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(localPath, UriKind.Absolute, out var fileUri))
                {
                    localPath = fileUri.LocalPath;
                }
            }
            else if (!Path.IsPathRooted(localPath) && !localPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !localPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                localPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, localPath));
            }

            if (File.Exists(localPath))
            {
                try
                {
                    data = File.ReadAllBytes(localPath);
                    ImageMemoryCache[SourceUrl] = data;
                    ImageMemoryCache[cleaned] = data;
                }
                catch
                {
                    return false;
                }
            }
        }

        if (data != null && data.Length > 0)
        {
            try
            {
                ApplyImageData(data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_gifFrames != null && _gifFrames.Count > 1)
        {
            StartGifAnimation();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopGifAnimation();
    }

    public void UpdateMaxDimensions(double maxWidth, double maxHeight)
    {
        MaxWidth = Math.Max(50, maxWidth);
        MaxHeight = Math.Max(50, maxHeight);
        _imageControl.MaxWidth = MaxWidth;
        _imageControl.MaxHeight = MaxHeight;
    }

    private async Task LoadImageAsync()
    {
        try
        {
            byte[]? data = await FetchImageDataAsync(SourceUrl);
            if (data == null || data.Length == 0)
            {
                ShowError(string.IsNullOrWhiteSpace(AltText) ? "無法載入圖片" : $"載入失敗: {AltText}");
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    ApplyImageData(data);
                }
                catch (Exception ex)
                {
                    ShowError(string.IsNullOrWhiteSpace(AltText) ? "圖片格式不支援" : $"無法解析: {AltText}");
                    System.Diagnostics.Debug.WriteLine($"[DialogueImageControl] Decode error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                ShowError(string.IsNullOrWhiteSpace(AltText) ? "圖片載入異常" : $"載入失敗: {AltText}");
            });
            System.Diagnostics.Debug.WriteLine($"[DialogueImageControl] Load error: {ex.Message}");
        }
    }

    private static async Task<byte[]?> FetchImageDataAsync(string urlOrPath)
    {
        if (string.IsNullOrWhiteSpace(urlOrPath)) return null;

        string cleaned = urlOrPath.Trim().Trim('<', '>', '"', '\'').Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return null;

        if (ImageMemoryCache.TryGetValue(cleaned, out var cached))
        {
            return cached;
        }

        if (Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var response = await HttpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode) return null;

            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            ImageMemoryCache[urlOrPath] = bytes;
            ImageMemoryCache[cleaned] = bytes;
            return bytes;
        }

        string localPath = cleaned;
        if (localPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(localPath, UriKind.Absolute, out var fileUri))
            {
                localPath = fileUri.LocalPath;
            }
        }
        else if (!Path.IsPathRooted(localPath))
        {
            localPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, localPath));
        }

        if (File.Exists(localPath))
        {
            byte[] bytes = await File.ReadAllBytesAsync(localPath);
            ImageMemoryCache[urlOrPath] = bytes;
            ImageMemoryCache[cleaned] = bytes;
            return bytes;
        }

        return null;
    }

    private void ApplyImageData(byte[] data)
    {
        using var stream = new MemoryStream(data);

        BitmapDecoder decoder;
        try
        {
            decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        }
        catch
        {
            // Fallback to BitmapImage if BitmapDecoder cannot infer
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new MemoryStream(data);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            _imageControl.Source = bmp;
            DisplayImageControl();
            return;
        }

        if (decoder is GifBitmapDecoder gifDecoder && gifDecoder.Frames.Count > 1)
        {
            _gifFrames = new List<(BitmapFrame Frame, int DelayMs)>();
            foreach (var frame in gifDecoder.Frames)
            {
                int delayMs = 100;
                if (frame.Metadata is BitmapMetadata metadata && metadata.ContainsQuery("/grctlext/Delay"))
                {
                    var val = metadata.GetQuery("/grctlext/Delay");
                    if (val is ushort cs && cs > 0)
                    {
                        delayMs = cs * 10;
                    }
                }
                if (delayMs < 20) delayMs = 100;

                _gifFrames.Add((frame, delayMs));
            }

            _currentFrameIndex = 0;
            _imageControl.Source = _gifFrames[0].Frame;
            DisplayImageControl();
            StartGifAnimation();
        }
        else if (decoder.Frames.Count > 0)
        {
            _imageControl.Source = decoder.Frames[0];
            DisplayImageControl();
        }
        else
        {
            ShowError(string.IsNullOrWhiteSpace(AltText) ? "圖片無效" : AltText);
        }
    }

    private void DisplayImageControl()
    {
        _statusTextBlock.Visibility = Visibility.Collapsed;
        _imageControl.Visibility = Visibility.Visible;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        IsImageLoaded = true;
        _imageLoaded?.Invoke();
    }

    private void ShowError(string message)
    {
        _imageControl.Visibility = Visibility.Collapsed;
        _statusTextBlock.Visibility = Visibility.Visible;
        _statusTextBlock.Text = $"⚠️ {message}";
        _statusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(180, 50, 50));
        Background = new SolidColorBrush(Color.FromArgb(20, 220, 50, 50));
        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 220, 50, 50));
        BorderThickness = new Thickness(1);
        _imageLoaded?.Invoke();
    }

    private void StartGifAnimation()
    {
        if (_gifFrames == null || _gifFrames.Count <= 1) return;

        StopGifAnimation();

        _gifTimer = new DispatcherTimer(DispatcherPriority.Render);
        _gifTimer.Interval = TimeSpan.FromMilliseconds(_gifFrames[_currentFrameIndex].DelayMs);
        _gifTimer.Tick += (s, e) =>
        {
            if (_gifFrames == null || _gifFrames.Count == 0) return;

            _currentFrameIndex = (_currentFrameIndex + 1) % _gifFrames.Count;
            _imageControl.Source = _gifFrames[_currentFrameIndex].Frame;
            _gifTimer.Interval = TimeSpan.FromMilliseconds(_gifFrames[_currentFrameIndex].DelayMs);
        };
        _gifTimer.Start();
    }

    private void StopGifAnimation()
    {
        if (_gifTimer != null)
        {
            _gifTimer.Stop();
            _gifTimer = null;
        }
    }
}
