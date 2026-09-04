// src/ChiikawaDesktopPet.Wpf/CharacterWindow.Menu.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ChiikawaDesktopPet.Wpf;

public partial class CharacterWindow
{
    private void ShowContextMenu()
    {
        var contextMenu = new ContextMenu();

        var hideAllItem = new MenuItem
        {
            Header = "一鍵隱藏"
        };
        hideAllItem.Click += (_, _) => App.HideAllCharactersStatic();
        contextMenu.Items.Add(hideAllItem);
        contextMenu.Items.Add(new Separator());

        string displayName = InstanceDisplayName;
        var titleItem = new MenuItem
        {
            Header = $"【{displayName}】",
            IsEnabled = false,
            FontWeight = System.Windows.FontWeights.Bold
        };
        contextMenu.Items.Add(titleItem);
        contextMenu.Items.Add(new Separator());

        var playMenu = new MenuItem { Header = "播放動畫" };
        foreach (var animName in AllAnimationNames())
        {
            var item = new MenuItem { Header = App.GetAnimationDisplayName(animName) };
            string nameCopy = animName;
            item.Click += (_, _) => PlayAnimationByName(nameCopy);
            playMenu.Items.Add(item);
        }

        if (CharacterName is "chiikawa" or "momonga")
        {
            playMenu.Items.Add(new Separator());
            var coopItem = new MenuItem { Header = "【雙人互動】飛撲蹭臉 (Chiikawa & Momonga)" };
            coopItem.Click += (_, _) =>
            {
                bool success = InteractionCoordinator.Instance.TriggerManualInteraction(this);
                if (!success)
                {
                    System.Windows.MessageBox.Show(
                        "所需角色不足（需要 Chiikawa 與 Momonga 同時在場）",
                        "雙人互動提示",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            };
            playMenu.Items.Add(coopItem);
        }

        contextMenu.Items.Add(playMenu);

        var defaultActionMenu = new MenuItem { Header = "預設動作" };
        var defaultIdleItem = new MenuItem
        {
            Header = "【預設待機】",
            IsCheckable = true,
            IsChecked = string.IsNullOrEmpty(_defaultAnimation)
        };
        defaultIdleItem.Click += (_, _) => SetDefaultAnimation(null);
        defaultActionMenu.Items.Add(defaultIdleItem);
        defaultActionMenu.Items.Add(new Separator());

        foreach (var animName in InPlaceAnimationNames())
        {
            var item = new MenuItem
            {
                Header = App.GetAnimationDisplayName(animName),
                IsCheckable = true,
                IsChecked = string.Equals(_defaultAnimation, animName, StringComparison.OrdinalIgnoreCase)
            };
            string nameCopy = animName;
            item.Click += (_, _) => SetDefaultAnimation(nameCopy);
            defaultActionMenu.Items.Add(item);
        }
        contextMenu.Items.Add(defaultActionMenu);

        var randomAnimItem = new MenuItem
        {
            Header = "隨機動作",
            IsCheckable = true,
            IsChecked = _randomAnimationsEnabled
        };
        randomAnimItem.Click += (_, _) => SetRandomAnimationsEnabled(randomAnimItem.IsChecked);
        contextMenu.Items.Add(randomAnimItem);

        var randomJumpItem = new MenuItem
        {
            Header = "允許隨機跳躍",
            IsCheckable = true,
            IsChecked = _jumpEnabled
        };
        randomJumpItem.Click += (_, _) => SetJumpEnabled(randomJumpItem.IsChecked);
        contextMenu.Items.Add(randomJumpItem);

        var alwaysShowBubbleItem = new MenuItem
        {
            Header = "永久顯示對話框",
            IsCheckable = true,
            IsChecked = _alwaysShowBubble
        };
        alwaysShowBubbleItem.Click += (_, _) => SetAlwaysShowBubble(alwaysShowBubbleItem.IsChecked);
        contextMenu.Items.Add(alwaysShowBubbleItem);

        var clickThroughItem = new MenuItem
        {
            Header = "滑鼠左鍵穿透",
            IsCheckable = true,
            IsChecked = _clickThrough
        };
        clickThroughItem.Click += (_, _) => SetClickThrough(clickThroughItem.IsChecked);
        contextMenu.Items.Add(clickThroughItem);

        var setQuoteItem = new MenuItem { Header = "設定對話文字..." };
        setQuoteItem.Click += (_, _) =>
        {
            var dialog = new TextInputDialog(CharacterName, _customText, DialogueAlignment, DialogueFontSize);
            if (dialog.ShowDialog() == true)
            {
                SetCustomText(dialog.ResultText, dialog.ResultAlignment, dialog.ResultFontSize);
            }
        };
        contextMenu.Items.Add(setQuoteItem);

        var scaleMenu = new MenuItem { Header = "調整角色比例" };
        var presetScales = new (string Label, double Ratio)[]
        {
            ("50%", 0.50),
            ("75%", 0.75),
            ("100%（預設）", 1.00),
            ("125%", 1.25),
            ("150%", 1.50),
            ("175%", 1.75),
            ("200%", 2.00)
        };

        foreach (var (label, ratio) in presetScales)
        {
            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = Math.Abs(ScaleRatio - ratio) < 0.01
            };
            double r = ratio;
            item.Click += (_, _) => SetScaleRatio(r);
            scaleMenu.Items.Add(item);
        }

        scaleMenu.Items.Add(new Separator());

        var customScaleItem = new MenuItem { Header = "自訂比例..." };
        customScaleItem.Click += (_, _) =>
        {
            var dialog = new ScaleInputDialog(CharacterName, ScaleRatio);
            if (dialog.ShowDialog() == true)
            {
                SetScaleRatio(dialog.ResultScaleRatio);
            }
        };
        scaleMenu.Items.Add(customScaleItem);
        contextMenu.Items.Add(scaleMenu);

        var opacityMenu = new MenuItem { Header = "調整角色透明度" };
        var presetOpacities = new (string Label, double Opacity)[]
        {
            ("100%（預設）", 1.00),
            ("80%", 0.80),
            ("60%", 0.60),
            ("40%", 0.40),
            ("20%", 0.20)
        };

        foreach (var (label, op) in presetOpacities)
        {
            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = Math.Abs(PetOpacity - op) < 0.01
            };
            double opCopy = op;
            item.Click += (_, _) => SetOpacity(opCopy, SyncBubbleOpacity);
            opacityMenu.Items.Add(item);
        }

        opacityMenu.Items.Add(new Separator());

        var customOpacityItem = new MenuItem { Header = "自訂透明度..." };
        customOpacityItem.Click += (_, _) =>
        {
            double originalOpacity = PetOpacity;
            bool originalSync = SyncBubbleOpacity;

            var dialog = new OpacityInputDialog(CharacterName, PetOpacity, SyncBubbleOpacity);
            dialog.PreviewChanged += (previewOp, previewSync) =>
            {
                SetOpacity(previewOp, previewSync);
            };

            if (dialog.ShowDialog() == true)
            {
                SetOpacity(dialog.ResultOpacity, dialog.ResultSyncBubble);
            }
            else
            {
                SetOpacity(originalOpacity, originalSync);
            }
        };
        opacityMenu.Items.Add(customOpacityItem);
        contextMenu.Items.Add(opacityMenu);

        contextMenu.Items.Add(new Separator());

        var sayHiItem = new MenuItem { Header = "打個招呼！" };
        sayHiItem.Click += (_, _) => SayHiRequested?.Invoke();
        contextMenu.Items.Add(sayHiItem);

        var kickItem = new MenuItem { Header = "踢出角色" };
        kickItem.Click += (_, _) => KickRequested?.Invoke();
        contextMenu.Items.Add(kickItem);

        ContextMenu = contextMenu;
        contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;

        IntPtr hwnd = Handle != IntPtr.Zero ? Handle : new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            if (ClickThrough)
            {
                NativeMethods.SetWindowClickThrough(hwnd, false);
            }
            NativeMethods.SetForegroundWindow(hwnd);
        }

