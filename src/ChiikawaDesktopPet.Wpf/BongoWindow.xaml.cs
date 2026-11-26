// src/ChiikawaDesktopPet.Wpf/BongoWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ChiikawaDesktopPet.Core;

namespace ChiikawaDesktopPet.Wpf;

public partial class BongoWindow : Window, IClickThroughWindow
{
    public const double BaseVisualScale = 0.5;

    private readonly BongoConfig _config;
    private readonly BongoSkinManager _skinManager;
    private readonly GlobalKeyboardHook _keyboardHook;
    private readonly GlobalMouseHook _mouseHook;
    private readonly BongoSpeedTracker _speedTracker;
    private readonly BongoBananaTracker _bananaTracker;
    private readonly BananaTreeAssets _bananaTreeAssets;

    private BongoSkin? _currentSkin;
    private readonly DispatcherTimer _rightPawResetTimer;
    private readonly DispatcherTimer _speedPollTimer;
    private readonly DispatcherTimer _mouseMotionTimer;
    private readonly DispatcherTimer _bubbleTimer = new();
    private readonly DispatcherTimer _eatAnimationTimer;
    private readonly DispatcherTimer _bananaSwayTimer;
    private readonly DispatcherTimer _statsSaveDebounceTimer;
    private bool _isUpdatingBubbleLayout;
    private double _lastBubbleHeight;

    private const int EatAnimationLoops = 2;
    private bool _isEatingBanana;
    private int _eatFrameIndex;
    private int _eatLoopsCompleted;
    private double _bananaSwayPhase;

    private double _targetMouseX;
    private double _targetMouseY;
    private double _currentMouseX;
    private double _currentMouseY;

    private double _targetRightPawX;
    private double _targetRightPawY;
    private double _currentRightPawX;
    private double _currentRightPawY;

    private BongoSpeedTier _currentTier = BongoSpeedTier.Idle;
    private bool _isBossKeyHidden;
    private bool _isSystemSuspended;
    private bool _hasBeenShown;

    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _contextMenuHwnd = IntPtr.Zero;

    // Active key and mouse down tracking for "hold down until release"
    private readonly HashSet<int> _activeRightKeys = [];
    private readonly HashSet<int> _activeLeftKeys = [];
    private bool _isMouseLeftDown;
    private bool _isMouseRightDown;

    public BongoConfig Config => _config;
    public BongoBananaTracker BananaTracker => _bananaTracker;
    public bool IsEatingBanana => _isEatingBanana;
    public bool CanCurrentSkinEatBanana => _currentSkin != null && _currentSkin.EatFrames.Count > 0;
    public BananaGrowthStage CurrentBananaStage => _bananaTracker.Stage;

    public event Action? VisibilityChanged;
    public event Action<bool>? ClickThroughChanged;
    public event Action? ProfileChanged;
    public event Action? BananaStatsChanged;

    public bool HasOpenContextMenu => BongoContextMenu.IsOpen;
    public bool IsPetHidden => _isBossKeyHidden || _isSystemSuspended || !IsVisible;
    public bool IsSystemSuspended => _isSystemSuspended;
    public CharacterWindow.SpeechBubblePlacement CurrentBubblePlacement { get; private set; } = CharacterWindow.SpeechBubblePlacement.Top;
    public bool AlwaysShowBubble => _config.AlwaysShowBubble;
    public bool IsBubbleVisible => BubbleContainer.Visibility == Visibility.Visible;
    public string CustomDialogueText => _config.DialogueText ?? string.Empty;
    public bool HasCustomDialogueText => !string.IsNullOrWhiteSpace(_config.DialogueText);
    public string CurrentDialogueText => HasCustomDialogueText
        ? _config.DialogueText!
        : CharacterQuotes.GetDefaultQuote(_config.CurrentSkinKey);
    public TextAlignment DialogueAlignment => ParseAlignment(_config.DialogueAlignment);
    public double DialogueFontSize => _config.DialogueFontSize > 0
        ? Math.Clamp(_config.DialogueFontSize, 9.0, 36.0)
        : 13.0;
    public double DialogueImageMaxWidth => _config.DialogueImageMaxWidth > 0 ? _config.DialogueImageMaxWidth : 260.0;
    public double DialogueImageMaxHeight => _config.DialogueImageMaxHeight > 0 ? _config.DialogueImageMaxHeight : 200.0;
    public double SafeLeft => double.IsFinite(Left) ? Left : (double.IsFinite(_config.PositionX) ? _config.PositionX : -1);
    public double SafeTop => double.IsFinite(Top) ? Top : (double.IsFinite(_config.PositionY) ? _config.PositionY : -1);
    public IntPtr Handle => _hwnd != IntPtr.Zero ? _hwnd : (_hwnd = new WindowInteropHelper(this).Handle);

    public void CloseContextMenu()
    {
        BongoContextMenu.IsOpen = false;
    }

    internal bool IsPointInsideContextMenu(NativeMethods.POINT pt)
    {
        if (!BongoContextMenu.IsOpen) return false;
        if (_contextMenuHwnd != IntPtr.Zero && NativeMethods.GetWindowRect(_contextMenuHwnd, out var rect))
        {
            return pt.X >= rect.Left && pt.X <= rect.Right && pt.Y >= rect.Top && pt.Y <= rect.Bottom;
        }
        return false;
    }

    bool IClickThroughWindow.IsPointInsideContextMenu(NativeMethods.POINT pt) => IsPointInsideContextMenu(pt);

    public void TriggerContextMenuFromHook()
    {
        if (_isBossKeyHidden || !IsVisible) return;

        var hwnd = Handle;
        if (hwnd != IntPtr.Zero)
        {
            if (_config.ClickThrough)
            {
                NativeMethods.SetWindowClickThrough(hwnd, false);
            }
            NativeMethods.SetForegroundWindow(hwnd);
        }

        BongoContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        BongoContextMenu.IsOpen = true;
    }

    public BongoWindow(BongoConfig config, BongoSkinManager skinManager, GlobalKeyboardHook keyboardHook)
        : this(config, skinManager, keyboardHook, new GlobalMouseHook())
    {
    }

