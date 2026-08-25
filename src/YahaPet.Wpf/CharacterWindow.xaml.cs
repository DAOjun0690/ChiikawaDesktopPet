// src/YahaPet.Wpf/CharacterWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YahaPet.Core;

namespace YahaPet.Wpf;

public partial class CharacterWindow : Window
{
    public string CharacterName { get; }

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

    private System.Windows.Point _dragOffset;
    private readonly DispatcherTimer _holdTimer = new() { Interval = TimeSpan.FromMilliseconds(4500) };
    private bool _isShaking;
    private BitmapSource? _grabbedSprite;

    private string _customText = "";
    public string CurrentDialogueText => string.IsNullOrEmpty(_customText) ? CharacterQuotes.GetDefaultQuote(CharacterName) : _customText;
    public TextAlignment DialogueAlignment { get; private set; } = TextAlignment.Center;
    public double DialogueFontSize { get; private set; } = 13.0;
    private bool _alwaysShowBubble;
    public bool AlwaysShowBubble => _alwaysShowBubble;
    private readonly DispatcherTimer _bubbleTimer = new();
    internal DispatcherTimer? TalkActionTimer { get; set; }

    public CharacterWindow(string characterName)
    {
        InitializeComponent();
        CharacterName = characterName.ToLowerInvariant();
        _assetRoot = Path.Combine(AppContext.BaseDirectory, "assets", CharacterName);

        _characterWidth = (int)(SystemParameters.PrimaryScreenWidth / 10);
        _characterHeight = (int)(SystemParameters.PrimaryScreenHeight / 10);
        _physicalCharacterWidth = (int)(System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width / 10);
        _physicalCharacterHeight = (int)(System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height / 10);

        _config = ConfigLoader.Load(Path.Combine(AppContext.BaseDirectory, "config.json"));

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
            SetSprite(_sprites["shaken"]);
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeToolWindow(hwnd);

            int typeId = InteractionCoordinator.GetCharacterTypeId(CharacterName);
            NativeMethods.SetProp(hwnd, "YahaPet_PetType", (IntPtr)typeId);
            NativeMethods.SetProp(hwnd, "YahaPet_IsReady", (IntPtr)1);

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            InteractionCoordinator.Instance.RegisterPet(this);
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
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
                    int targetX = (int)NativeMethods.GetProp(hwnd, "YahaPet_TargetX");
                    int targetY = (int)NativeMethods.GetProp(hwnd, "YahaPet_TargetY");
                    ExitInteractionState(new PetPoint(targetX, targetY));
                    return (IntPtr)1;

                case InteractionCoordinator.CMD_MOVE_TO:
                    handled = true;
                    int moveX = (int)NativeMethods.GetProp(hwnd, "YahaPet_TargetX");
                    int moveY = (int)NativeMethods.GetProp(hwnd, "YahaPet_TargetY");
                    SmoothMoveTo(new PetPoint(moveX, moveY), null);
                    return (IntPtr)1;
            }
        }
        return IntPtr.Zero;
    }

    public void Spawn()
    {
        LoadStaticSprites();
        DiscoverOtherAnimations();

        double startX = SystemParameters.PrimaryScreenWidth / 2;
        Left = startX;
        Top = 0;
        BubbleText.Text = CurrentDialogueText;
        BubbleText.TextAlignment = DialogueAlignment;
        BubbleText.FontSize = DialogueFontSize;
        SetSprite(RandomFrom(_sprites, "spawn"));
        Show();

        FallTo();
        StartIdleTimer();
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

    private static BitmapSource RandomFrom(Dictionary<string, BitmapSource> pool, string prefix)
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
        return candidates.Count > 0 ? candidates[Random.Shared.Next(candidates.Count)] : pool[prefix];
    }

    private void SetSprite(BitmapSource sprite)
    {
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

    private void UpdateWindowSizeAndLayout(double oldWidth = 0, double oldHeight = 0)
    {
        double bubbleW = 0;
        double bubbleH = 0;

        if (BubbleContainer.Visibility == Visibility.Visible)
        {
            BubbleContainer.Measure(new Size(240, double.PositiveInfinity));
            bubbleW = BubbleContainer.DesiredSize.Width;
            bubbleH = BubbleContainer.DesiredSize.Height;
        }

        double newWidth = Math.Max(_currentSpriteWidth, bubbleW);
        double newHeight = _currentSpriteHeight + bubbleH;

        Width = newWidth;
        Height = newHeight;

        if (!_isFalling && oldHeight > 0)
        {
            Top += (oldHeight - newHeight);
        }
        if (oldWidth > 0 && Math.Abs(oldWidth - newWidth) > 0.01)
        {
            Left += (oldWidth - newWidth) / 2.0;
        }

        ClampToScreen();
    }

    private void ClampToScreen()
    {
        if (IsLoaded && !_isFalling && !_isDragging && _attachedHwnd == null)
        {
            double dipScale = GetDipScale();
            var screenPoint = new System.Drawing.Point((int)(Left / dipScale), (int)(Top / dipScale));
            var workingArea = System.Windows.Forms.Screen.FromPoint(screenPoint).WorkingArea;
            var bounds = new PetBounds(
                (int)(workingArea.Left * dipScale),
                (int)(workingArea.Top * dipScale),
                (int)(workingArea.Right * dipScale),
                (int)(workingArea.Bottom * dipScale));
            var clamped = BehaviorPlanner.ClampToBounds(new PetPoint((int)Left, (int)Top), bounds, (int)Width, (int)Height);
            Left = clamped.X;
            Top = clamped.Y;
        }
    }

    public void ShowSpeechBubble(int durationMs = 3500)
    {
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
        if (_alwaysShowBubble)
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
        if (BubbleContainer.Visibility == Visibility.Visible)
        {
            UpdateWindowSizeAndLayout(Width, Height);
        }
        ShowSpeechBubble(3500);
    }

    public void ResetToDefaultQuote()
    {
        _customText = "";
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

    private void PlayTalkAction()
    {
        ShowSpeechBubble(3500);
        _isAnimating = true;
        TalkActionTimer?.Stop();
        TalkActionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };
        TalkActionTimer.Tick += (_, _) =>
        {
            TalkActionTimer.Stop();
            TalkActionTimer = null;
            _isAnimating = false;
            EnterIdleState();
        };
        TalkActionTimer.Start();
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

        // If the character has a bounce/idle animation, loop it during idle
        var idleFrames = GetOrLoadFrames("bounce");
        if (idleFrames.Count > 0 && (CharacterName == "jokebear" || CharacterName == "loverabbit" || CharacterName == "poro" || CharacterName == "pochita"))
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
    }

    private void StartIdleTimer()
    {
        _idleTimer.Interval = TimeSpan.FromMilliseconds(BehaviorPlanner.NextIdleIntervalMs(SystemRandomSource.Shared));
        _idleTimer.Start();
    }

    private void OnIdleTick()
    {
        _idleTimer.Stop();
        if (_randomAnimationsEnabled && !_isAnimating && !_isDragging)
        {
            var action = BehaviorPlanner.ChooseAutonomousAction(_otherAnimationNames, SystemRandomSource.Shared, _jumpEnabled);
            switch (action.Kind)
            {
                case AutonomousActionKind.Jump: PlayJump(); break;
                case AutonomousActionKind.Walk: PlayWalk(); break;
                case AutonomousActionKind.Talk: PlayTalkAction(); break;
                case AutonomousActionKind.PlayAnimation: PlayNamedAnimation(action.AnimationName!); break;
                case AutonomousActionKind.NoOp: break;
            }
        }
        if (_randomAnimationsEnabled) StartIdleTimer();
    }

    private bool _randomAnimationsEnabled = true;

    public bool RandomAnimationsEnabled => _randomAnimationsEnabled;

    public void SetRandomAnimationsEnabled(bool enabled)
    {
        if (_randomAnimationsEnabled == enabled) return;
        _randomAnimationsEnabled = enabled;
        if (_randomAnimationsEnabled) StartIdleTimer();
        else _idleTimer.Stop();
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
        if (_randomAnimationsEnabled) StartIdleTimer();
    }

    public void Shutdown()
    {
        _isShuttingDown = true;
        InteractionCoordinator.Instance.UnregisterPet(this);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.RemoveProp(hwnd, "YahaPet_PetType");
            NativeMethods.RemoveProp(hwnd, "YahaPet_IsReady");
            NativeMethods.RemoveProp(hwnd, "YahaPet_TargetX");
            NativeMethods.RemoveProp(hwnd, "YahaPet_TargetY");
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