        contextMenu.Opened += (_, _) =>
        {
            HasOpenContextMenu = true;
            var source = (HwndSource?)PresentationSource.FromVisual(contextMenu);
            ContextMenuHwnd = source?.Handle ?? IntPtr.Zero;
        };

        contextMenu.Closed += (_, _) =>
        {
            HasOpenContextMenu = false;
            ContextMenuHwnd = IntPtr.Zero;
            _isRightButtonDown = false;
            if (ClickThrough)
            {
                IntPtr h = Handle != IntPtr.Zero ? Handle : new WindowInteropHelper(this).Handle;
                if (h != IntPtr.Zero)
                {
                    NativeMethods.SetWindowClickThrough(h, true);
                }
            }
            if (!_isShuttingDown && !_isAnimating && !_isDragging)
            {
                EnterIdleState();
            }
        };

        HasOpenContextMenu = true;
        contextMenu.IsOpen = true;
    }

    public bool HasOpenContextMenu { get; private set; }
    public IntPtr ContextMenuHwnd { get; private set; }

    internal bool IsPointInsideContextMenu(NativeMethods.POINT pt)
    {
        if (!HasOpenContextMenu) return false;

        if (ContextMenuHwnd != IntPtr.Zero && NativeMethods.GetWindowRect(ContextMenuHwnd, out var rect))
        {
            return pt.X >= rect.Left && pt.X <= rect.Right && pt.Y >= rect.Top && pt.Y <= rect.Bottom;
        }

        return false;
    }
}

