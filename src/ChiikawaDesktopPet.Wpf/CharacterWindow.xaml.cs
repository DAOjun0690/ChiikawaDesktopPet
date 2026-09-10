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

    public const double BaseScaleMultiplier = 1.5;
    private readonly int _characterWidth;
    private readonly int _characterHeight;
    private readonly int _physicalCharacterWidth;
    private readonly int _physicalCharacterHeight;
    private int _currentSpriteWidth;
    private int _currentSpriteHeight;
    private bool _isFalling = true;
    private bool _isUpdatingLayout;
    private readonly Dictionary<string, List<BitmapSource>> _frames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapSource> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly CharacterAssetPackage _assetPackage;

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
    public event Action? ProfileChanged;

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
    public double DialogueImageMaxWidth { get; private set; } = 260.0;
    public double DialogueImageMaxHeight { get; private set; } = 200.0;
    private bool _alwaysShowBubble;
    public bool AlwaysShowBubble => _alwaysShowBubble;
    private readonly DispatcherTimer _bubbleTimer = new();
    private bool _pendingHideBubbleAfterJump;
    private double _jumpStartingFeetY;
    internal DispatcherTimer? TalkActionTimer { get; set; }

    public CharacterWindow(string characterName, int instanceIndex = 1, string? instanceDisplayName = null)
    {
        InitializeComponent();
        CharacterName = characterName.ToLowerInvariant();
        InstanceIndex = instanceIndex;
        InstanceDisplayName = instanceDisplayName ?? $"{App.GetCharacterDisplayName(CharacterName)} {instanceIndex}";
        InstanceId = $"{CharacterName}_{InstanceIndex}";
        _assetPackage = CharacterAssetPackage.Open(CharacterName);

        _characterWidth = (int)((SystemParameters.PrimaryScreenWidth / 10.0) * BaseScaleMultiplier);
        _characterHeight = (int)((SystemParameters.PrimaryScreenHeight / 10.0) * BaseScaleMultiplier);
        var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
        _physicalCharacterWidth = (int)(((primaryScreen?.Bounds.Width ?? (int)SystemParameters.PrimaryScreenWidth) / 10.0) * BaseScaleMultiplier);
        _physicalCharacterHeight = (int)(((primaryScreen?.Bounds.Height ?? (int)SystemParameters.PrimaryScreenHeight) / 10.0) * BaseScaleMultiplier);

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
        BubbleContainer.SizeChanged += OnBubbleContainerSizeChanged;

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
        try
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

            if (msg == NativeMethods.WM_DISPLAYCHANGE || msg == NativeMethods.WM_DWMCOMPOSITIONCHANGED)
            {
                RefreshVisualSurface();
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex, "CharacterWindow.WndProc");
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
        UpdateBubbleContent();
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
        ProfileChanged?.Invoke();
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
        var loaded = _assetPackage.LoadStaticSprites(_physicalCharacterWidth * 4, _physicalCharacterHeight * 4);
        foreach (var (name, sprite) in loaded)
        {
            _sprites[name] = sprite;
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
        double fitScale = Math.Min(BaseScaleMultiplier, baseScale);
        double dipScale = GetDipScale();
        double dipWidth = sprite.PixelWidth * fitScale * dipScale * ScaleRatio;
        double dipHeight = sprite.PixelHeight * fitScale * dipScale * ScaleRatio;

        _currentSpriteWidth = (int)Math.Round(dipWidth);
        _currentSpriteHeight = (int)Math.Round(dipHeight);
        SpriteImage.Width = _currentSpriteWidth;
        SpriteImage.Height = _currentSpriteHeight;

        UpdateWindowSizeAndLayout(oldWidth, oldHeight);
    }

    public void RefreshVisualSurface()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshVisualSurface);
            return;
        }

        InvalidateVisual();
        SpriteImage.InvalidateVisual();
        UpdateLayout();

        if (SpriteImage.Source is BitmapSource bs)
        {
            SpriteImage.Source = null;
            SpriteImage.Source = bs;
        }
    }

    public enum SpeechBubblePlacement { Top, Bottom }
    public SpeechBubblePlacement CurrentBubblePlacement { get; private set; } = SpeechBubblePlacement.Top;

    public double UpdateBubblePlacement(double bubbleH, double? explicitCharHeadTop = null)
    {
        bool shouldBeBottom = false;

        if (BubbleContainer.Visibility == Visibility.Visible && HasCustomText && bubbleH > 0)
        {
            double dipScale = GetDipScale();
            double charHeadTop;

            if (explicitCharHeadTop.HasValue)
            {
                charHeadTop = explicitCharHeadTop.Value;
            }
            else if (_attachedHwnd != null && TryGetAttachedWindowBounds(out var rect))
            {
                charHeadTop = rect.Top * dipScale - _currentSpriteHeight;
            }
            else
            {
                if (double.IsNaN(Top))
                {
                    return 0;
                }

                // When bubble is above character, character sits at the bottom of the window.
                // Its head is at Top + (Height - _currentSpriteHeight).
                charHeadTop = (CurrentBubblePlacement == SpeechBubblePlacement.Top && BubbleContainer.IsVisible && Height > _currentSpriteHeight)
                    ? (Top + (Height - _currentSpriteHeight))
                    : Top;
            }

            if (double.IsNaN(charHeadTop))
            {
                return 0;
            }

            double currentLeft = double.IsNaN(Left) ? 0 : Left;
            var screenPoint = new System.Drawing.Point((int)(currentLeft / dipScale), (int)(charHeadTop / dipScale));
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            var screen = (primary != null) ? System.Windows.Forms.Screen.FromPoint(screenPoint) : null;
            var workingArea = screen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);

            double topBoundDip = workingArea.Top * dipScale;
            double bottomBoundDip = workingArea.Bottom * dipScale;

            double spaceAbove = charHeadTop - topBoundDip;
            double spaceBelow = bottomBoundDip - (charHeadTop + _currentSpriteHeight);

            const double Margin = 10.0;
            bool canFitAbove = spaceAbove >= (bubbleH + Margin);
            bool canFitBelow = spaceBelow >= (bubbleH + Margin);

            if (CurrentBubblePlacement == SpeechBubblePlacement.Top)
            {
                // Currently Top. Only flip to Bottom if bubble cannot fit above
                // AND there is room below (or more room below than above).
                shouldBeBottom = !canFitAbove && (canFitBelow || spaceBelow > spaceAbove);
            }
            else
            {
                // Currently Bottom.
                // Maintain Bottom when the bubble is resting on the taskbar/ground (spaceBelow >= bubbleH - Margin).
                // Only flip to Top if the user drags the character body itself down past the resting bubble
                // (spaceBelow < bubbleH - Margin) AND there is sufficient space above (or more space above).
                bool bubbleTouchingOrFitsBelow = spaceBelow >= (bubbleH - Margin);
                if (bubbleTouchingOrFitsBelow)
                {
                    if (spaceBelow > (bubbleH + Margin) && spaceAbove > spaceBelow && canFitAbove)
                    {
                        shouldBeBottom = false; // flip back to Top in mid-air
                    }
                    else
                    {
                        shouldBeBottom = true; // stay Bottom
                    }
                }
                else
                {
                    shouldBeBottom = !canFitAbove && spaceBelow > spaceAbove;
                }
            }
        }

        double deltaY = 0;
        if (shouldBeBottom && CurrentBubblePlacement != SpeechBubblePlacement.Bottom)
        {
            CurrentBubblePlacement = SpeechBubblePlacement.Bottom;
            Grid.SetRow(SpriteImage, 0);
            Grid.SetRow(BubbleContainer, 1);
            BubblePointerUp.Visibility = Visibility.Visible;
            BubblePointerDown.Visibility = Visibility.Collapsed;
            RootGrid.VerticalAlignment = VerticalAlignment.Top;
            deltaY = bubbleH;
        }
        else if (!shouldBeBottom && CurrentBubblePlacement != SpeechBubblePlacement.Top)
        {
            CurrentBubblePlacement = SpeechBubblePlacement.Top;
            Grid.SetRow(BubbleContainer, 0);
            Grid.SetRow(SpriteImage, 1);
            BubblePointerDown.Visibility = Visibility.Visible;
            BubblePointerUp.Visibility = Visibility.Collapsed;
            RootGrid.VerticalAlignment = VerticalAlignment.Bottom;
            deltaY = -bubbleH;
        }

        return deltaY;
    }

    private void UpdateWindowSizeAndLayout(double oldWidth = 0, double oldHeight = 0)
    {
        if (_isUpdatingLayout) return;
        _isUpdatingLayout = true;
        try
        {
            double currentTop = double.IsNaN(Top) ? 0 : Top;
            double currentLeft = double.IsNaN(Left) ? 0 : Left;
            double currentHeight = (double.IsNaN(Height) || Height <= 0) ? _currentSpriteHeight : Height;
            double currentWidth = (double.IsNaN(Width) || Width <= 0) ? _currentSpriteWidth : Width;

            if (oldWidth <= 0 || double.IsNaN(oldWidth)) oldWidth = currentWidth;
            if (oldHeight <= 0 || double.IsNaN(oldHeight)) oldHeight = currentHeight;

            double bubbleW = 0;
            double bubbleH = 0;

            var previousPlacement = CurrentBubblePlacement;
            double dipScale = GetDipScale();
            double oldFeet;
            double charHeadTop;

            if (_attachedHwnd != null && TryGetAttachedWindowBounds(out var attachedBounds))
            {
                double winTopDip = attachedBounds.Top * dipScale;
                oldFeet = winTopDip;
                charHeadTop = winTopDip - _currentSpriteHeight;
            }
            else
            {
                bool wasBubbleAbove = (previousPlacement == SpeechBubblePlacement.Top && oldHeight > _currentSpriteHeight + 1);
                oldFeet = wasBubbleAbove ? (currentTop + oldHeight) : (currentTop + _currentSpriteHeight);
                charHeadTop = double.IsNaN(Top) ? double.NaN : (oldFeet - _currentSpriteHeight);
            }

            var screenPoint = new System.Drawing.Point((int)(currentLeft / dipScale), (int)((double.IsNaN(charHeadTop) ? 0 : charHeadTop) / dipScale));
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            var screen = (primary != null) ? System.Windows.Forms.Screen.FromPoint(screenPoint) : null;
            var workingArea = screen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);

            double topBoundDip = workingArea.Top * dipScale;
            double bottomBoundDip = workingArea.Bottom * dipScale;
            double spaceAbove = double.IsNaN(charHeadTop) ? 450.0 : Math.Max(0.0, charHeadTop - topBoundDip);
            double spaceBelow = double.IsNaN(charHeadTop) ? 450.0 : Math.Max(0.0, bottomBoundDip - (charHeadTop + _currentSpriteHeight));

            const double Margin = 10.0;
            const double Overhead = 45.0; // Margin (20) + Border padding & stroke (~18) + Pointer (~7)

            if (BubbleContainer.Visibility == Visibility.Visible && HasCustomText)
            {
                // Determine if bubble should be placed at the bottom or top
                bool isBottom = double.IsNaN(Top)
                    ? (previousPlacement == SpeechBubblePlacement.Bottom)
                    : ((previousPlacement == SpeechBubblePlacement.Bottom)
                        ? (spaceBelow >= (60.0 - Margin) && (spaceAbove <= spaceBelow || spaceAbove < (80.0 + Margin)))
                        : (spaceAbove < 80.0 && spaceBelow > spaceAbove));

                double availableSpace = isBottom ? spaceBelow : spaceAbove;
                if (double.IsNaN(availableSpace) || double.IsInfinity(availableSpace) || availableSpace <= 0)
                {
                    availableSpace = 450.0;
                }
                double maxAllowedBubbleH = Math.Max(60.0, availableSpace - 15.0);
                double maxViewerH = Math.Min(450.0, Math.Max(30.0, maxAllowedBubbleH - Overhead));
                if (double.IsNaN(maxViewerH) || maxViewerH <= 0)
                {
                    maxViewerH = 450.0;
                }

                if (BubbleScrollViewer != null)
                {
                    BubbleScrollViewer.MaxHeight = maxViewerH;
                }

                double maxContainerWidth = Math.Max(BubbleBorder.MaxWidth + 20, _currentSpriteWidth);
                BubbleContainer.Measure(new Size(maxContainerWidth, double.PositiveInfinity));
                bubbleW = Math.Max(BubbleContainer.DesiredSize.Width, BubbleContainer.ActualWidth);
                bubbleH = Math.Max(BubbleContainer.DesiredSize.Height, BubbleContainer.ActualHeight);

                UpdateBubblePlacement(bubbleH, double.IsNaN(charHeadTop) ? null : charHeadTop);

                // If placement flipped after measure, re-constrain to the actual placement
                if (CurrentBubblePlacement != (isBottom ? SpeechBubblePlacement.Bottom : SpeechBubblePlacement.Top))
                {
                    availableSpace = CurrentBubblePlacement == SpeechBubblePlacement.Bottom ? spaceBelow : spaceAbove;
                    if (double.IsNaN(availableSpace) || double.IsInfinity(availableSpace) || availableSpace <= 0)
                    {
                        availableSpace = 450.0;
                    }
                    maxAllowedBubbleH = Math.Max(60.0, availableSpace - 15.0);
                    maxViewerH = Math.Min(450.0, Math.Max(30.0, maxAllowedBubbleH - Overhead));
                    if (double.IsNaN(maxViewerH) || maxViewerH <= 0)
                    {
                        maxViewerH = 450.0;
                    }
                    if (BubbleScrollViewer != null)
                    {
                        BubbleScrollViewer.MaxHeight = maxViewerH;
                    }
                    BubbleContainer.Measure(new Size(maxContainerWidth, double.PositiveInfinity));
                    bubbleW = Math.Max(BubbleContainer.DesiredSize.Width, BubbleContainer.ActualWidth);
                    bubbleH = Math.Max(BubbleContainer.DesiredSize.Height, BubbleContainer.ActualHeight);
                }
            }
            else
            {
                if (BubbleScrollViewer != null)
                {
                    BubbleScrollViewer.MaxHeight = 450.0;
                }
                UpdateBubblePlacement(0);
            }

            double newWidth = Math.Ceiling(Math.Max(_currentSpriteWidth, bubbleW));
            double newHeight = Math.Ceiling(_currentSpriteHeight + bubbleH);

            if (_attachedHwnd != null && TryGetAttachedWindowBounds(out var attachedRect))
            {
                double scale = GetDipScale();
                double winTopDip = attachedRect.Top * scale;
                Top = CurrentBubblePlacement == SpeechBubblePlacement.Top ? (winTopDip - newHeight) : (winTopDip - _currentSpriteHeight);
            }
            else
            {
                if (!double.IsNaN(Top) && !_isFalling && oldHeight > 0)
                {
                    if (CurrentBubblePlacement == SpeechBubblePlacement.Top)
                    {
                        double maxPossibleHeight = Math.Max(_currentSpriteHeight, oldFeet - topBoundDip);
                        if (newHeight > maxPossibleHeight)
                        {
                            newHeight = maxPossibleHeight;
                            bubbleH = Math.Max(0.0, newHeight - _currentSpriteHeight);
                            if (BubbleScrollViewer != null)
                            {
                                BubbleScrollViewer.MaxHeight = Math.Max(30.0, bubbleH - Overhead);
                            }
                        }
                        Top = oldFeet - newHeight;
                    }
                    else
                    {
                        double maxPossibleHeight = Math.Max(_currentSpriteHeight, bottomBoundDip - (oldFeet - _currentSpriteHeight));
                        if (newHeight > maxPossibleHeight)
                        {
                            newHeight = maxPossibleHeight;
                            bubbleH = Math.Max(0.0, newHeight - _currentSpriteHeight);
                            if (BubbleScrollViewer != null)
                            {
                                BubbleScrollViewer.MaxHeight = Math.Max(30.0, bubbleH - Overhead);
                            }
                        }
                        Top = oldFeet - _currentSpriteHeight;
                    }
                }

                if (!double.IsNaN(Left) && oldWidth > 0 && Math.Abs(oldWidth - newWidth) > 0.01)
                {
                    Left += (oldWidth - newWidth) / 2.0;
                }
            }

            Width = newWidth;
            Height = newHeight;
            ClampToScreen();
        }
        finally
        {
            _isUpdatingLayout = false;
        }
    }

    private void ClampToScreen()
    {
        if (IsPetHidden || _isShuttingDown) return;
        if (double.IsNaN(Left) || double.IsNaN(Top)) return;
        if (IsLoaded && !_isFalling && !_isDragging)
        {
            double dipScale = GetDipScale();
            var screenPoint = new System.Drawing.Point((int)(Left / dipScale), (int)(Top / dipScale));
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            var screen = (primary != null) ? System.Windows.Forms.Screen.FromPoint(screenPoint) : null;
            var workingArea = screen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
            var bounds = new PetBounds(
                (int)(workingArea.Left * dipScale),
                (int)(workingArea.Top * dipScale),
                (int)(workingArea.Right * dipScale),
                (int)(workingArea.Bottom * dipScale));

            if (_attachedHwnd == null)
            {
                var clamped = BehaviorPlanner.ClampToBounds(new PetPoint((int)Math.Round(Left), (int)Math.Round(Top)), bounds, (int)Math.Ceiling(Width), (int)Math.Ceiling(Height));
                if (Left < bounds.Left || Left + Width > bounds.Right + 5)
                {
                    Left = clamped.X;
                }
                if (Top < bounds.Top || Top + Height > bounds.Bottom + 1)
                {
                    Top = clamped.Y;
                }
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

    public void ShowSpeechBubble(int durationMs = 3500, bool forceRefreshContent = false)
    {
        _pendingHideBubbleAfterJump = false;
        if (!HasCustomText)
        {
            HideSpeechBubble();
            return;
        }

        if (BubbleContainer.Visibility == Visibility.Visible && !forceRefreshContent)
        {
            // 對話框已在顯示中，只需刷新計時器，避免重複清空/解析 Markdown 導致高度塌陷與視窗震盪
            if (!_alwaysShowBubble)
            {
                int effectiveDuration = durationMs;
                if (durationMs == 3500 && MarkdownBubbleRenderer.ShouldExtendDisplayDuration(CurrentDialogueText))
                {
                    effectiveDuration = 6000;
                }

                _bubbleTimer.Stop();
                _bubbleTimer.Interval = TimeSpan.FromMilliseconds(effectiveDuration);
                _bubbleTimer.Start();
            }
            return;
        }

        UpdateBubbleContent();
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
            int effectiveDuration = durationMs;
            if (durationMs == 3500 && MarkdownBubbleRenderer.ShouldExtendDisplayDuration(CurrentDialogueText))
            {
                effectiveDuration = 6000;
            }

            _bubbleTimer.Stop();
            _bubbleTimer.Interval = TimeSpan.FromMilliseconds(effectiveDuration);
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
            var oldPlacement = CurrentBubblePlacement;
            BubbleContainer.Visibility = Visibility.Collapsed;
            UpdateWindowSizeAndLayout(oldW, oldH);

            if (oldPlacement == SpeechBubblePlacement.Bottom && _attachedHwnd == null && !_isFalling && !_isDragging)
            {
                FallTo();
            }
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
        ProfileChanged?.Invoke();
    }

    public void ToggleAlwaysShowBubble() => SetAlwaysShowBubble(!_alwaysShowBubble);

    public void SetCustomText(
        string text,
        TextAlignment alignment = TextAlignment.Center,
        double fontSize = 13.0,
        double imageMaxWidth = 260.0,
        double imageMaxHeight = 200.0)
    {
        _customText = text.Trim();
        DialogueAlignment = alignment;
        DialogueFontSize = fontSize;
        DialogueImageMaxWidth = imageMaxWidth > 0 ? imageMaxWidth : 260.0;
        DialogueImageMaxHeight = imageMaxHeight > 0 ? imageMaxHeight : 200.0;
        UpdateBubbleContent();
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
        ProfileChanged?.Invoke();
    }

    public void ResetToDefaultQuote()
    {
        _customText = CharacterQuotes.GetDefaultQuote(CharacterName);
        DialogueAlignment = TextAlignment.Center;
        DialogueFontSize = 13.0;
        DialogueImageMaxWidth = 260.0;
        DialogueImageMaxHeight = 200.0;
        UpdateBubbleContent();
        if (BubbleContainer.Visibility == Visibility.Visible)
        {
            UpdateWindowSizeAndLayout(Width, Height);
        }
        ShowSpeechBubble(3500);
        ProfileChanged?.Invoke();
    }

    private void OnBubbleContainerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isUpdatingLayout) return;
        if (BubbleContainer.Visibility != Visibility.Visible || !HasCustomText) return;
        if (!e.HeightChanged && !e.WidthChanged) return;

        double currentBubbleH = Math.Max(BubbleContainer.ActualHeight, BubbleContainer.DesiredSize.Height);
        double currentBubbleW = Math.Max(BubbleContainer.ActualWidth, BubbleContainer.DesiredSize.Width);

        double expectedHeight = Math.Ceiling(_currentSpriteHeight + currentBubbleH);
        double expectedWidth = Math.Ceiling(Math.Max(_currentSpriteWidth, currentBubbleW));

        if (Math.Abs(Height - expectedHeight) > 0.5 || Math.Abs(Width - expectedWidth) > 0.5)
        {
            UpdateWindowSizeAndLayout(Width, Height);
        }
    }

    private void UpdateBubbleContent()
    {
        BubbleBorder.MaxWidth = Math.Max(320, DialogueImageMaxWidth + 40);
        BubbleScrollViewer?.ScrollToTop();
        MarkdownBubbleRenderer.Render(
            BubbleText,
            CurrentDialogueText,
            DialogueFontSize,
            DialogueAlignment,
            DialogueImageMaxWidth,
            DialogueImageMaxHeight,
            onImageLoaded: OnBubbleImageLoaded,
            onCheckboxToggled: OnBubbleCheckboxToggled);
    }

    private void OnBubbleCheckboxToggled(int index)
    {
        string newText = MarkdownBubbleRenderer.ToggleCheckboxAt(_customText, index);
        if (newText != _customText)
        {
            _customText = newText;
            UpdateBubbleContent();
        }
    }

    private void OnBubbleImageLoaded()
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (BubbleContainer.Visibility == Visibility.Visible && HasCustomText)
            {
                UpdateWindowSizeAndLayout(Width, Height);
                BubbleScrollViewer?.ScrollToTop();
            }
        });
    }

    private void OnBubbleTimerTick()
    {
        _bubbleTimer.Stop();
        if (_isJumping)
        {
            _pendingHideBubbleAfterJump = true;
            return;
        }
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
            DialogueImageMaxWidth = DialogueImageMaxWidth,
            DialogueImageMaxHeight = DialogueImageMaxHeight,
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
        double imageMaxWidth = profile.DialogueImageMaxWidth > 0 ? profile.DialogueImageMaxWidth : 260.0;
        double imageMaxHeight = profile.DialogueImageMaxHeight > 0 ? profile.DialogueImageMaxHeight : 200.0;

        if (!string.IsNullOrWhiteSpace(profile.DialogueText))
        {
            SetCustomText(profile.DialogueText, alignment, fontSize, imageMaxWidth, imageMaxHeight);
        }
        else
        {
            SetCustomText("", alignment, fontSize, imageMaxWidth, imageMaxHeight);
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
    internal static double GetDipScale()
    {
        var primary = System.Windows.Forms.Screen.PrimaryScreen;
        if (primary == null || primary.Bounds.Width <= 0) return 1.0;
        return SystemParameters.PrimaryScreenWidth / primary.Bounds.Width;
    }

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
        var primary = System.Windows.Forms.Screen.PrimaryScreen;
        var screen = (primary != null) ? System.Windows.Forms.Screen.FromPoint(screenPoint) : null;
        var bounds = screen?.Bounds ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
        return ((int)(bounds.Left * scale), (int)(bounds.Right * scale));
    }

    private void EnterIdleState()
    {
        if (IsPetHidden || _isShuttingDown) return;

        TalkActionTimer?.Stop();
        TalkActionTimer = null;
        BeginAnimation(TopProperty, null);
        BeginAnimation(LeftProperty, null);
        _isAnimating = false;
        _isFalling = false;
        _isWalking = false;
        _isJumping = false;

        if (_pendingHideBubbleAfterJump)
        {
            _pendingHideBubbleAfterJump = false;
            if (!_alwaysShowBubble)
            {
                HideSpeechBubble();
            }
        }

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
        _pendingHideBubbleAfterJump = false;
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
        var primary = System.Windows.Forms.Screen.PrimaryScreen;
        var screen = (primary != null) ? System.Windows.Forms.Screen.FromPoint(screenPoint) : null;
        var bottom = screen?.WorkingArea.Bottom ?? (int)SystemParameters.PrimaryScreenHeight;
        Top = (bottom * dipScale) - Height;

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
        _pendingHideBubbleAfterJump = false;
        TalkActionTimer?.Stop();
        TalkActionTimer = null;
        TimedAnimationTimer?.Stop();
        TimedAnimationTimer = null;

        _isAnimating = false;
        _isWalking = false;
        _isJumping = false;
        _isFalling = false;
        _loopCurrentAnimation = false;

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

    protected override void OnClosed(EventArgs e)
    {
        ClickThroughManager.Instance.Unregister(this);
        InteractionCoordinator.Instance.UnregisterPet(this);
        _assetPackage.Dispose();
        base.OnClosed(e);
    }
}
