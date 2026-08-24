// src/YahaPet.Wpf/CharacterWindow.Input.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using YahaPet.Core;

namespace YahaPet.Wpf;

public partial class CharacterWindow
{
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TalkActionTimer != null)
        {
            TalkActionTimer.Stop();
            TalkActionTimer = null;
            _isAnimating = false;
        }

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
        CaptureMouse();
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

