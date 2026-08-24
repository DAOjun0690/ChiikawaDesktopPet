// src/YahaPet.Wpf/CharacterWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
    private readonly Random _spriteRandom = new();

    private readonly DispatcherTimer _idleTimer = new();
    private readonly DispatcherTimer _frameTimer = new();
    private readonly Dictionary<string, CharacterConfig> _config;
    private List<string> _otherAnimationNames = new();
    private bool _isAnimating;
    private bool _isDragging;
    private List<BitmapSource> _currentAnimationFrames = new();
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
    private readonly Random _dragRandom = new();
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

        // config is loaded once per app; for this slice, load it directly here for simplicity
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
        foreach (var file in Directory.GetFiles(spritesDir, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            _sprites[name] = SpriteLoader.LoadSingle(file, _physicalCharacterWidth, _physicalCharacterHeight);
        }
    }

    private BitmapSource RandomFrom(Dictionary<string, BitmapSource> pool, string prefix)
    {
        var candidates = new List<BitmapSource>();
        foreach (var kvp in pool)
        {
            if (kvp.Key == prefix) candidates.Add(kvp.Value);
            else if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                     kvp.Key.Length > prefix.Length &&
                     char.IsDigit(kvp.Key[prefix.Length]))
            {
                candidates.Add(kvp.Value);
            }
        }
        return candidates.Count > 0 ? candidates[_spriteRandom.Next(candidates.Count)] : pool[prefix];
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
    // everything BehaviorPlanner computes) are WPF DIPs. This app has no DPI-awareness
    // manifest, so WPF sees a virtualized/scaled view of the screen while WinForms (once an
    // Icon/NotifyIcon exists, as App.xaml.cs's tray icon does) reports true physical pixels.
    // Convert using the primary screen's own known DIP/physical ratio.
    private static double GetDipScale() =>
        SystemParameters.PrimaryScreenWidth / System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width;

    // The full multi-monitor virtual desktop's horizontal extent, in DIPs. Walk/jump used to
    // be bounded by [0, PrimaryScreenWidth], which stranded the character on whichever
    // monitor it was already on: a secondary monitor to the left spans negative X, which a
    // hardcoded 0 lower bound can never reach, and a secondary monitor to the right is past
    // PrimaryScreenWidth, which a hardcoded upper bound can never reach either.
    private static (int MinX, int MaxX) GetVirtualDesktopXBoundsInDips()
    {
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        double scale = GetDipScale();
        return ((int)(virtualScreen.Left * scale), (int)(virtualScreen.Right * scale));
    }

    // Global, tray-menu-controlled toggle (see App.xaml.cs): when true (the default),
    // autonomous walk/jump stay on whichever monitor the character is currently on --
    // dragging it to another monitor confines its own roaming to that monitor too. When
    // false, walk/jump can freely cross the whole multi-monitor virtual desktop.
    public static bool ConfineToCurrentMonitor { get; set; } = true;

    private (int MinX, int MaxX) GetWalkJumpXBoundsInDips()
    {
        if (!ConfineToCurrentMonitor) return GetVirtualDesktopXBoundsInDips();

        double scale = GetDipScale();
        var screenPoint = new System.Drawing.Point((int)(Left / scale), (int)(Top / scale));
        var bounds = System.Windows.Forms.Screen.FromPoint(screenPoint).Bounds;
        return ((int)(bounds.Left * scale), (int)(bounds.Right * scale));
    }

    private void FallTo()
    {
        _isAnimating = true;
        _isFalling = true;

        var currentPos = new PetPoint((int)Left, (int)Top);

        // SystemParameters.PrimaryScreenHeight/WorkArea always reflect the PRIMARY monitor.
        // Determine the monitor the window is actually on instead (same approach as the
        // drag clamp in OnMouseMove), so a fall computed after a drag-release onto a
        // secondary (possibly taller) monitor doesn't compute a negative duration -- which
        // throws ArgumentException when converted to a Duration and crashes the process --
        // or a landing point off that monitor.
        double dipScale = GetDipScale();
        var screenPoint = new System.Drawing.Point((int)(Left / dipScale), (int)(Top / dipScale));
        var screen = System.Windows.Forms.Screen.FromPoint(screenPoint);

        int screenHeight = (int)(screen.Bounds.Bottom * dipScale);
        int landingY = (int)(screen.WorkingArea.Bottom * dipScale);

        var outcome = BehaviorPlanner.PlanFall(
            currentPos,
            screenHeight: screenHeight,
            landingY: landingY,
            characterHeight: _currentSpriteHeight,
            new SystemRandomSource());

        SetSprite(RandomFrom(_sprites, "falling"));
        AnimatePosition(new PetPoint((int)Left, outcome.LandingPoint.Y), outcome.DurationMs, onComplete: () =>
        {
            _isFalling = false;
            if (outcome.Crashed)
            {
                _isAnimating = false;
                SetSprite(_sprites["fallingend"]);
            }
            else
            {
                EnterIdleState();
            }
        });
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

    private void AnimatePosition(PetPoint target, int durationMs, Action? onComplete)
    {
        var animation = new System.Windows.Media.Animation.DoubleAnimation(Top, target.Y, TimeSpan.FromMilliseconds(durationMs));
        animation.Completed += (_, _) =>
        {
            // Release the animation's hold on TopProperty before reassigning, otherwise
            // WPF's property-value precedence keeps the animation in control and the
            // reassignment (and any later manual assignment, e.g. from OnMouseMove) is a
            // silent no-op. See final-review Finding 1.
            BeginAnimation(TopProperty, null);
            Top = target.Y;
            onComplete?.Invoke();
        };
        BeginAnimation(TopProperty, animation);
        Left = target.X;
    }

    private void DiscoverOtherAnimations()
    {
        string animationsDir = Path.Combine(_assetRoot, "animations");
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "walkleft", "walkright", "jumpleft", "jumpright", "falling", "bounce" };
        _otherAnimationNames = Directory.Exists(animationsDir)
            ? Directory.GetDirectories(animationsDir)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n) && !excluded.Contains(n!))
                .Select(n => n!)
                .ToList()
            : new List<string>();
    }

    private void StartIdleTimer()
    {
        _idleTimer.Interval = TimeSpan.FromMilliseconds(BehaviorPlanner.NextIdleIntervalMs(new SystemRandomSource()));
        _idleTimer.Start();
    }

    private void OnIdleTick()
    {
        _idleTimer.Stop();
        if (_randomAnimationsEnabled && !_isAnimating && !_isDragging)
        {
            var action = BehaviorPlanner.ChooseAutonomousAction(_otherAnimationNames, new SystemRandomSource(), _jumpEnabled);
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

    public IReadOnlyList<string> AllAnimationNames()
    {
        var names = new List<string>(_otherAnimationNames) { "walkleft", "walkright" };
        if (Directory.Exists(Path.Combine(_assetRoot, "animations", "bounce")))
        {
            names.Insert(0, "bounce");
        }
        if (HasDirectionalCapability("jumpleft", "jumpright"))
        {
            names.Add("jumpleft");
            names.Add("jumpright");
        }
        return names;
    }

    // True if this character can actually play both directions of a left/right pair --
    // either direction's animation FOLDER existing is enough (GetOrLoadFrames mirrors the
    // missing side), but a static SPRITE fallback (PlayJump's else-branch, which has no
    // mirroring) needs BOTH sprites, or the missing direction would throw
    // KeyNotFoundException the first time it's rolled. Chiikawa only has
    // animations/jumpright (no jumpleft folder or sprite) -- checking just "jumpleft"
    // would hide jump from the menu even though it works via mirroring.
    private bool HasDirectionalCapability(string leftName, string rightName)
    {
        bool hasEitherFolder = Directory.Exists(Path.Combine(_assetRoot, "animations", leftName)) ||
                               Directory.Exists(Path.Combine(_assetRoot, "animations", rightName));
        bool hasBothSprites = File.Exists(Path.Combine(_assetRoot, "sprites", $"{leftName}.png")) &&
                              File.Exists(Path.Combine(_assetRoot, "sprites", $"{rightName}.png"));
        return hasEitherFolder || hasBothSprites;
    }

    public void PlayAnimationByName(string animationName)
    {
        if (_isDragging) return;
        if (animationName.Equals("jumpleft", StringComparison.OrdinalIgnoreCase)) { PlayJump(BehaviorPlanner.JumpDirection.Left); return; }
        if (animationName.Equals("jumpright", StringComparison.OrdinalIgnoreCase)) { PlayJump(BehaviorPlanner.JumpDirection.Right); return; }
        if (animationName.Equals("walkleft", StringComparison.OrdinalIgnoreCase)) { PlayWalk(BehaviorPlanner.WalkDirection.Left); return; }
        if (animationName.Equals("walkright", StringComparison.OrdinalIgnoreCase)) { PlayWalk(BehaviorPlanner.WalkDirection.Right); return; }
        if (animationName.Equals("bounce", StringComparison.OrdinalIgnoreCase))
        {
            PlayTimedAnimation(animationName, 5000);
            return;
        }
        PlayNamedAnimation(animationName);
    }

    private void PlayTimedAnimation(string animationName, int durationMs)
    {
        _isAnimating = true;
        _loopCurrentAnimation = true;
        PlayFrameSequence(animationName, onComplete: () => { });

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            EnterIdleState();
        };
        timer.Start();
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

    // Only "walkleft"/"walkright"/"jumpleft"/"jumpright"-shaped names are mirrorable.
    // Requires an actual "left"/"right" suffix (not just a "walk"/"jump" prefix), so a
    // hypothetical future animation like "jumpscare" or the bare name "jump" doesn't
    // compute a bogus/out-of-range mirror name.
    private static bool TryGetMirroredAnimationName(string animationName, out string mirroredName)
    {
        bool isWalkOrJump = animationName.StartsWith("walk", StringComparison.OrdinalIgnoreCase) ||
                           animationName.StartsWith("jump", StringComparison.OrdinalIgnoreCase);
        if (isWalkOrJump && animationName.Length > 4 && animationName.EndsWith("left", StringComparison.OrdinalIgnoreCase))
        {
            mirroredName = animationName[..^4] + "right";
            return true;
        }
        if (isWalkOrJump && animationName.Length > 5 && animationName.EndsWith("right", StringComparison.OrdinalIgnoreCase))
        {
            mirroredName = animationName[..^5] + "left";
            return true;
        }
        mirroredName = "";
        return false;
    }

    private List<BitmapSource> GetOrLoadFrames(string animationName)
    {
        if (_frames.TryGetValue(animationName, out var cached)) return cached;

        bool isMirrorable = TryGetMirroredAnimationName(animationName, out string mirroredName);

        // If this direction's own folder is missing, check the OPPOSITE direction before
        // giving up -- e.g. Chiikawa only has animations/jumpright, no jumpleft, and even a
        // request for "walkright" before "walkleft" was ever loaded would hit this for
        // Hachiware. Mirroring must work regardless of which direction is requested first.
        string ownFolder = Path.Combine(_assetRoot, "animations", animationName);
        bool ownExists = Directory.Exists(ownFolder);
        string? sourceFolder = ownExists ? ownFolder
            : isMirrorable ? Path.Combine(_assetRoot, "animations", mirroredName)
            : null;

        if (sourceFolder is null || !Directory.Exists(sourceFolder))
        {
            _frames[animationName] = new List<BitmapSource>();
            return _frames[animationName];
        }

        var sourceFrames = SpriteLoader.LoadFrames(sourceFolder, _physicalCharacterWidth, _physicalCharacterHeight);
        var result = ownExists ? sourceFrames : sourceFrames.ConvertAll(SpriteLoader.Mirror);
        _frames[animationName] = result;

        // Populating both directions from a single decode -- whichever direction is
        // requested first, the other is cached alongside it, so it's never re-decoded.
        if (isMirrorable)
            _frames[mirroredName] = ownExists ? sourceFrames.ConvertAll(SpriteLoader.Mirror) : sourceFrames;

        return result;
    }

    private void PlayFrameSequence(string animationName, Action onComplete)
    {
        _currentAnimationFrames = GetOrLoadFrames(animationName);
        _currentFrameIndex = 0;
        if (_currentAnimationFrames.Count == 0) { onComplete(); return; }

        int fps = BehaviorPlanner.GetFps(_config, CharacterName, animationName);
        _frameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
        _isAnimating = true;
        _pendingOnComplete = onComplete;
        PlayAnimationSound(animationName);
        _frameTimer.Start();
    }

    private void PlayAnimationSound(string animationName) =>
        SoundPlayerFactory.PlayIfExists(Path.Combine(_assetRoot, "sounds", $"{animationName}.wav"));

    private void OnFrameTick()
    {
        if (_currentFrameIndex >= _currentAnimationFrames.Count)
        {
            if (_loopCurrentAnimation)
            {
                _currentFrameIndex = 0;
                return;
            }
            _frameTimer.Stop();
            _isAnimating = false;
            _pendingOnComplete?.Invoke();
            return;
        }
        SetSprite(_currentAnimationFrames[_currentFrameIndex]);
        _currentFrameIndex++;
    }

    private void PlayWalk(BehaviorPlanner.WalkDirection? forcedDirection = null)
    {
        var (minX, maxX) = GetWalkJumpXBoundsInDips();
        var plan = BehaviorPlanner.PlanWalk(new PetPoint((int)Left, (int)Top), minX, maxX, _currentSpriteWidth, new SystemRandomSource(), forcedDirection);
        if (plan is null) return;

        _isAnimating = true;
        _loopCurrentAnimation = true;
        string animationName = plan.Direction == BehaviorPlanner.WalkDirection.Left ? "walkleft" : "walkright";
        PlayFrameSequence(animationName, onComplete: () => { });

        var animation = new System.Windows.Media.Animation.DoubleAnimation(Left, plan.TargetX, TimeSpan.FromMilliseconds(plan.DurationMs));
        animation.Completed += (_, _) =>
        {
            // See final-review Finding 1: release before reassigning, or the reassignment
            // (and later manual drag assignment) is silently ignored.
            BeginAnimation(LeftProperty, null);
            Left = plan.TargetX;
            _loopCurrentAnimation = false;
            _frameTimer.Stop();
            EnterIdleState();
        };
        BeginAnimation(LeftProperty, animation);
    }

    private void PlayJump(BehaviorPlanner.JumpDirection? forcedDirection = null)
    {
        var (minX, maxX) = GetWalkJumpXBoundsInDips();
        double dipScale = GetDipScale();
        var screenPoint = new System.Drawing.Point((int)(Left / dipScale), (int)(Top / dipScale));
        var screen = System.Windows.Forms.Screen.FromPoint(screenPoint);
        int landingY = (int)(screen.WorkingArea.Bottom * dipScale);

        // PlanJump's edge-avoidance treats maxX as the boundary for the character's LEFT
        // edge (X), with no allowance for its own width -- unlike PlanWalk, which already
        // reserves characterWidth for its rightward endpoint. Reserving it here too keeps
        // the character's right edge from poking past maxX.
        var plan = BehaviorPlanner.PlanJump(
            new PetPoint((int)Left, (int)Top),
            _currentSpriteHeight,
            minX,
            maxX - _currentSpriteWidth,
            landingY,
            new SystemRandomSource(),
            forcedDirection);
        _isAnimating = true;
        _isFalling = true;

        string animationName = plan.Direction == BehaviorPlanner.JumpDirection.Left ? "jumpleft" : "jumpright";
        var frames = GetOrLoadFrames(animationName);

        var riseAnimation = new System.Windows.Media.Animation.DoubleAnimation(Top, plan.RiseTarget.Y, TimeSpan.FromMilliseconds(plan.DurationMs))
        {
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        var riseAnimationX = new System.Windows.Media.Animation.DoubleAnimation(Left, plan.RiseTarget.X, TimeSpan.FromMilliseconds(plan.DurationMs))
        {
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };

        if (frames.Count > 0)
        {
            _loopCurrentAnimation = true;
            PlayFrameSequence(animationName, onComplete: () => { });
        }
        else
        {
            // Hachiware has no animated jump frames — use the single static sprite, matching
            // the original's fallback path.
            SetSprite(_sprites[animationName]);
        }

        riseAnimation.Completed += (_, _) =>
        {
            var landAnimation = new System.Windows.Media.Animation.DoubleAnimation(Top, plan.LandTarget.Y, TimeSpan.FromMilliseconds(plan.DurationMs))
            {
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            var landAnimationX = new System.Windows.Media.Animation.DoubleAnimation(Left, plan.LandTarget.X, TimeSpan.FromMilliseconds(plan.DurationMs))
            {
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            landAnimation.Completed += (_, _) =>
            {
                // See final-review Finding 1: release before reassigning, or the
                // reassignment (and later manual drag assignment) is silently ignored.
                BeginAnimation(TopProperty, null);
                BeginAnimation(LeftProperty, null);
                Top = plan.LandTarget.Y;
                Left = plan.LandTarget.X;
                _loopCurrentAnimation = false;
                _frameTimer.Stop();
                EnterIdleState();
            };
            BeginAnimation(TopProperty, landAnimation);
            BeginAnimation(LeftProperty, landAnimationX);
        };
        BeginAnimation(TopProperty, riseAnimation);
        BeginAnimation(LeftProperty, riseAnimationX);
    }

    private void PlayNamedAnimation(string animationName)
    {
        _isAnimating = true;
        _loopCurrentAnimation = false;
        PlayFrameSequence(animationName, onComplete: () =>
        {
            EnterIdleState();
        });
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isAnimating) return;

        _frameTimer.Stop();
        _loopCurrentAnimation = false;
        _isDragging = true;
        _isFalling = false;
        _isShaking = false;
        _grabbedSprite = RandomFrom(_sprites, "grabbed");
        SetSprite(_grabbedSprite);
        _dragOffset = e.GetPosition(this);
        _holdTimer.Start();

        PlayRandomGrabbedSound();
        CaptureMouse();
    }

    private void PlayRandomGrabbedSound()
    {
        string soundsDir = Path.Combine(_assetRoot, "sounds");
        if (!Directory.Exists(soundsDir)) return; // Hachiware: no-op, matches Global Constraints note.

        var candidates = new List<string>();
        foreach (var file in Directory.GetFiles(soundsDir, "grabbed*.wav"))
            candidates.Add(file);
        if (candidates.Count == 0) return;

        SoundPlayerFactory.PlayIfExists(candidates[_dragRandom.Next(candidates.Count)]);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _isAnimating) return;

        double dipScale = GetDipScale();
        var cursor = PointToScreen(e.GetPosition(this));
        var candidate = new PetPoint(
            (int)(cursor.X * dipScale - _dragOffset.X),
            (int)(cursor.Y * dipScale - _dragOffset.Y));

        // SystemParameters.WorkArea always reflects the PRIMARY monitor. Clamp against
        // whichever monitor is under the candidate point instead, so dragging onto a
        // second monitor doesn't get pulled back onto the primary one.
        var screenPoint = new System.Drawing.Point((int)(candidate.X / dipScale), (int)(candidate.Y / dipScale));
        var workingArea = System.Windows.Forms.Screen.FromPoint(screenPoint).WorkingArea;

        // Same DIP/physical-pixel mismatch as FallTo -- see GetDipScale's comment for why.
        var bounds = new PetBounds(
            (int)(workingArea.Left * dipScale),
            (int)(workingArea.Top * dipScale),
            (int)(workingArea.Right * dipScale),
            (int)(workingArea.Bottom * dipScale));

        var clamped = BehaviorPlanner.ClampToBounds(candidate, bounds, _currentSpriteWidth, _currentSpriteHeight);

        if (_isShaking)
        {
            Left = clamped.X + _dragRandom.Next(0, 11);
            Top = clamped.Y + _dragRandom.Next(0, 11);
        }
        else
        {
            Left = clamped.X;
            Top = clamped.Y;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isAnimating) return;

        ReleaseMouseCapture();
        _isDragging = false;
        _holdTimer.Stop();
        _isShaking = false;
        _grabbedSprite = null;

        FallTo();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isShuttingDown) return;
        e.Handled = true;

        if (_isDragging)
        {
            ReleaseMouseCapture();
            _isDragging = false;
            _holdTimer.Stop();
            _isShaking = false;
            _grabbedSprite = null;
        }

        // Pause active movements and animations immediately
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

    private void ShowContextMenu()
    {
        var contextMenu = new System.Windows.Controls.ContextMenu();

        string displayName = App.GetCharacterDisplayName(CharacterName);
        var titleItem = new System.Windows.Controls.MenuItem
        {
            Header = $"【{displayName}】",
            IsEnabled = false,
            FontWeight = System.Windows.FontWeights.Bold
        };
        contextMenu.Items.Add(titleItem);
        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var playMenu = new System.Windows.Controls.MenuItem { Header = "播放動畫" };
        foreach (var animName in AllAnimationNames())
        {
            var item = new System.Windows.Controls.MenuItem { Header = App.GetAnimationDisplayName(animName) };
            string nameCopy = animName;
            item.Click += (_, _) => PlayAnimationByName(nameCopy);
            playMenu.Items.Add(item);
        }
        contextMenu.Items.Add(playMenu);

        var randomAnimItem = new System.Windows.Controls.MenuItem
        {
            Header = "隨機動作",
            IsCheckable = true,
            IsChecked = _randomAnimationsEnabled
        };
        randomAnimItem.Click += (_, _) => SetRandomAnimationsEnabled(randomAnimItem.IsChecked);
        contextMenu.Items.Add(randomAnimItem);

        var randomJumpItem = new System.Windows.Controls.MenuItem
        {
            Header = "允許隨機跳躍",
            IsCheckable = true,
            IsChecked = _jumpEnabled
        };
        randomJumpItem.Click += (_, _) => SetJumpEnabled(randomJumpItem.IsChecked);
        contextMenu.Items.Add(randomJumpItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var sayHiItem = new System.Windows.Controls.MenuItem { Header = "打個招呼！" };
        sayHiItem.Click += (_, _) => SayHiRequested?.Invoke();
        contextMenu.Items.Add(sayHiItem);

        var kickItem = new System.Windows.Controls.MenuItem { Header = "踢出角色" };
        kickItem.Click += (_, _) => KickRequested?.Invoke();
        contextMenu.Items.Add(kickItem);

        contextMenu.Closed += (_, _) =>
        {
            if (!_isShuttingDown && !_isAnimating && !_isDragging)
            {
                EnterIdleState();
                if (_randomAnimationsEnabled)
                {
                    StartIdleTimer();
                }
            }
        };

        ContextMenu = contextMenu;
        contextMenu.IsOpen = true;
    }
}
