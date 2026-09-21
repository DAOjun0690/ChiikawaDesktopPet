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
    private readonly BongoConfig _config;
    private readonly BongoSkinManager _skinManager;
    private readonly GlobalKeyboardHook _keyboardHook;
    private readonly GlobalMouseHook _mouseHook;
    private readonly BongoSpeedTracker _speedTracker;

    private BongoSkin? _currentSkin;
    private readonly DispatcherTimer _rightPawResetTimer;
    private readonly DispatcherTimer _speedPollTimer;
    private readonly DispatcherTimer _mouseMotionTimer;

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

    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _contextMenuHwnd = IntPtr.Zero;

    // Active key and mouse down tracking for "hold down until release"
    private readonly HashSet<int> _activeRightKeys = [];
    private readonly HashSet<int> _activeLeftKeys = [];
    private bool _isMouseLeftDown;
    private bool _isMouseRightDown;

    public BongoConfig Config => _config;
    public event Action? VisibilityChanged;
    public event Action<bool>? ClickThroughChanged;

    public bool HasOpenContextMenu => BongoContextMenu.IsOpen;
    public bool IsPetHidden => _isBossKeyHidden || !IsVisible;
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
        ApplyScale(_config.Scale);
        ApplySkin(_config.CurrentSkinKey);
        ApplyInitialPosition();

        _keyboardHook.KeyDown += OnGlobalKeyDown;
        _keyboardHook.KeyUp += OnGlobalKeyUp;
        _mouseHook.MouseMove += OnGlobalMouseMove;
        _mouseHook.MouseDown += OnGlobalMouseDown;
        _mouseHook.MouseUp += OnGlobalMouseUp;

        _speedPollTimer.Start();
        _mouseMotionTimer.Start();

        RebuildContextMenu();
    }

    public void ApplyInitialPosition()
    {
        var workArea = SystemParameters.WorkArea;
        double defaultWidth = 280 * _config.Scale;
        double defaultHeight = 240 * _config.Scale;

        if (_config.PositionX == -1 && _config.PositionY == -1)
        {
            Left = Math.Max(0, workArea.Right - defaultWidth - 30);
            Top = Math.Max(0, workArea.Bottom - defaultHeight - 30);
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
        _currentSkin = _skinManager.LoadSkin(skinKey);
        _config.CurrentSkinKey = _currentSkin.Info.Key;

        BodyImage.Source = _currentSkin.Body;
        LeftPawImage.Source = _currentSkin.LeftPawUp;
        RightPawImage.Source = _currentSkin.RightPawUp;
        FaceImage.Source = _currentSkin.FaceNormal;
        EffectImage.Source = _currentSkin.OverdriveEffect;
        EffectImage.Visibility = Visibility.Collapsed;

        RebuildContextMenu();
    }

    public void ApplyScale(double scale)
    {
        scale = Math.Clamp(scale, 0.2, 4.0);
        _config.Scale = scale;
        WindowScaleTransform.ScaleX = scale;
        WindowScaleTransform.ScaleY = scale;
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
        });
    }

    private void OnGlobalKeyUp(int vkCode)
    {
        if (_isBossKeyHidden || !IsVisible) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
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
        });
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
        if (_currentSkin == null) return;

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

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_config.IsLocked) return;
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        ClampToScreen();
        _config.PositionX = Left;
        _config.PositionY = Top;
    }

    private void ClampToScreen()
    {
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;
        double windowWidth = ActualWidth > 0 ? ActualWidth : 280 * _config.Scale;
        double windowHeight = ActualHeight > 0 ? ActualHeight : 220 * _config.Scale;

        double maxRight = vLeft + vWidth - windowWidth;
        double maxBottom = vTop + vHeight - windowHeight;

        Left = Math.Clamp(Left, vLeft, Math.Max(vLeft, maxRight));
        Top = Math.Clamp(Top, vTop, Math.Max(vTop, maxBottom));
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

    public void PauseForBossKey()
    {
        _isBossKeyHidden = true;
        ResetInteractionState();
        Visibility = Visibility.Collapsed;
        VisibilityChanged?.Invoke();
    }

    public void ResumeFromBossKey()
    {
        _isBossKeyHidden = false;
        if (_config.IsEnabled)
        {
            Visibility = Visibility.Visible;
            _speedPollTimer.Start();
            _mouseMotionTimer.Start();
        }
        VisibilityChanged?.Invoke();
    }

    public void ShowBongo()
    {
        _config.IsEnabled = true;
        Visibility = Visibility.Visible;
        if (!_keyboardHook.IsActive) _keyboardHook.Start();
        if (!_mouseHook.IsActive) _mouseHook.Start();
        _speedPollTimer.Start();
        _mouseMotionTimer.Start();
        VisibilityChanged?.Invoke();
    }

    public void HideBongo()
    {
        _config.IsEnabled = false;
        Visibility = Visibility.Collapsed;
        ResetInteractionState();
        VisibilityChanged?.Invoke();
    }

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

        _keyboardHook.KeyDown -= OnGlobalKeyDown;
        _keyboardHook.KeyUp -= OnGlobalKeyUp;
        _mouseHook.MouseMove -= OnGlobalMouseMove;
        _mouseHook.MouseDown -= OnGlobalMouseDown;
        _mouseHook.MouseUp -= OnGlobalMouseUp;

        ClickThroughManager.Instance.Unregister(this);

        base.OnClosed(e);
    }
}
