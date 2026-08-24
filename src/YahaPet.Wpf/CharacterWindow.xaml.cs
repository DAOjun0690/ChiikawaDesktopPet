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
    private readonly Dictionary<string, CharacterConfig> _config;
    private List<string> _otherAnimationNames = [];
    private bool _isAnimating;
    private bool _isDragging;
    private List<BitmapSource> _currentAnimationFrames = [];
    private int _currentFrameIndex;
    private bool _loopCurrentAnimation;
    private Action? _pendingOnComplete;

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
        };
    }

    public void Spawn()
    {
        LoadStaticSprites();
        DiscoverOtherAnimations();

        double startX = SystemParameters.PrimaryScreenWidth / 2;
        Left = startX;
        Top = 0;
        SetSprite(RandomFrom(_sprites, "spawn"));
        PlayAnimationSound("spawn");
        Show();

        FallTo();
        StartIdleTimer();
    }

    private void LoadStaticSprites()
    {
        string spritesDir = Path.Combine(_assetRoot, "sprites");
        if (!Directory.Exists(spritesDir)) return;

        foreach (var file in Directory.EnumerateFiles(spritesDir, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            _sprites[name] = SpriteLoader.LoadSingle(file, _physicalCharacterWidth, _physicalCharacterHeight);
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
        SpriteImage.Source = sprite;

        double dipScale = GetDipScale();
        double dipWidth = sprite.PixelWidth * dipScale;
        double dipHeight = sprite.PixelHeight * dipScale;

        Width = dipWidth;
        Height = dipHeight;
        _currentSpriteWidth = (int)Math.Round(dipWidth);
        _currentSpriteHeight = (int)Math.Round(dipHeight);

        if (!_isFalling && oldHeight > 0)
        {
            Top += (oldHeight - dipHeight);
        }
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
        if (!ConfineToCurrentMonitor) return GetVirtualDesktopXBoundsInDips();

        double scale = GetDipScale();
        var screenPoint = new System.Drawing.Point((int)(Left / scale), (int)(Top / scale));
        var bounds = System.Windows.Forms.Screen.FromPoint(screenPoint).Bounds;
        return ((int)(bounds.Left * scale), (int)(bounds.Right * scale));
    }

    private void EnterIdleState()
    {
        _isAnimating = false;
        _isFalling = false;

        // If the character has a bounce/idle animation, loop it during idle
        var idleFrames = GetOrLoadFrames("bounce");
        if (idleFrames.Count > 0 && (CharacterName == "jokebear" || CharacterName == "loverabbit"))
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

    public void Shutdown()
    {
        _isShuttingDown = true;
        if (ContextMenu != null)
        {
            ContextMenu.IsOpen = false;
            ContextMenu = null;
        }
        _idleTimer.Stop();
        _frameTimer.Stop();
        _holdTimer.Stop();
        Close();
    }
}
