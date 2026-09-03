// src/ChiikawaDesktopPet.Wpf/CharacterWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ChiikawaDesktopPet.Core;

namespace ChiikawaDesktopPet.Wpf;

public partial class CharacterWindow : Window
{
    public string CharacterName { get; }
    public int InstanceIndex { get; }
    public string InstanceDisplayName { get; }
    public string InstanceId { get; }

    private readonly int _characterWidth;
    private readonly int _characterHeight;
    private readonly int _physicalCharacterWidth;
    private readonly int _physicalCharacterHeight;
    private int _currentSpriteWidth;
    private int _currentSpriteHeight;
    private bool _isFalling = true;
    private readonly Dictionary<string, List<BitmapSource>> _frames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapSource> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _assetRoot;

    private readonly DispatcherTimer _idleTimer = new();
    private readonly DispatcherTimer _frameTimer = new();
    private readonly DispatcherTimer _windowTrackingTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Dictionary<string, CharacterConfig> _config;
    private List<string> _otherAnimationNames = [];
    private bool _isAnimating;
    private bool _isDragging;
    private bool _isWalking;
    private bool _isJumping;
    private List<BitmapSource> _currentAnimationFrames = [];
    private int _currentFrameIndex;
    private bool _loopCurrentAnimation;
    private Action? _pendingOnComplete;

    private IntPtr? _attachedHwnd;
    private double _attachedRelativeX;

    public bool IsAttachedToWindow => _attachedHwnd != null;
    public IntPtr? AttachedWindowHwnd => _attachedHwnd;

    public event Action<bool>? RandomAnimationsEnabledChanged;
    public event Action<bool>? JumpEnabledChanged;
    public event Action? KickRequested;
    public event Action? SayHiRequested;

    private bool _jumpEnabled = true;
    private bool _isShuttingDown;
    public bool IsPetHidden { get; private set; }

    private System.Windows.Point _dragOffset;
    private readonly DispatcherTimer _holdTimer = new() { Interval = TimeSpan.FromMilliseconds(4500) };
    private bool _isShaking;
    private BitmapSource? _grabbedSprite;

    private string _customText = "";
    public bool HasCustomText => !string.IsNullOrWhiteSpace(_customText);
    public string CurrentDialogueText => _customText;
    public TextAlignment DialogueAlignment { get; private set; } = TextAlignment.Center;
    public double DialogueFontSize { get; private set; } = 13.0;
    private bool _alwaysShowBubble;
    public bool AlwaysShowBubble => _alwaysShowBubble;
    private readonly DispatcherTimer _bubbleTimer = new();
    internal DispatcherTimer? TalkActionTimer { get; set; }

