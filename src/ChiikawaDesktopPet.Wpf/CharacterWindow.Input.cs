// src/ChiikawaDesktopPet.Wpf/CharacterWindow.Input.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ChiikawaDesktopPet.Core;

namespace ChiikawaDesktopPet.Wpf;

public partial class CharacterWindow
{
    private System.Windows.Point _mouseDownScreenPos;
    private bool _hasDragged;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_clickThrough) return;
        _hasDragged = false;
        _mouseDownScreenPos = PointToScreen(e.GetPosition(this));
        _dragOffset = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_clickThrough) return;
        if (!IsMouseCaptured) return;

        if (!_hasDragged)
        {
            var currentScreen = PointToScreen(e.GetPosition(this));
            if (Math.Abs(currentScreen.X - _mouseDownScreenPos.X) > 5 ||
                Math.Abs(currentScreen.Y - _mouseDownScreenPos.Y) > 5)
            {
                _hasDragged = true;
                _isDragging = true;
                TalkActionTimer?.Stop();
                TalkActionTimer = null;
                _idleTimer.Stop();
                _frameTimer.Stop();
                _loopCurrentAnimation = false;
                BeginAnimation(TopProperty, null);
                BeginAnimation(LeftProperty, null);
                _isAnimating = false;
                _isWalking = false;
                _isJumping = false;
                DetachFromWindow();
                _isFalling = false;
                _isShaking = false;
                _grabbedSprite = RandomFrom(_sprites, "grabbed");
                SetSprite(_grabbedSprite);
                _holdTimer.Start();
            }
            else
            {
                return;
            }
        }

        if (!_isDragging) return;

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

        var clamped = BehaviorPlanner.ClampToBounds(candidate, bounds, (int)Width, (int)Height);

        if (_isShaking)
        {
            Left = clamped.X + Random.Shared.Next(0, 11);
            Top = clamped.Y + Random.Shared.Next(0, 11);
        }
        else
        {
            Left = clamped.X;
            Top = clamped.Y;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_clickThrough) return;
        ReleaseMouseCapture();
        _holdTimer.Stop();

        if (_hasDragged)
        {
            _isDragging = false;
            _isShaking = false;
            _grabbedSprite = null;

            if (TryFindSnapTargetWindow(out var targetHwnd, out var targetRect))
            {
                AttachToWindow(targetHwnd, targetRect);
            }
            else
            {
                FallTo();
            }
        }
        else
        {
            _isDragging = false;
            _isShaking = false;
            _grabbedSprite = null;

            if (_alwaysShowBubble)
            {
                // 永久顯示對話框已勾選時，左鍵點擊觸發下一個隨機動作
                PlayRandomAction();
            }
            else
            {
                // 永久顯示對話框未勾選時：
                if (HasCustomText)
                {
                    // 顯示對話框（或刷新顯示時間），當前動作繼續進行不中斷
                    ShowSpeechBubble(3500);
                }
                else
                {
                    // 無自訂文字時，觸發下一個隨機動作
                    PlayRandomAction();
                }
            }
        }
    }

    private bool TryFindSnapTargetWindow(out IntPtr targetHwnd, out NativeMethods.RECT targetRect)
    {
        targetHwnd = IntPtr.Zero;
        targetRect = default;

        double scale = GetDipScale();
        double petBottomDip = Top + Height;
        double petCenterXDip = Left + Width / 2.0;

        int physicalCenterX = (int)(petCenterXDip / scale);
        int physicalBottomY = (int)(petBottomDip / scale);

        var thisHwnd = new WindowInteropHelper(this).Handle;

        IntPtr foundHwnd = NativeMethods.FindTopLevelWindowForSnap(
            physicalCenterX,
            physicalBottomY,
            dipTolerance: 30.0,
            dipScale: scale,
            currentPetHwnd: thisHwnd,
            isPetWindowPredicate: static hwnd =>
            {
                if (Application.Current == null) return false;
                foreach (Window window in Application.Current.Windows)
                {
                    if (new WindowInteropHelper(window).Handle == hwnd) return true;
                }
                return false;
            },
            out var rect);

        if (foundHwnd != IntPtr.Zero)
        {
            targetHwnd = foundHwnd;
            targetRect = rect;
            return true;
        }

        return false;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRightButtonDown = false;
        if (_isShuttingDown) return;
        e.Handled = true;

        TalkActionTimer?.Stop();
        TalkActionTimer = null;

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
}