    public BongoWindow(BongoConfig config, BongoSkinManager skinManager, GlobalKeyboardHook keyboardHook, GlobalMouseHook mouseHook)
    {
        InitializeComponent();

        _config = config;
        _skinManager = skinManager;
        _keyboardHook = keyboardHook;
        _mouseHook = mouseHook;

        _speedTracker = new BongoSpeedTracker
        {
            FocusCpmThreshold = _config.FocusCpmThreshold,
            OverdriveCpmThreshold = _config.OverdriveCpmThreshold
        };

        _bananaTracker = BongoBananaTracker.FromConfig(_config);
        _bananaTreeAssets = _skinManager.LoadBananaTreeAssets();
        BananaTreeBaseImage.Source = _bananaTreeAssets.TreeBase;

        _eatAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _eatAnimationTimer.Tick += (_, _) => AdvanceEatAnimationFrame();

        _bananaSwayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _bananaSwayTimer.Tick += (_, _) =>
        {
            _bananaSwayPhase += 0.22;
            BananaSwayTransform.Angle = Math.Sin(_bananaSwayPhase) * 6.5;
        };

        _statsSaveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statsSaveDebounceTimer.Tick += (_, _) =>
        {
            _statsSaveDebounceTimer.Stop();
            _bananaTracker.SyncToConfig(_config);
            BananaStatsChanged?.Invoke();
        };

        UpdateBananaTreeVisuals();

        _rightPawResetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _rightPawResetTimer.Tick += (_, _) =>
        {
            _rightPawResetTimer.Stop();
            _targetRightPawX = 0;
            _targetRightPawY = 0;
        };

        _speedPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _speedPollTimer.Tick += (_, _) => UpdateSpeedFeedback();

        _mouseMotionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        _mouseMotionTimer.Tick += (_, _) =>
        {
            if (_config.InputMode == BongoInputMode.KeyboardAndMouse && _config.TrackMouseMotion)
            {
                _currentMouseX += (_targetMouseX - _currentMouseX) * 0.35;
                _currentMouseY += (_targetMouseY - _currentMouseY) * 0.35;
                LeftPawTransform.X = _currentMouseX;
                LeftPawTransform.Y = _currentMouseY;
            }
            else
            {
                LeftPawTransform.X = 0;
                LeftPawTransform.Y = 0;
            }

            // Smooth return or follow for right keyboard paw
            _currentRightPawX += (_targetRightPawX - _currentRightPawX) * 0.35;
            _currentRightPawY += (_targetRightPawY - _currentRightPawY) * 0.35;
            RightPawTransform.X = _currentRightPawX;
            RightPawTransform.Y = _currentRightPawY;
        };

        BongoContextMenu.Opened += (_, _) =>
        {
            var source = (HwndSource?)PresentationSource.FromVisual(BongoContextMenu);
            _contextMenuHwnd = source?.Handle ?? IntPtr.Zero;
        };

        BongoContextMenu.Closed += (_, _) =>
        {
            _contextMenuHwnd = IntPtr.Zero;
            if (_config.ClickThrough)
            {
                var hwnd = Handle;
                if (hwnd != IntPtr.Zero)
                {
                    NativeMethods.SetWindowClickThrough(hwnd, true);
                }
            }
        };

        _bubbleTimer.Tick += (_, _) => OnBubbleTimerTick();
        BubbleContainer.SizeChanged += OnBubbleContainerSizeChanged;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        LocationChanged += OnLocationChanged;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeToolWindow(_hwnd);
        NativeMethods.DisableMinimizeButton(_hwnd);

        if (_config.ClickThrough)
        {
            NativeMethods.SetWindowClickThrough(_hwnd, true);
            ClickThroughManager.Instance.Register(this);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hasBeenShown = true;
        ApplyScale(_config.Scale);
        ApplySkin(_config.CurrentSkinKey);
        ApplyInitialPosition();

        _keyboardHook.KeyDown -= OnGlobalKeyDown;
        _keyboardHook.KeyUp -= OnGlobalKeyUp;
        _mouseHook.MouseMove -= OnGlobalMouseMove;
        _mouseHook.MouseDown -= OnGlobalMouseDown;
        _mouseHook.MouseUp -= OnGlobalMouseUp;

        _keyboardHook.KeyDown += OnGlobalKeyDown;
        _keyboardHook.KeyUp += OnGlobalKeyUp;
        _mouseHook.MouseMove += OnGlobalMouseMove;
        _mouseHook.MouseDown += OnGlobalMouseDown;
        _mouseHook.MouseUp += OnGlobalMouseUp;

        if (_config.IsEnabled && !_isBossKeyHidden && !_isSystemSuspended)
        {
            _speedPollTimer.Start();
            _mouseMotionTimer.Start();
        }

        if (_config.AlwaysShowBubble)
        {
            ShowSpeechBubble(forceRefreshContent: true);
        }

        RebuildContextMenu();
    }

    public void ApplyInitialPosition()
    {
        var workArea = SystemParameters.WorkArea;
        double defaultWidth = 438 * _config.Scale * BaseVisualScale;
        double defaultHeight = 240 * _config.Scale * BaseVisualScale;

        bool hasInvalidSavedPos = !double.IsFinite(_config.PositionX) ||
                                  !double.IsFinite(_config.PositionY) ||
                                  (_config.PositionX == -1 && _config.PositionY == -1);

        if (hasInvalidSavedPos)
        {
            Left = Math.Max(workArea.Left, workArea.Right - defaultWidth - 30);
            Top = Math.Max(workArea.Top, workArea.Bottom - defaultHeight - 30);
            _config.PositionX = Left;
            _config.PositionY = Top;
        }
        else
        {
            Left = _config.PositionX;
            Top = _config.PositionY;
            ClampToScreen();
        }
    }

    public void ApplySkin(string skinKey)
    {
        if (_isEatingBanana)
        {
            StopEatingAnimation();
        }

        _currentSkin = _skinManager.LoadSkin(skinKey);
        _config.CurrentSkinKey = _currentSkin.Info.Key;

        BodyImage.Source = _currentSkin.Body;
        LeftPawImage.Source = _currentSkin.LeftPawUp;
        RightPawImage.Source = _currentSkin.RightPawUp;
        FaceImage.Source = _currentSkin.FaceNormal;
        EffectImage.Source = _currentSkin.OverdriveEffect;
        EffectImage.Visibility = Visibility.Collapsed;

        UpdateBananaTreeVisuals();

        if (IsBubbleVisible && !HasCustomDialogueText)
        {
            UpdateBubbleContent();
        }

        RebuildContextMenu();
    }

    public void ApplyScale(double scale)
    {
        scale = Math.Clamp(scale, 0.2, 4.0);
        _config.Scale = scale;
        double effectiveScale = scale * BaseVisualScale;
        WindowScaleTransform.ScaleX = effectiveScale;
        WindowScaleTransform.ScaleY = effectiveScale;
        UpdateLayout();
        ClampToScreen();
    }

    private void OnGlobalMouseMove(int screenX, int screenY)
    {
        if (_isBossKeyHidden || !IsVisible || !_config.TrackMouseMotion || _config.InputMode != BongoInputMode.KeyboardAndMouse) return;

        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;
        if (vWidth <= 0) vWidth = 1920;
        if (vHeight <= 0) vHeight = 1080;

        double normX = Math.Clamp((screenX - vLeft) / vWidth, 0.0, 1.0);
        double normY = Math.Clamp((screenY - vTop) / vHeight, 0.0, 1.0);

        // Safe mousepad travel boundary: delta X [-22, +22], delta Y [-14, +14]
        // Mirrored X tracking: user moves right -> pet moves left; user moves left -> pet moves right
        double factorX = _config.MirrorMouseMotion ? -1.0 : 1.0;
        _targetMouseX = factorX * (normX - 0.5) * 44.0;
        _targetMouseY = (normY - 0.5) * 28.0;
    }

    private void OnGlobalMouseDown(bool isLeftButton)
    {
        if (_isBossKeyHidden || !IsVisible) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (isLeftButton) _isMouseLeftDown = true;
            else _isMouseRightDown = true;

            _speedTracker.RecordMouseClick(isLeftButton);

            if (_isEatingBanana) return;

            if (_currentSkin != null)
            {
                if (isLeftButton)
                {
                    // User Left Click -> mouse's LEFT button (mirrored to screen-right in 180° layout)
                    LeftPawImage.Source = _currentSkin.LeftPawDown ?? _currentSkin.LeftPawUp;
                }
                else
                {
                    // User Right Click -> mouse's RIGHT button (mirrored to screen-left in 180° layout)
                    LeftPawImage.Source = _currentSkin.LeftPawDownRight ?? _currentSkin.LeftPawDown ?? _currentSkin.LeftPawUp;
                }
            }

            UpdateSpeedFeedback();
        });
    }

    private void OnGlobalMouseUp(bool isLeftButton)
    {
        if (_isBossKeyHidden || !IsVisible) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (isLeftButton) _isMouseLeftDown = false;
            else _isMouseRightDown = false;

            if (_isEatingBanana) return;

            if (_currentSkin != null)
            {
                if (_isMouseLeftDown)
                {
                    LeftPawImage.Source = _currentSkin.LeftPawDown ?? _currentSkin.LeftPawUp;
                }
                else if (_isMouseRightDown)
                {
                    LeftPawImage.Source = _currentSkin.LeftPawDownRight ?? _currentSkin.LeftPawDown ?? _currentSkin.LeftPawUp;
                }
                else
                {
                    LeftPawImage.Source = _currentSkin.LeftPawUp;
                }
            }
        });
    }

    private void OnGlobalKeyDown(int vkCode)
    {
        if (_isBossKeyHidden || !IsVisible) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            ProcessKeyDownOnUiThread(vkCode);
        });
    }

    internal void ProcessKeyDownOnUiThread(int vkCode)
    {
        if (_bananaTracker.OnKeyDown(vkCode))
        {
            _bananaTracker.SyncToConfig(_config);
            UpdateBananaTreeVisuals();
            _statsSaveDebounceTimer.Stop();
            _statsSaveDebounceTimer.Start();
        }

        if (_isEatingBanana) return;

        if (_config.InputMode == BongoInputMode.KeyboardAndMouse)
        {
            _activeRightKeys.Add(vkCode);
            _speedTracker.RecordKeyPress(vkCode, alternateIfRapid: false);
            TapRightPaw(vkCode);
        }
        else
        {
            // In PureKeyboard mode, assign by key half
            _speedTracker.RecordKeyPress(vkCode, alternateIfRapid: true);
            if (BongoKeyboardLayout.IsLeftHandKey(vkCode))
            {
                _activeLeftKeys.Add(vkCode);
                if (_currentSkin != null) LeftPawImage.Source = _currentSkin.LeftPawDown ?? _currentSkin.LeftPawUp;
            }
            else
            {
                _activeRightKeys.Add(vkCode);
                TapRightPaw(vkCode);
            }
        }

        UpdateSpeedFeedback();
    }

    private void OnGlobalKeyUp(int vkCode)
    {
        if (_isBossKeyHidden || !IsVisible) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            ProcessKeyUpOnUiThread(vkCode);
        });
    }

    internal void ProcessKeyUpOnUiThread(int vkCode)
    {
        _bananaTracker.OnKeyUp(vkCode);

        if (_isEatingBanana)
        {
            _activeLeftKeys.Remove(vkCode);
            _activeRightKeys.Remove(vkCode);
            return;
        }

        if (_config.InputMode == BongoInputMode.KeyboardAndMouse)
        {
            _activeRightKeys.Remove(vkCode);
            if (_activeRightKeys.Count == 0)
            {
                if (_currentSkin != null) RightPawImage.Source = _currentSkin.RightPawUp;
                _rightPawResetTimer.Stop();
                _rightPawResetTimer.Start();
            }
        }
        else
        {
            if (BongoKeyboardLayout.IsLeftHandKey(vkCode))
            {
                _activeLeftKeys.Remove(vkCode);
                if (_activeLeftKeys.Count == 0 && _currentSkin != null)
                {
                    LeftPawImage.Source = _currentSkin.LeftPawUp;
                }
            }
            else
            {
                _activeRightKeys.Remove(vkCode);
                if (_activeRightKeys.Count == 0)
                {
                    if (_currentSkin != null) RightPawImage.Source = _currentSkin.RightPawUp;
                    _rightPawResetTimer.Stop();
                    _rightPawResetTimer.Start();
                }
            }
        }
    }

    // Dynamic right paw key targeting and timer reset logic
    private void TapRightPaw(int vkCode)
    {
        var (targetX, targetY) = BongoKeyboardLayout.GetTargetOffset(vkCode);
        _targetRightPawX = targetX;
        _targetRightPawY = targetY;

        _currentRightPawX = _currentRightPawX * 0.3 + targetX * 0.7;
        _currentRightPawY = _currentRightPawY * 0.3 + targetY * 0.7;
        RightPawTransform.X = _currentRightPawX;
        RightPawTransform.Y = _currentRightPawY;

        if (_currentSkin != null) RightPawImage.Source = _currentSkin.RightPawDown ?? _currentSkin.RightPawUp;

        // Stop resting return timer while key is actively held down
        _rightPawResetTimer.Stop();
    }

    private void UpdateSpeedFeedback()
    {
        if (_currentSkin == null || _isEatingBanana) return;

        var tier = _speedTracker.GetCurrentTier();
        if (tier == _currentTier) return;

        _currentTier = tier;
        switch (tier)
        {
            case BongoSpeedTier.Overdrive:
                FaceImage.Source = _currentSkin.FaceOverdrive ?? _currentSkin.FaceFocused ?? _currentSkin.FaceNormal;
                EffectImage.Visibility = _currentSkin.OverdriveEffect != null ? Visibility.Visible : Visibility.Collapsed;
                break;

            case BongoSpeedTier.Focused:
                FaceImage.Source = _currentSkin.FaceFocused ?? _currentSkin.FaceNormal;
                EffectImage.Visibility = Visibility.Collapsed;
                break;

            case BongoSpeedTier.Normal:
            case BongoSpeedTier.Idle:
            default:
                FaceImage.Source = _currentSkin.FaceNormal;
                EffectImage.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void OnBananaTreeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        if (_bananaTracker.IsRipe && CanCurrentSkinEatBanana && !_isEatingBanana)
        {
            e.Handled = true;
            TryEatRipeBanana();
        }
    }

    public bool TryEatRipeBanana()
    {
        if (_isEatingBanana || !_bananaTracker.IsRipe || !CanCurrentSkinEatBanana)
        {
            return false;
        }

        if (!_bananaTracker.TryHarvest())
        {
            return false;
        }

        _bananaTracker.SyncToConfig(_config);
        UpdateBananaTreeVisuals();
        StartEatingAnimation();

        _statsSaveDebounceTimer.Stop();
        BananaStatsChanged?.Invoke();
        return true;
    }

    private void UpdateBananaTreeVisuals()
    {
        var stage = _bananaTracker.Stage;
        var fruit = _bananaTreeAssets.GetFruitForStage(stage);
        BananaFruitImage.Source = fruit;
        BananaFruitImage.Visibility = fruit != null ? Visibility.Visible : Visibility.Collapsed;

        bool canEatNow = _bananaTracker.IsRipe && CanCurrentSkinEatBanana && !_isEatingBanana;
        BananaTreeContainer.Cursor = canEatNow ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
        BananaTreeContainer.ToolTip = _bananaTracker.FormatTooltip(CanCurrentSkinEatBanana);

        if (_bananaTracker.IsRipe && !_isBossKeyHidden && !_isSystemSuspended && _config.IsEnabled)
        {
            if (!_bananaSwayTimer.IsEnabled)
            {
                _bananaSwayPhase = 0;
                _bananaSwayTimer.Start();
            }
        }
        else
        {
            _bananaSwayTimer.Stop();
            BananaSwayTransform.Angle = 0;
        }
    }

    private void StartEatingAnimation()
    {
        if (_currentSkin == null || _currentSkin.EatFrames.Count == 0) return;

        _isEatingBanana = true;
        _eatFrameIndex = 0;
        _eatLoopsCompleted = 0;

        FaceImage.Visibility = Visibility.Collapsed;
        EffectImage.Visibility = Visibility.Collapsed;
        LeftPawImage.Visibility = Visibility.Collapsed;
        RightPawImage.Visibility = Visibility.Collapsed;

        EatAnimationImage.Source = _currentSkin.EatFrames[0];
        EatAnimationImage.Visibility = Visibility.Visible;

        UpdateBananaTreeVisuals();
        _eatAnimationTimer.Start();
    }

    internal void AdvanceEatAnimationFrame()
    {
        if (!_isEatingBanana || _currentSkin == null || _currentSkin.EatFrames.Count == 0)
        {
            StopEatingAnimation();
            return;
        }

        _eatFrameIndex++;
        if (_eatFrameIndex >= _currentSkin.EatFrames.Count)
        {
            _eatFrameIndex = 0;
            _eatLoopsCompleted++;
            if (_eatLoopsCompleted >= EatAnimationLoops)
            {
                StopEatingAnimation();
                return;
            }
        }

        EatAnimationImage.Source = _currentSkin.EatFrames[_eatFrameIndex];
    }

    private void StopEatingAnimation()
    {
        _eatAnimationTimer.Stop();
        _isEatingBanana = false;
        _eatFrameIndex = 0;
        _eatLoopsCompleted = 0;

        EatAnimationImage.Visibility = Visibility.Collapsed;
        EatAnimationImage.Source = null;

        FaceImage.Visibility = Visibility.Visible;
        LeftPawImage.Visibility = Visibility.Visible;
        RightPawImage.Visibility = Visibility.Visible;
        _currentTier = BongoSpeedTier.Idle;

        if (_currentSkin != null)
        {
            FaceImage.Source = _currentSkin.FaceNormal;
            LeftPawImage.Source = _currentSkin.LeftPawUp;
            RightPawImage.Source = _currentSkin.RightPawUp;
        }

        UpdateBananaTreeVisuals();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        if (_config.IsLocked)
        {
            ShowSpeechBubble(3500);
            return;
        }

        double startLeft = Left;
        double startTop = Top;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Ignore if mouse button was released before DragMove started
        }

        double dx = double.IsFinite(Left) && double.IsFinite(startLeft) ? Math.Abs(Left - startLeft) : 0;
        double dy = double.IsFinite(Top) && double.IsFinite(startTop) ? Math.Abs(Top - startTop) : 0;
        if (dx + dy < 3.0)
        {
            ShowSpeechBubble(3500);
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingBubbleLayout) return;

        ClampToScreen();
        if (double.IsFinite(Left) && double.IsFinite(Top))
        {
            _config.PositionX = Left;
            _config.PositionY = Top;
        }
    }

    private void ClampToScreen()
    {
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;
        if (vWidth <= 0 || vHeight <= 0) return;

        double windowWidth = ActualWidth > 0 ? ActualWidth : 438 * _config.Scale * BaseVisualScale;
        double windowHeight = ActualHeight > 0 ? ActualHeight : 240 * _config.Scale * BaseVisualScale;

        double currentLeft = Left;
        double currentTop = Top;
        if (!double.IsFinite(currentLeft) || !double.IsFinite(currentTop))
        {
            var workArea = SystemParameters.WorkArea;
            currentLeft = Math.Max(workArea.Left, workArea.Right - windowWidth - 30);
            currentTop = Math.Max(workArea.Top, workArea.Bottom - windowHeight - 30);
        }

        double maxRight = vLeft + vWidth - windowWidth;
        double maxBottom = vTop + vHeight - windowHeight;

        Left = Math.Clamp(currentLeft, vLeft, Math.Max(vLeft, maxRight));
        Top = Math.Clamp(currentTop, vTop, Math.Max(vTop, maxBottom));

        if (double.IsFinite(Left) && double.IsFinite(Top))
        {
            _config.PositionX = Left;
            _config.PositionY = Top;
        }
    }

    public void SetLocked(bool isLocked)
    {
        _config.IsLocked = isLocked;
        RebuildContextMenu();
    }

    public void SetClickThrough(bool clickThrough)
    {
        _config.ClickThrough = clickThrough;
        var hwnd = Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowClickThrough(hwnd, clickThrough);
        }

        if (clickThrough)
        {
            ClickThroughManager.Instance.Register(this);
        }
        else
        {
            ClickThroughManager.Instance.Unregister(this);
        }

        RebuildContextMenu();
        ClickThroughChanged?.Invoke(_config.ClickThrough);
    }

    public void ResetPosition()
    {
        _config.PositionX = -1;
        _config.PositionY = -1;
        ApplyInitialPosition();
    }

    private void ResetInteractionState()
    {
        _speedPollTimer.Stop();
        _mouseMotionTimer.Stop();
        _rightPawResetTimer.Stop();
        _bubbleTimer.Stop();
        _bananaSwayTimer.Stop();
        _statsSaveDebounceTimer.Stop();
        BananaSwayTransform.Angle = 0;
        _bananaTracker.ResetKeyState();
        if (_isEatingBanana)
        {
            StopEatingAnimation();
        }
        _activeRightKeys.Clear();
        _activeLeftKeys.Clear();
        _isMouseLeftDown = false;
        _isMouseRightDown = false;
        _currentRightPawX = 0;
        _currentRightPawY = 0;
        _targetRightPawX = 0;
        _targetRightPawY = 0;
        RightPawTransform.X = 0;
        RightPawTransform.Y = 0;
        if (_currentSkin != null)
        {
            LeftPawImage.Source = _currentSkin.LeftPawUp;
            RightPawImage.Source = _currentSkin.RightPawUp;
        }
    }

    public void RefreshVisualSurface(bool clamp = false)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => RefreshVisualSurface(clamp));
            return;
        }

        InvalidateVisual();
        UpdateLayout();

        if (_currentSkin != null)
        {
            BodyImage.Source = null;
            BodyImage.Source = _currentSkin.Body;
            LeftPawImage.Source = null;
            LeftPawImage.Source = _currentSkin.LeftPawUp;
            RightPawImage.Source = null;
            RightPawImage.Source = _currentSkin.RightPawUp;
            FaceImage.Source = null;
            FaceImage.Source = _currentSkin.FaceNormal;
        }

        if (clamp)
        {
            ClampToScreen();
        }
    }

    private void ShowWindowEnsureLoaded()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top))
        {
            ApplyInitialPosition();
        }

        if (!_hasBeenShown && !IsLoaded)
        {
            _hasBeenShown = true;
            Show();
        }
        else
        {
            Visibility = Visibility.Visible;
        }
    }

    public void PauseForBossKey()
    {
        _isBossKeyHidden = true;
        ResetInteractionState();
        _keyboardHook.Stop();
        _mouseHook.Stop();
        Visibility = Visibility.Collapsed;
        VisibilityChanged?.Invoke();
    }

    public void ResumeFromBossKey()
    {
        _isBossKeyHidden = false;
        if (_config.IsEnabled && !_isSystemSuspended)
        {
            ShowWindowEnsureLoaded();
            RefreshVisualSurface(clamp: true);
            if (_config.AlwaysShowBubble) ShowSpeechBubble(forceRefreshContent: true);
            if (!_keyboardHook.IsActive) _keyboardHook.Start();
            if (!_mouseHook.IsActive) _mouseHook.Start();
            _speedPollTimer.Start();
            _mouseMotionTimer.Start();
            UpdateBananaTreeVisuals();
        }
        VisibilityChanged?.Invoke();
    }

    public void PauseForSystemSuspend()
    {
        if (_isSystemSuspended) return;
        _isSystemSuspended = true;
        ResetInteractionState();
        _keyboardHook.Stop();
        _mouseHook.Stop();
    }

    public void ResumeFromSystemSuspend()
    {
        if (!_isSystemSuspended) return;
        _isSystemSuspended = false;
        if (_config.IsEnabled && !_isBossKeyHidden)
        {
            ShowWindowEnsureLoaded();
            RefreshVisualSurface(clamp: true);
            if (_config.AlwaysShowBubble) ShowSpeechBubble(forceRefreshContent: true);
            if (!_keyboardHook.IsActive) _keyboardHook.Start();
            if (!_mouseHook.IsActive) _mouseHook.Start();
            _speedPollTimer.Start();
            _mouseMotionTimer.Start();
            UpdateBananaTreeVisuals();
        }
    }

    public void ShowBongo()
    {
        _config.IsEnabled = true;
        if (!_isBossKeyHidden && !_isSystemSuspended)
        {
            ShowWindowEnsureLoaded();
            RefreshVisualSurface(clamp: true);
            if (_config.AlwaysShowBubble) ShowSpeechBubble(forceRefreshContent: true);
            if (!_keyboardHook.IsActive) _keyboardHook.Start();
            if (!_mouseHook.IsActive) _mouseHook.Start();
            _speedPollTimer.Start();
            _mouseMotionTimer.Start();
            UpdateBananaTreeVisuals();
        }
        VisibilityChanged?.Invoke();
    }

    public void HideBongo()
    {
        _config.IsEnabled = false;
        Visibility = Visibility.Collapsed;
        ResetInteractionState();
        _keyboardHook.Stop();
        _mouseHook.Stop();
        VisibilityChanged?.Invoke();
    }

    public void ShowSpeechBubble(int durationMs = 3500, bool forceRefreshContent = false)
    {
        string text = CurrentDialogueText;
        if (string.IsNullOrWhiteSpace(text))
        {
            HideSpeechBubble(force: true);
            return;
        }

        if (BubbleContainer.Visibility == Visibility.Visible && !forceRefreshContent)
        {
            if (!_config.AlwaysShowBubble)
            {
                StartBubbleTimer(durationMs, text);
            }
            return;
        }

        UpdateBubbleContent();
        SetBubbleVisibilityAnchored(true);

        if (!_config.AlwaysShowBubble)
        {
            StartBubbleTimer(durationMs, text);
        }
        else
        {
            _bubbleTimer.Stop();
        }
    }

    public void HideSpeechBubble(bool force = false)
    {
        _bubbleTimer.Stop();
        if (!force && _config.AlwaysShowBubble) return;

        if (BubbleContainer.Visibility == Visibility.Visible)
        {
            SetBubbleVisibilityAnchored(false);
        }
    }

    public void SetAlwaysShowBubble(bool always)
    {
        if (_config.AlwaysShowBubble == always && (always == IsBubbleVisible))
        {
            RebuildContextMenu();
            return;
        }

        _config.AlwaysShowBubble = always;
        if (_config.AlwaysShowBubble)
        {
            _bubbleTimer.Stop();
            ShowSpeechBubble(forceRefreshContent: true);
        }
        else
        {
            HideSpeechBubble(force: true);
        }

        RebuildContextMenu();
        ProfileChanged?.Invoke();
    }

    public void SetCustomText(
        string? text,
        TextAlignment alignment = TextAlignment.Center,
        double fontSize = 13.0,
        double imageMaxWidth = 260.0,
        double imageMaxHeight = 200.0,
        bool showPreview = true)
    {
        _config.DialogueText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        _config.DialogueAlignment = alignment.ToString();
        _config.DialogueFontSize = Math.Clamp(fontSize, 9.0, 36.0);
        _config.DialogueImageMaxWidth = imageMaxWidth > 0 ? imageMaxWidth : 260.0;
        _config.DialogueImageMaxHeight = imageMaxHeight > 0 ? imageMaxHeight : 200.0;

        UpdateBubbleContent();

        if (showPreview)
        {
            if (_config.AlwaysShowBubble || HasCustomDialogueText)
            {
                ShowSpeechBubble(3500, forceRefreshContent: true);
            }
            else
            {
                HideSpeechBubble(force: true);
            }
        }
        else if (_config.AlwaysShowBubble)
        {
            ShowSpeechBubble(forceRefreshContent: true);
        }
        else
        {
            HideSpeechBubble(force: true);
        }

        ProfileChanged?.Invoke();
    }

    public void ApplyDialogueProfile(BongoProfileState state)
    {
        _config.DialogueText = string.IsNullOrWhiteSpace(state.DialogueText) ? null : state.DialogueText.Trim();
        _config.DialogueAlignment = string.IsNullOrWhiteSpace(state.DialogueAlignment) ? "Center" : state.DialogueAlignment;
        _config.DialogueFontSize = state.DialogueFontSize > 0 ? Math.Clamp(state.DialogueFontSize, 9.0, 36.0) : 13.0;
        _config.DialogueImageMaxWidth = state.DialogueImageMaxWidth > 0 ? state.DialogueImageMaxWidth : 260.0;
        _config.DialogueImageMaxHeight = state.DialogueImageMaxHeight > 0 ? state.DialogueImageMaxHeight : 200.0;
        _config.AlwaysShowBubble = state.AlwaysShowBubble;

        UpdateBubbleContent();
        if (_config.AlwaysShowBubble)
        {
            ShowSpeechBubble(forceRefreshContent: true);
        }
        else
        {
            HideSpeechBubble(force: true);
        }

        RebuildContextMenu();
    }

    private void StartBubbleTimer(int durationMs, string dialogueText)
    {
        int effectiveDuration = durationMs;
        if (durationMs == 3500 && MarkdownBubbleRenderer.ShouldExtendDisplayDuration(dialogueText))
        {
            effectiveDuration = 6000;
        }

        _bubbleTimer.Stop();
        _bubbleTimer.Interval = TimeSpan.FromMilliseconds(effectiveDuration);
        _bubbleTimer.Start();
    }

    private void OnBubbleTimerTick()
    {
        _bubbleTimer.Stop();
        if (!_config.AlwaysShowBubble)
        {
            HideSpeechBubble();
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
        if (!HasCustomDialogueText) return;

        string current = _config.DialogueText!;
        string newText = MarkdownBubbleRenderer.ToggleCheckboxAt(current, index);
        if (newText != current)
        {
            _config.DialogueText = newText;
            UpdateBubbleContent();
            ProfileChanged?.Invoke();
        }
    }

    private void OnBubbleImageLoaded()
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (BubbleContainer.Visibility == Visibility.Visible)
            {
                SetBubbleVisibilityAnchored(true);
                BubbleScrollViewer?.ScrollToTop();
            }
        });
    }

    private void OnBubbleContainerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isUpdatingBubbleLayout) return;
        if (BubbleContainer.Visibility != Visibility.Visible) return;
        if (!e.HeightChanged && !e.WidthChanged) return;

        double newBubbleH = BubbleContainer.ActualHeight;
        if (Math.Abs(newBubbleH - _lastBubbleHeight) > 0.5)
        {
            ReanchorAfterBubbleSizeChange(e.PreviousSize, e.NewSize);
        }
    }

    private void ReanchorAfterBubbleSizeChange(Size previousSize, Size newSize)
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top)) return;

        _isUpdatingBubbleLayout = true;
        try
        {
            double deltaH = newSize.Height - previousSize.Height;
            double deltaW = newSize.Width - previousSize.Width;

            if (CurrentBubblePlacement == CharacterWindow.SpeechBubblePlacement.Top)
            {
                Top -= deltaH;
            }
            if (Math.Abs(deltaW) > 0.5)
            {
                Left -= deltaW / 2.0;
            }

            _lastBubbleHeight = newSize.Height;
            ClampToScreen();
        }
        finally
        {
            _isUpdatingBubbleLayout = false;
        }
    }

    private void SetBubbleVisibilityAnchored(bool visible)
    {
        _isUpdatingBubbleLayout = true;
        try
        {
            bool hasFinitePos = double.IsFinite(Left) && double.IsFinite(Top);
            bool wasVisible = BubbleContainer.Visibility == Visibility.Visible;
            var oldPlacement = CurrentBubblePlacement;

            double oldBubbleH = wasVisible ? GetBubbleOuterHeight() : 0;
            double oldWindowW = ActualWidth > 0 ? ActualWidth : GetElementWidth(RootContainer);
            double petH = GetPetRowHeight();

            double petTopBefore = hasFinitePos
                ? ((wasVisible && oldPlacement == CharacterWindow.SpeechBubblePlacement.Top) ? Top + oldBubbleH : Top)
                : double.NaN;
            double petCenterXBefore = hasFinitePos ? Left + oldWindowW / 2.0 : double.NaN;

            BubbleContainer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (visible)
            {
                BubbleContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double measuredBubbleH = Math.Max(BubbleContainer.DesiredSize.Height, GetBubbleOuterHeight());
                UpdateBubblePlacement(measuredBubbleH, petTopBefore, petH);
            }
            else
            {
                ApplyBubblePlacementRows(CharacterWindow.SpeechBubblePlacement.Top);
            }

            UpdateLayout();

            double newBubbleH = visible ? GetBubbleOuterHeight() : 0;
            double newWindowW = ActualWidth > 0 ? ActualWidth : GetElementWidth(RootContainer);
            _lastBubbleHeight = visible ? BubbleContainer.ActualHeight : 0;

            if (hasFinitePos)
            {
                Left = petCenterXBefore - newWindowW / 2.0;
                Top = (visible && CurrentBubblePlacement == CharacterWindow.SpeechBubblePlacement.Top)
                    ? petTopBefore - newBubbleH
                    : petTopBefore;
                ClampToScreen();
            }
        }
        finally
        {
            _isUpdatingBubbleLayout = false;
        }
    }

    private double GetBubbleOuterHeight()
    {
        if (BubbleContainer.Visibility != Visibility.Visible) return 0;

        if (BubbleContainer.ActualHeight > 0)
        {
            return BubbleContainer.ActualHeight + BubbleContainer.Margin.Top + BubbleContainer.Margin.Bottom;
        }

        BubbleContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return BubbleContainer.DesiredSize.Height;
    }

    private double GetPetRowHeight()
    {
        int petRow = Grid.GetRow(PetVisualContainer);
        if (petRow >= 0 && petRow < RootContainer.RowDefinitions.Count && RootContainer.RowDefinitions[petRow].ActualHeight > 0)
        {
            return RootContainer.RowDefinitions[petRow].ActualHeight;
        }
        return 240 * _config.Scale * BaseVisualScale;
    }

    public CharacterWindow.SpeechBubblePlacement UpdateBubblePlacement(double bubbleH, double petTopDip, double petHeightDip)
    {
        var placement = CharacterWindow.SpeechBubblePlacement.Top;
        if (BubbleContainer.Visibility == Visibility.Visible && bubbleH > 0 && double.IsFinite(petTopDip))
        {
            var workArea = SystemParameters.WorkArea;
            double spaceAbove = petTopDip - workArea.Top;
            double spaceBelow = workArea.Bottom - (petTopDip + petHeightDip);

            const double margin = 10.0;
            bool canFitAbove = spaceAbove >= bubbleH + margin;
            bool canFitBelow = spaceBelow >= bubbleH + margin;

            if (!canFitAbove && (canFitBelow || spaceBelow > spaceAbove))
            {
                placement = CharacterWindow.SpeechBubblePlacement.Bottom;
            }
        }

        ApplyBubblePlacementRows(placement);
        return placement;
    }

    private void ApplyBubblePlacementRows(CharacterWindow.SpeechBubblePlacement placement)
    {
        CurrentBubblePlacement = placement;
        if (placement == CharacterWindow.SpeechBubblePlacement.Bottom)
        {
            Grid.SetRow(PetVisualContainer, 0);
            Grid.SetRow(BubbleContainer, 1);
            BubblePointerUp.Visibility = Visibility.Visible;
            BubblePointerDown.Visibility = Visibility.Collapsed;
            BubbleContainer.Margin = new Thickness(10, 4, 10, 12);
        }
        else
        {
            Grid.SetRow(BubbleContainer, 0);
            Grid.SetRow(PetVisualContainer, 1);
            BubblePointerUp.Visibility = Visibility.Collapsed;
            BubblePointerDown.Visibility = Visibility.Visible;
            BubbleContainer.Margin = new Thickness(10, 12, 10, 8);
        }
    }

    private static double GetElementHeight(FrameworkElement element)
    {
        if (element.ActualHeight > 0) return element.ActualHeight;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Height;
    }

    private static double GetElementWidth(FrameworkElement element)
    {
        if (element.ActualWidth > 0) return element.ActualWidth;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Width;
    }

    private static TextAlignment ParseAlignment(string? alignment) =>
        Enum.TryParse<TextAlignment>(alignment, ignoreCase: true, out var parsed)
            ? parsed
            : TextAlignment.Center;

    private void RebuildContextMenu()
    {
        BongoContextMenu.Items.Clear();

        // 一鍵隱藏 (Hide all desktop pets)
        var hideAllItem = new MenuItem
        {
            Header = "一鍵隱藏"
        };
        hideAllItem.Click += (_, _) => App.HideAllCharactersStatic();
        BongoContextMenu.Items.Add(hideAllItem);
        BongoContextMenu.Items.Add(new Separator());

        // Title Header
        var titleItem = new MenuItem
        {
            Header = $"🥁 Bongo 打字pet ({_currentSkin?.Info.DisplayName ?? "吉伊卡哇"})",
            IsEnabled = false,
            FontWeight = FontWeights.Bold
        };
        BongoContextMenu.Items.Add(titleItem);
        BongoContextMenu.Items.Add(new Separator());

        // Skins Submenu
        var skinsMenu = new MenuItem { Header = "更換造型 (Skins)" };
        var availableSkins = _skinManager.DiscoverSkins();
        foreach (var skin in availableSkins)
        {
            var item = new MenuItem
            {
                Header = skin.DisplayName,
                IsChecked = string.Equals(skin.Key, _config.CurrentSkinKey, StringComparison.OrdinalIgnoreCase)
            };
            string key = skin.Key;
            item.Click += (_, _) => ApplySkin(key);
            skinsMenu.Items.Add(item);
        }
        BongoContextMenu.Items.Add(skinsMenu);

        // Input Mode Submenu
        var modeMenu = new MenuItem { Header = "操作模式 (Mode)" };
        var kmItem = new MenuItem
        {
            Header = "鍵鼠並用 (左手滑鼠 + 右手鍵盤)",
            IsChecked = _config.InputMode == BongoInputMode.KeyboardAndMouse
        };
        kmItem.Click += (_, _) =>
        {
            _config.InputMode = BongoInputMode.KeyboardAndMouse;
            _targetRightPawX = 0;
            _targetRightPawY = 0;
            _currentRightPawX = 0;
            _currentRightPawY = 0;
            RightPawTransform.X = 0;
            RightPawTransform.Y = 0;
            RebuildContextMenu();
        };
        modeMenu.Items.Add(kmItem);

        var pkItem = new MenuItem
        {
            Header = "雙手純鍵盤 (雙手敲擊鍵盤)",
            IsChecked = _config.InputMode == BongoInputMode.PureKeyboard
        };
        pkItem.Click += (_, _) =>
        {
            _config.InputMode = BongoInputMode.PureKeyboard;
            LeftPawTransform.X = 0;
            LeftPawTransform.Y = 0;
            _targetRightPawX = 0;
            _targetRightPawY = 0;
            _currentRightPawX = 0;
            _currentRightPawY = 0;
            RightPawTransform.X = 0;
            RightPawTransform.Y = 0;
            RebuildContextMenu();
        };
        modeMenu.Items.Add(pkItem);

        modeMenu.Items.Add(new Separator());

        var trackItem = new MenuItem
        {
            Header = "左手滑鼠跟隨游標",
            IsChecked = _config.TrackMouseMotion
        };
        trackItem.Click += (_, _) =>
        {
            _config.TrackMouseMotion = !_config.TrackMouseMotion;
            if (!_config.TrackMouseMotion)
            {
                LeftPawTransform.X = 0;
                LeftPawTransform.Y = 0;
            }
            RebuildContextMenu();
        };
        modeMenu.Items.Add(trackItem);

        var mirrorItem = new MenuItem
        {
            Header = "滑鼠游標水平鏡像",
            IsChecked = _config.MirrorMouseMotion,
            IsEnabled = _config.TrackMouseMotion
        };
        mirrorItem.Click += (_, _) =>
        {
            _config.MirrorMouseMotion = !_config.MirrorMouseMotion;
            RebuildContextMenu();
        };
        modeMenu.Items.Add(mirrorItem);
        BongoContextMenu.Items.Add(modeMenu);

        // Scale Submenu
        var scaleMenu = new MenuItem { Header = "縮放比例 (Scale)" };
        double[] presetScales = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];
        foreach (var s in presetScales)
        {
            var item = new MenuItem
            {
                Header = $"{s * 100:0}%",
                IsChecked = Math.Abs(_config.Scale - s) < 0.01
            };
            double val = s;
            item.Click += (_, _) => ApplyScale(val);
            scaleMenu.Items.Add(item);
        }
        scaleMenu.Items.Add(new Separator());
        var customScaleItem = new MenuItem { Header = "自訂比例..." };
        customScaleItem.Click += (_, _) =>
        {
            var dlg = new ScaleInputDialog("Bongo 打字pet", _config.Scale) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                ApplyScale(dlg.ResultScaleRatio);
            }
        };
        scaleMenu.Items.Add(customScaleItem);
        BongoContextMenu.Items.Add(scaleMenu);

        BongoContextMenu.Items.Add(new Separator());

        // Speech bubble / message board controls (matching CharacterWindow)
        var alwaysShowBubbleItem = new MenuItem
        {
            Header = "永久顯示對話框",
            IsCheckable = true,
            IsChecked = _config.AlwaysShowBubble
        };
        alwaysShowBubbleItem.Click += (_, _) => SetAlwaysShowBubble(alwaysShowBubbleItem.IsChecked);
        BongoContextMenu.Items.Add(alwaysShowBubbleItem);

        var setQuoteItem = new MenuItem { Header = "設定對話文字..." };
        setQuoteItem.Click += (_, _) =>
        {
            string displayName = $"Bongo 打字pet ({_currentSkin?.Info.DisplayName ?? _config.CurrentSkinKey})";
            var dialog = new TextInputDialog(
                _config.CurrentSkinKey,
                CustomDialogueText,
                DialogueAlignment,
                DialogueFontSize,
                DialogueImageMaxWidth,
                DialogueImageMaxHeight,
                displayNameOverride: displayName)
            {
                Owner = this
            };
            if (dialog.ShowDialog() == true)
            {
                SetCustomText(
                    dialog.ResultText,
                    dialog.ResultAlignment,
                    dialog.ResultFontSize,
                    dialog.ResultImageMaxWidth,
                    dialog.ResultImageMaxHeight);
            }
        };
        BongoContextMenu.Items.Add(setQuoteItem);

        BongoContextMenu.Items.Add(new Separator());

        // Lock position toggle
        var lockItem = new MenuItem
        {
            Header = "鎖定位置 (禁止拖曳)",
            IsChecked = _config.IsLocked
        };
        lockItem.Click += (_, _) => SetLocked(!_config.IsLocked);
        BongoContextMenu.Items.Add(lockItem);

        // Click-through toggle
        var clickThroughItem = new MenuItem
        {
            Header = "滑鼠穿透 (點擊穿透)",
            IsChecked = _config.ClickThrough
        };
        clickThroughItem.Click += (_, _) => SetClickThrough(!_config.ClickThrough);
        BongoContextMenu.Items.Add(clickThroughItem);

        // Reset position
        var resetItem = new MenuItem { Header = "重設位置至右下角" };
        resetItem.Click += (_, _) => ResetPosition();
        BongoContextMenu.Items.Add(resetItem);

        BongoContextMenu.Items.Add(new Separator());

        // Hide
        var hideItem = new MenuItem { Header = "收起 Bongo 打字pet" };
        hideItem.Click += (_, _) => HideBongo();
        BongoContextMenu.Items.Add(hideItem);
    }

    protected override void OnClosed(EventArgs e)
    {
        _speedPollTimer.Stop();
        _mouseMotionTimer.Stop();
        _rightPawResetTimer.Stop();
        _bubbleTimer.Stop();
        _eatAnimationTimer.Stop();
        _bananaSwayTimer.Stop();
        _statsSaveDebounceTimer.Stop();
        _bananaTracker.SyncToConfig(_config);

        _keyboardHook.KeyDown -= OnGlobalKeyDown;
        _keyboardHook.KeyUp -= OnGlobalKeyUp;
        _mouseHook.MouseMove -= OnGlobalMouseMove;
        _mouseHook.MouseDown -= OnGlobalMouseDown;
        _mouseHook.MouseUp -= OnGlobalMouseUp;

        ClickThroughManager.Instance.Unregister(this);

        base.OnClosed(e);
    }
}