    public CharacterWindow(string characterName, int instanceIndex = 1, string? instanceDisplayName = null)
    {
        InitializeComponent();
        CharacterName = characterName.ToLowerInvariant();
        InstanceIndex = instanceIndex;
        InstanceDisplayName = instanceDisplayName ?? $"{App.GetCharacterDisplayName(CharacterName)} {instanceIndex}";
        InstanceId = $"{CharacterName}_{InstanceIndex}";
        _assetRoot = Path.Combine(AppContext.BaseDirectory, "assets", CharacterName);

        _characterWidth = (int)(SystemParameters.PrimaryScreenWidth / 10);
        _characterHeight = (int)(SystemParameters.PrimaryScreenHeight / 10);
        _physicalCharacterWidth = (int)(System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width / 10);
        _physicalCharacterHeight = (int)(System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height / 10);

        _config = ConfigLoader.Load(Path.Combine(AppContext.BaseDirectory, "config.json"));
        if (_config.TryGetValue(CharacterName, out var charConfig))
        {
            if (charConfig.Scale > 0)
            {
                ScaleRatio = Math.Clamp(charConfig.Scale, 0.2, 4.0);
            }
            if (charConfig.Opacity > 0)
            {
                PetOpacity = Math.Clamp(charConfig.Opacity, 0.1, 1.0);
            }
        }

        DiscoverOtherAnimations();

        _idleTimer.Tick += (_, _) => OnIdleTick();
        _frameTimer.Tick += (_, _) => OnFrameTick();
        _bubbleTimer.Tick += (_, _) => OnBubbleTimerTick();
        _windowTrackingTimer.Tick += (_, _) => OnWindowTrackingTick();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonUp += OnMouseRightButtonUp;
        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer.Stop();
            _isShaking = true;
            SetSprite(_sprites.TryGetValue("shaken", out var shakenSprite) ? shakenSprite : null);
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Handle = hwnd;
            NativeMethods.MakeToolWindow(hwnd);

            if (_clickThrough)
            {
                NativeMethods.SetWindowClickThrough(hwnd, true);
                ClickThroughManager.Instance.Register(this);
            }

            int typeId = InteractionCoordinator.GetCharacterTypeId(CharacterName);
            NativeMethods.SetProp(hwnd, "ChiikawaDesktopPet_PetType", (IntPtr)typeId);
            NativeMethods.SetProp(hwnd, "ChiikawaDesktopPet_IsReady", (IntPtr)1);

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            InteractionCoordinator.Instance.RegisterPet(this);
        };
    }

    public IntPtr Handle { get; private set; } = IntPtr.Zero;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_clickThrough)
        {
            if (msg == NativeMethods.WM_RBUTTONDOWN)
            {
                _isRightButtonDown = true;
            }
            else if (msg == NativeMethods.WM_RBUTTONUP || msg == NativeMethods.WM_CONTEXTMENU)
            {
                _isRightButtonDown = false;
            }

            if (msg == NativeMethods.WM_NCHITTEST)
            {
                bool isRButtonDown = _isRightButtonDown ||
                                     (NativeMethods.GetKeyState(NativeMethods.VK_RBUTTON) & 0x8000) != 0 ||
                                     (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RBUTTON) & 0x8000) != 0;
                if (isRButtonDown)
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTCLIENT;
                }
                else
                {
                    handled = true;
                    return (IntPtr)NativeMethods.HTTRANSPARENT;
                }
            }
        }

        if ((uint)msg == InteractionCoordinator.Instance.MessageId)
        {
            int cmd = (int)wParam;
            switch (cmd)
            {
                case InteractionCoordinator.CMD_QUERY:
                    handled = true;
                    return (IntPtr)InteractionCoordinator.GetCharacterTypeId(CharacterName);

                case InteractionCoordinator.CMD_ENTER_INTERACTION:
                    handled = true;
                    EnterInteractionState();
                    return (IntPtr)1;

                case InteractionCoordinator.CMD_EXIT_INTERACTION:
                    handled = true;
                    int targetX = (int)NativeMethods.GetProp(hwnd, "ChiikawaDesktopPet_TargetX");
                    int targetY = (int)NativeMethods.GetProp(hwnd, "ChiikawaDesktopPet_TargetY");
                    ExitInteractionState(new PetPoint(targetX, targetY));
                    return (IntPtr)1;

                case InteractionCoordinator.CMD_MOVE_TO:
                    handled = true;
                    int moveX = (int)NativeMethods.GetProp(hwnd, "ChiikawaDesktopPet_TargetX");
                    int moveY = (int)NativeMethods.GetProp(hwnd, "ChiikawaDesktopPet_TargetY");
                    SmoothMoveTo(new PetPoint(moveX, moveY), null);
                    return (IntPtr)1;
            }
        }
        return IntPtr.Zero;
    }

    public void Spawn(double? initialX = null)
    {
        LoadStaticSprites();
        DiscoverOtherAnimations();
        ApplyOpacity();

        double startX = initialX ?? (SystemParameters.PrimaryScreenWidth / 2);
        Left = startX;
        Top = 0;
        BubbleText.Text = CurrentDialogueText;
        BubbleText.TextAlignment = DialogueAlignment;
        BubbleText.FontSize = DialogueFontSize;
        SetSprite(RandomFrom(_sprites, "spawn"));
        Show();

        FallTo();
    }

    public double ScaleRatio { get; private set; } = 1.0;

    public void SetScaleRatio(double ratio)
    {
        double clamped = Math.Clamp(ratio, 0.2, 4.0);
        if (Math.Abs(ScaleRatio - clamped) < 0.001) return;

        ScaleRatio = clamped;
        if (SpriteImage.Source is BitmapSource currentSprite)
        {
            SetSprite(currentSprite);
        }
    }

    public double PetOpacity { get; private set; } = 1.0;
    public bool SyncBubbleOpacity { get; private set; } = true;
    public event Action<double, bool>? OpacityChanged;

    public void SetOpacity(double opacity, bool syncBubble = true)
    {
        double clamped = Math.Clamp(opacity, 0.1, 1.0);
        PetOpacity = clamped;
        SyncBubbleOpacity = syncBubble;
        ApplyOpacity();
        OpacityChanged?.Invoke(PetOpacity, SyncBubbleOpacity);
    }

    public void ApplyOpacity()
    {
        if (SyncBubbleOpacity)
        {
            Opacity = PetOpacity;
            if (SpriteImage != null) SpriteImage.Opacity = 1.0;
            if (BubbleContainer != null) BubbleContainer.Opacity = 1.0;
        }
        else
        {
            Opacity = 1.0;
            if (SpriteImage != null) SpriteImage.Opacity = PetOpacity;
            if (BubbleContainer != null) BubbleContainer.Opacity = 1.0;
        }
    }

    private bool _clickThrough;
    public bool ClickThrough => _clickThrough;
    public event Action<bool>? ClickThroughChanged;
    private bool _isRightButtonDown;

    public void SetClickThrough(bool enabled)
    {
        if (_clickThrough == enabled) return;
        _clickThrough = enabled;
        _isRightButtonDown = false;

        IntPtr hwnd = Handle != IntPtr.Zero ? Handle : (IsLoaded ? new WindowInteropHelper(this).Handle : IntPtr.Zero);
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowClickThrough(hwnd, enabled);
        }

        if (enabled)
        {
            ClickThroughManager.Instance.Register(this);
        }
        else
        {
            ClickThroughManager.Instance.Unregister(this);
        }

        ClickThroughChanged?.Invoke(_clickThrough);
    }

    public void ToggleClickThrough() => SetClickThrough(!_clickThrough);

    public void TriggerContextMenuFromHook()
    {
        if (_isShuttingDown || IsPetHidden) return;

        TalkActionTimer?.Stop();
        TalkActionTimer = null;

        _idleTimer.Stop();
        _frameTimer.Stop();
        double currentTop = Top;
        double currentLeft = Left;
        BeginAnimation(TopProperty, null);
        BeginAnimation(LeftProperty, null);
        Top = currentTop;
        Left = currentLeft;
        _isAnimating = false;
        _isFalling = false;
        _loopCurrentAnimation = false;

        ShowContextMenu();
    }

    private void LoadStaticSprites()
    {
        string spritesDir = Path.Combine(_assetRoot, "sprites");
        if (!Directory.Exists(spritesDir)) return;

        foreach (var file in Directory.EnumerateFiles(spritesDir, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            _sprites[name] = SpriteLoader.LoadSingle(file, _physicalCharacterWidth * 4, _physicalCharacterHeight * 4);
        }
    }

    private static BitmapSource? RandomFrom(Dictionary<string, BitmapSource> pool, string prefix)
    {
        var candidates = new List<BitmapSource>();
        foreach (var kvp in pool)
        {
            if (kvp.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(kvp.Value);
            }
            else if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                     kvp.Key.Length > prefix.Length &&
                     char.IsDigit(kvp.Key[prefix.Length]))
            {
                candidates.Add(kvp.Value);
            }
        }
        if (candidates.Count > 0) return candidates[Random.Shared.Next(candidates.Count)];
        return pool.TryGetValue(prefix, out var sprite) ? sprite : null;
    }

    private void SetSprite(BitmapSource? sprite)
    {
        if (sprite == null) return;
        double oldHeight = Height;
        double oldWidth = Width;
        SpriteImage.Source = sprite;

        double baseScale = Math.Min((double)_physicalCharacterWidth / sprite.PixelWidth, (double)_physicalCharacterHeight / sprite.PixelHeight);
        double fitScale = Math.Min(1.0, baseScale);
        double dipScale = GetDipScale();
        double dipWidth = sprite.PixelWidth * fitScale * dipScale * ScaleRatio;
        double dipHeight = sprite.PixelHeight * fitScale * dipScale * ScaleRatio;

        SpriteImage.Width = dipWidth;
        SpriteImage.Height = dipHeight;
        _currentSpriteWidth = (int)Math.Round(dipWidth);
        _currentSpriteHeight = (int)Math.Round(dipHeight);

        UpdateWindowSizeAndLayout(oldWidth, oldHeight);
    }

    public enum SpeechBubblePlacement { Top, Bottom }
    public SpeechBubblePlacement CurrentBubblePlacement { get; private set; } = SpeechBubblePlacement.Top;

    public void UpdateBubblePlacement(double bubbleH)
    {
        bool shouldBeBottom = false;

        if (BubbleContainer.Visibility == Visibility.Visible && HasCustomText && bubbleH > 0)
        {
            double dipScale = GetDipScale();
            var screenPoint = new System.Drawing.Point((int)(Left / dipScale), (int)(Top / dipScale));
            var workingArea = System.Windows.Forms.Screen.FromPoint(screenPoint).WorkingArea;
            double topBoundDip = workingArea.Top * dipScale;

            double charHeadTop;
            if (_attachedHwnd != null && TryGetAttachedWindowBounds(out var rect))
            {
                charHeadTop = rect.Top * dipScale;
            }
            else
            {
                // If bubble was already rendered above the character, character head is at Top + bubbleH.
                // Otherwise (e.g. bubble was collapsed or was below character), window Top is character head.
                charHeadTop = (CurrentBubblePlacement == SpeechBubblePlacement.Top && BubbleContainer.IsVisible) ? (Top + bubbleH) : Top;
            }

            shouldBeBottom = (charHeadTop - topBoundDip) < (bubbleH + 10);
        }

        if (shouldBeBottom && CurrentBubblePlacement != SpeechBubblePlacement.Bottom)
        {
            CurrentBubblePlacement = SpeechBubblePlacement.Bottom;
            Grid.SetRow(SpriteImage, 0);
            Grid.SetRow(BubbleContainer, 1);
            BubblePointerUp.Visibility = Visibility.Visible;
            BubblePointerDown.Visibility = Visibility.Collapsed;
            RootGrid.VerticalAlignment = VerticalAlignment.Top;
        }
        else if (!shouldBeBottom && CurrentBubblePlacement != SpeechBubblePlacement.Top)
        {
            CurrentBubblePlacement = SpeechBubblePlacement.Top;
            Grid.SetRow(BubbleContainer, 0);
            Grid.SetRow(SpriteImage, 1);
            BubblePointerDown.Visibility = Visibility.Visible;
            BubblePointerUp.Visibility = Visibility.Collapsed;
            RootGrid.VerticalAlignment = VerticalAlignment.Bottom;
        }
    }

    private void UpdateWindowSizeAndLayout(double oldWidth = 0, double oldHeight = 0)
    {
        double bubbleW = 0;
        double bubbleH = 0;

        if (BubbleContainer.Visibility == Visibility.Visible && HasCustomText)
        {
            BubbleContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            bubbleW = BubbleContainer.DesiredSize.Width;
            bubbleH = BubbleContainer.DesiredSize.Height;
        }

        UpdateBubblePlacement(bubbleH);

        double newWidth = Math.Max(_currentSpriteWidth, bubbleW);
        double newHeight = _currentSpriteHeight + bubbleH;

        Width = newWidth;
        Height = newHeight;

        if (_attachedHwnd != null && TryGetAttachedWindowBounds(out var rect))
        {
            double scale = GetDipScale();
            double winTopDip = rect.Top * scale;
            Top = CurrentBubblePlacement == SpeechBubblePlacement.Top ? (winTopDip - newHeight) : (winTopDip - _currentSpriteHeight);
        }
        else
        {
            if (!_isFalling && oldHeight > 0)
            {
                if (CurrentBubblePlacement == SpeechBubblePlacement.Top)
                {
                    Top += (oldHeight - newHeight);
                }
            }
            if (oldWidth > 0 && Math.Abs(oldWidth - newWidth) > 0.01)
            {
                Left += (oldWidth - newWidth) / 2.0;
            }
            ClampToScreen();
        }
    }

    private void ClampToScreen()
    {
        if (IsLoaded && !_isFalling && !_isDragging)
        {
            double dipScale = GetDipScale();
            var screenPoint = new System.Drawing.Point((int)(Left / dipScale), (int)(Top / dipScale));
            var workingArea = System.Windows.Forms.Screen.FromPoint(screenPoint).WorkingArea;
            var bounds = new PetBounds(
                (int)(workingArea.Left * dipScale),
                (int)(workingArea.Top * dipScale),
                (int)(workingArea.Right * dipScale),
                (int)(workingArea.Bottom * dipScale));

            if (_attachedHwnd == null)
            {
                var clamped = BehaviorPlanner.ClampToBounds(new PetPoint((int)Left, (int)Top), bounds, (int)Width, (int)Height);
                Left = clamped.X;
                Top = clamped.Y;
            }
            else
            {
                if (Top < bounds.Top)
                {
                    Top = bounds.Top;
                }
            }
        }
    }

    public void ShowSpeechBubble(int durationMs = 3500)
    {
        if (!HasCustomText)
        {
            HideSpeechBubble();
            return;
        }

        BubbleText.Text = CurrentDialogueText;
        BubbleText.TextAlignment = DialogueAlignment;
        BubbleText.FontSize = DialogueFontSize;
        if (BubbleContainer.Visibility != Visibility.Visible)
        {
            double oldW = Width;
            double oldH = Height;
            BubbleContainer.Visibility = Visibility.Visible;
            UpdateWindowSizeAndLayout(oldW, oldH);
        }
        else
        {
            UpdateWindowSizeAndLayout(Width, Height);
        }

        if (!_alwaysShowBubble)
        {
            _bubbleTimer.Stop();
            _bubbleTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
            _bubbleTimer.Start();
        }
    }

    public void HideSpeechBubble()
    {
        _bubbleTimer.Stop();
        if (_alwaysShowBubble) return;

        if (BubbleContainer.Visibility == Visibility.Visible)
        {
            double oldW = Width;
            double oldH = Height;
            BubbleContainer.Visibility = Visibility.Collapsed;
            UpdateWindowSizeAndLayout(oldW, oldH);
        }
    }

    public void SetAlwaysShowBubble(bool always)
    {
        if (_alwaysShowBubble == always) return;
        _alwaysShowBubble = always;
        if (_alwaysShowBubble && HasCustomText)
        {
            _bubbleTimer.Stop();
            ShowSpeechBubble();
        }
        else
        {
            HideSpeechBubble();
        }
    }

    public void ToggleAlwaysShowBubble() => SetAlwaysShowBubble(!_alwaysShowBubble);

    public void SetCustomText(string text, TextAlignment alignment = TextAlignment.Center, double fontSize = 13.0)
    {
        _customText = text.Trim();
        DialogueAlignment = alignment;
        DialogueFontSize = fontSize;
        BubbleText.Text = CurrentDialogueText;
        BubbleText.TextAlignment = DialogueAlignment;
        BubbleText.FontSize = DialogueFontSize;
        if (HasCustomText)
        {
            if (BubbleContainer.Visibility == Visibility.Visible)
            {
                UpdateWindowSizeAndLayout(Width, Height);
            }
            ShowSpeechBubble(3500);
        }
        else
        {
            HideSpeechBubble();
        }
    }

    public void ResetToDefaultQuote()
    {
        _customText = CharacterQuotes.GetDefaultQuote(CharacterName);
        DialogueAlignment = TextAlignment.Center;
        DialogueFontSize = 13.0;
        BubbleText.Text = CurrentDialogueText;
        BubbleText.TextAlignment = DialogueAlignment;
        BubbleText.FontSize = DialogueFontSize;
        if (BubbleContainer.Visibility == Visibility.Visible)
        {
            UpdateWindowSizeAndLayout(Width, Height);
        }
        ShowSpeechBubble(3500);
    }

    private void OnBubbleTimerTick()
    {
        _bubbleTimer.Stop();
        if (!_alwaysShowBubble)
        {
            HideSpeechBubble();
        }
    }

    public void PlayTalkAction()
    {
        if (!HasCustomText) return;
        ShowSpeechBubble(3500);
    }

    public CharacterProfileItem ToProfileItem()
    {
        return new CharacterProfileItem
        {
            CharacterName = CharacterName,
            DialogueText = _customText,
            DialogueAlignment = DialogueAlignment.ToString(),
            DialogueFontSize = DialogueFontSize,
            AlwaysShowBubble = _alwaysShowBubble,
            ScaleRatio = ScaleRatio,
            DefaultAnimation = _defaultAnimation,
            RandomAnimationsEnabled = _randomAnimationsEnabled,
            JumpEnabled = _jumpEnabled,
            Opacity = PetOpacity,
            SyncBubbleOpacity = SyncBubbleOpacity,
            ClickThrough = _clickThrough
        };
    }

    public void ApplyProfile(CharacterProfileItem profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        SetScaleRatio(profile.ScaleRatio > 0 ? profile.ScaleRatio : 1.0);

        var alignment = TextAlignment.Center;
        if (!string.IsNullOrWhiteSpace(profile.DialogueAlignment) &&
            Enum.TryParse<TextAlignment>(profile.DialogueAlignment, true, out var parsedAlignment))
        {
            alignment = parsedAlignment;
        }

        double fontSize = profile.DialogueFontSize > 0 ? profile.DialogueFontSize : 13.0;

        if (!string.IsNullOrWhiteSpace(profile.DialogueText))
        {
            SetCustomText(profile.DialogueText, alignment, fontSize);
        }
        else
        {
            SetCustomText("", alignment, fontSize);
        }

        SetAlwaysShowBubble(profile.AlwaysShowBubble);
        SetDefaultAnimation(profile.DefaultAnimation);
        SetRandomAnimationsEnabled(profile.RandomAnimationsEnabled);
        SetJumpEnabled(profile.JumpEnabled);

        if (profile.Opacity > 0)
        {
            SetOpacity(profile.Opacity, profile.SyncBubbleOpacity);
        }
        SetClickThrough(profile.ClickThrough);
    }

    // Screen.Bounds/WorkingArea/VirtualScreen are physical pixels, but Window.Top/Left (and
    // everything BehaviorPlanner computes) are WPF DIPs. Convert using the primary screen's ratio.
    private static double GetDipScale() =>
        SystemParameters.PrimaryScreenWidth / System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width;

    // The full multi-monitor virtual desktop's horizontal extent, in DIPs.
    private static (int MinX, int MaxX) GetVirtualDesktopXBoundsInDips()
    {
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        double scale = GetDipScale();
        return ((int)(virtualScreen.Left * scale), (int)(virtualScreen.Right * scale));
    }

    // Global, tray-menu-controlled toggle
    public static bool ConfineToCurrentMonitor { get; set; } = true;

    private (int MinX, int MaxX) GetWalkJumpXBoundsInDips()
    {
        double scale = GetDipScale();
        if (_attachedHwnd is { } hwnd && TryGetAttachedWindowBounds(out var rect))
        {
            return ((int)(rect.Left * scale), (int)(rect.Right * scale));
        }

        if (!ConfineToCurrentMonitor) return GetVirtualDesktopXBoundsInDips();

        var screenPoint = new System.Drawing.Point((int)(Left / scale), (int)(Top / scale));
        var bounds = System.Windows.Forms.Screen.FromPoint(screenPoint).Bounds;
        return ((int)(bounds.Left * scale), (int)(bounds.Right * scale));
    }

    private void EnterIdleState()
    {
        TalkActionTimer?.Stop();
        TalkActionTimer = null;
        _isAnimating = false;
        _isFalling = false;
        _isWalking = false;
        _isJumping = false;

        if (_attachedHwnd is { } hwnd && TryGetAttachedWindowBounds(out var rect))
        {
            double scale = GetDipScale();
            _attachedRelativeX = Left - (rect.Left * scale);
        }

        if (!string.IsNullOrEmpty(_defaultAnimation))
        {
            var customFrames = GetOrLoadFrames(_defaultAnimation);
            if (customFrames.Count > 0)
            {
                _currentAnimationFrames = customFrames;
                _currentFrameIndex = 0;
                _loopCurrentAnimation = true;
                int fps = BehaviorPlanner.GetFps(_config, CharacterName, _defaultAnimation);
                _frameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
                _pendingOnComplete = null;
                _frameTimer.Start();

                if (_randomAnimationsEnabled && !_isShuttingDown && !_isDragging && !_isFalling && !_isInteracting && !IsPetHidden)
                {
                    StartIdleTimer();
                }
                return;
            }
        }

        // If the character has a bounce/idle animation, loop it during idle
        var idleFrames = GetOrLoadFrames("bounce");
        if (idleFrames.Count > 0)
        {
            _currentAnimationFrames = idleFrames;
            _currentFrameIndex = 0;
            _loopCurrentAnimation = true;
            int fps = BehaviorPlanner.GetFps(_config, CharacterName, "bounce");
            _frameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
            _pendingOnComplete = null;
            _frameTimer.Start();
        }
        else
        {
            _loopCurrentAnimation = false;
            _frameTimer.Stop();
            SetSprite(RandomFrom(_sprites, "spawn"));
        }

        if (_randomAnimationsEnabled && !_isShuttingDown && !_isDragging && !_isFalling && !_isInteracting && !IsPetHidden)
        {
            StartIdleTimer();
        }
    }

    private void StartIdleTimer()
    {
        _idleTimer.Stop();
        _idleTimer.Interval = TimeSpan.FromMilliseconds(BehaviorPlanner.NextIdleIntervalMs(SystemRandomSource.Shared));
        _idleTimer.Start();
    }

    private void OnIdleTick()
    {
        _idleTimer.Stop();
        if (_randomAnimationsEnabled && !_isShuttingDown && !_isAnimating && !_isDragging && !_isFalling && !_isInteracting && !IsPetHidden)
        {
            var action = BehaviorPlanner.ChooseAutonomousAction(_otherAnimationNames, SystemRandomSource.Shared, _jumpEnabled);
            switch (action.Kind)
            {
                case AutonomousActionKind.Jump:
                    PlayJump();
                    break;
                case AutonomousActionKind.Walk:
                    PlayWalk();
                    break;
                case AutonomousActionKind.Talk:
                    if (HasCustomText) PlayTalkAction();
                    if (_randomAnimationsEnabled && !_isShuttingDown && !IsPetHidden) StartIdleTimer();
                    break;
                case AutonomousActionKind.PlayAnimation:
                    PlayNamedAnimation(action.AnimationName!);
                    break;
                case AutonomousActionKind.NoOp:
                    if (_randomAnimationsEnabled && !_isShuttingDown && !IsPetHidden) StartIdleTimer();
                    break;
            }
        }
        else if (_randomAnimationsEnabled && !_isShuttingDown && !_isAnimating && !_isDragging && !_isFalling && !_isInteracting && !IsPetHidden)
        {
            StartIdleTimer();
        }
    }

    private string? _defaultAnimation;
    public string? DefaultAnimation => _defaultAnimation;
    public event Action<string?>? DefaultAnimationChanged;

    public void SetDefaultAnimation(string? animationName)
    {
        string? normalized = string.IsNullOrWhiteSpace(animationName) ? null : animationName.Trim();
        if (string.Equals(_defaultAnimation, normalized, StringComparison.OrdinalIgnoreCase)) return;

        _defaultAnimation = normalized;
        DefaultAnimationChanged?.Invoke(_defaultAnimation);

        if (!_isShuttingDown && !_isDragging && !_isFalling && !_isInteracting)
        {
            EnterIdleState();
        }
    }

    private bool _randomAnimationsEnabled = true;

    public bool RandomAnimationsEnabled => _randomAnimationsEnabled;

    public void SetRandomAnimationsEnabled(bool enabled)
    {
        if (_randomAnimationsEnabled == enabled) return;
        _randomAnimationsEnabled = enabled;
        if (_randomAnimationsEnabled)
        {
            if (!_isShuttingDown && !_isAnimating && !_isDragging && !_isFalling && !_isInteracting)
            {
                StartIdleTimer();
            }
        }
        else
        {
            _idleTimer.Stop();
        }
        RandomAnimationsEnabledChanged?.Invoke(_randomAnimationsEnabled);
    }

    public void ToggleRandomAnimations() => SetRandomAnimationsEnabled(!_randomAnimationsEnabled);

    public bool JumpEnabled => _jumpEnabled;

    public void SetJumpEnabled(bool enabled)
    {
        if (_jumpEnabled == enabled) return;
        _jumpEnabled = enabled;
        JumpEnabledChanged?.Invoke(_jumpEnabled);
    }

    public void ToggleJump() => SetJumpEnabled(!_jumpEnabled);

    private bool _isInteracting;
    public bool IsInteracting => _isInteracting;
    public bool IsReadyForInteraction => !_isInteracting && !_isDragging && !_isAnimating && !_isFalling && IsLoaded;

    public void EnterInteractionState()
    {
        _isInteracting = true;
        TalkActionTimer?.Stop();
        TalkActionTimer = null;
        _idleTimer.Stop();
        _frameTimer.Stop();
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        Hide();
    }

    public void ExitInteractionState(PetPoint reappearPos)
    {
        _isInteracting = false;
        _isFalling = false;
        _isWalking = false;
        _isJumping = false;
        _attachedHwnd = null;
        _windowTrackingTimer.Stop();

        Left = reappearPos.X;
        Show();
        EnterIdleState();

        double dipScale = GetDipScale();
        var screenPoint = new System.Drawing.Point((int)(reappearPos.X / dipScale), (int)(reappearPos.Y / dipScale));
        var screen = System.Windows.Forms.Screen.FromPoint(screenPoint);
        Top = (screen.WorkingArea.Bottom * dipScale) - Height;

        ClampToScreen();
    }

    public void HidePet()
    {
        if (IsPetHidden || _isShuttingDown) return;
        IsPetHidden = true;

        if (_isDragging)
        {
            ReleaseMouseCapture();
            _isDragging = false;
            _holdTimer.Stop();
            _isShaking = false;
            _grabbedSprite = null;
        }

        if (ContextMenu != null)
        {
            ContextMenu.IsOpen = false;
        }

        _idleTimer.Stop();
        _frameTimer.Stop();
        _windowTrackingTimer.Stop();
        _holdTimer.Stop();
        _bubbleTimer.Stop();
        TalkActionTimer?.Stop();
        TalkActionTimer = null;

        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);

        Hide();
    }

    public void ShowPet()
    {
        if (!IsPetHidden || _isShuttingDown) return;
        IsPetHidden = false;

        Show();

        if (_attachedHwnd is { } hwnd)
        {
            if (NativeMethods.IsWindow(hwnd) && NativeMethods.IsWindowVisible(hwnd) && !NativeMethods.IsIconic(hwnd) && !NativeMethods.IsZoomed(hwnd))
            {
                _windowTrackingTimer.Start();
            }
            else
            {
                _attachedHwnd = null;
            }
        }

        if (_alwaysShowBubble && HasCustomText)
        {
            ShowSpeechBubble();
        }

        EnterIdleState();
        ClampToScreen();
    }

    public void Shutdown()
    {
        _isShuttingDown = true;
        ClickThroughManager.Instance.Unregister(this);
        InteractionCoordinator.Instance.UnregisterPet(this);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            if (_clickThrough)
            {
                NativeMethods.SetWindowClickThrough(hwnd, false);
            }
            NativeMethods.RemoveProp(hwnd, "ChiikawaDesktopPet_PetType");
            NativeMethods.RemoveProp(hwnd, "ChiikawaDesktopPet_IsReady");
            NativeMethods.RemoveProp(hwnd, "ChiikawaDesktopPet_TargetX");
            NativeMethods.RemoveProp(hwnd, "ChiikawaDesktopPet_TargetY");
        }

        if (ContextMenu != null)
        {
            ContextMenu.IsOpen = false;
            ContextMenu = null;
        }
        _windowTrackingTimer.Stop();
        _attachedHwnd = null;
        _idleTimer.Stop();
        _frameTimer.Stop();
        _holdTimer.Stop();
        _bubbleTimer.Stop();
        TalkActionTimer?.Stop();
        Close();
    }
}
