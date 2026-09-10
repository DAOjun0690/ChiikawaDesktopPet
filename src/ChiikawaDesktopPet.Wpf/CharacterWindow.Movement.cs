// src/ChiikawaDesktopPet.Wpf/CharacterWindow.Movement.cs
using System;
using System.Windows;
using System.Windows.Media.Animation;
using ChiikawaDesktopPet.Core;

namespace ChiikawaDesktopPet.Wpf;

public partial class CharacterWindow
{
    internal bool TryGetAttachedWindowBounds(out NativeMethods.RECT rect)
    {
        if (_attachedHwnd is { } hwnd)
        {
            return NativeMethods.TryGetWindowBounds(hwnd, out rect);
        }
        rect = default;
        return false;
    }

    internal void AttachToWindow(IntPtr hwnd, NativeMethods.RECT rect)
    {
        _attachedHwnd = hwnd;
        double scale = GetDipScale();
        double winLeft = rect.Left * scale;
        double winTop = rect.Top * scale;

        if (BubbleContainer.Visibility == System.Windows.Visibility.Visible && HasCustomText)
        {
            double bubbleH = BubbleContainer.ActualHeight > 0 ? BubbleContainer.ActualHeight : BubbleContainer.DesiredSize.Height;
            if (bubbleH <= 0)
            {
                BubbleContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                bubbleH = BubbleContainer.DesiredSize.Height;
            }
            UpdateBubblePlacement(bubbleH);
        }
        Top = CurrentBubblePlacement == SpeechBubblePlacement.Top ? (winTop - Height) : (winTop - _currentSpriteHeight);
        _attachedRelativeX = Left - winLeft;
        _isFalling = false;
        _isWalking = false;
        _isJumping = false;
        _windowTrackingTimer.Start();
        EnterIdleState();
    }

    internal void DetachFromWindow()
    {
        _attachedHwnd = null;
        _windowTrackingTimer.Stop();
        _isWalking = false;
        _isJumping = false;
    }

    internal void DetachAndFall()
    {
        if (_isFalling && _attachedHwnd == null) return;
        DetachFromWindow();
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        _frameTimer.Stop();
        _loopCurrentAnimation = false;
        FallTo();
    }

    private void OnWindowTrackingTick()
    {
        if (_attachedHwnd is not { } hwnd || !NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd))
        {
            DetachAndFall();
            return;
        }

        if (NativeMethods.IsZoomed(hwnd))
        {
            DetachAndFall();
            return;
        }

        if (!NativeMethods.TryGetWindowBounds(hwnd, out var rect))
        {
            DetachAndFall();
            return;
        }

        double scale = GetDipScale();
        double winLeftDip = rect.Left * scale;
        double winTopDip = rect.Top * scale;
        double winRightDip = rect.Right * scale;

        // Squeezed against the top of the monitor / screen
        if (BehaviorPlanner.IsWindowSqueezed((int)winTopDip, 0))
        {
            DetachAndFall();
            return;
        }

        if (!_isDragging && !_isFalling)
        {
            if (BubbleContainer.Visibility == System.Windows.Visibility.Visible && HasCustomText)
            {
                double bubbleH = BubbleContainer.ActualHeight > 0 ? BubbleContainer.ActualHeight : BubbleContainer.DesiredSize.Height;
                if (bubbleH <= 0)
                {
                    BubbleContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    bubbleH = BubbleContainer.DesiredSize.Height;
                }
                UpdateBubblePlacement(bubbleH);
            }
            double attachedTop = CurrentBubblePlacement == SpeechBubblePlacement.Top ? (winTopDip - Height) : (winTopDip - _currentSpriteHeight);

            if (_isWalking)
            {
                // If walking along the window top edge, check if stepped off
                if (BehaviorPlanner.IsSteppedOffWindow((int)Left, (int)Width, (int)winLeftDip, (int)winRightDip))
                {
                    BeginAnimation(LeftProperty, null);
                    DetachAndFall();
                    return;
                }
                Top = attachedTop;
            }
            else if (_isJumping)
            {
                // Jump handles its own arc animations
            }
            else
            {
                // Idle, talking, custom animation, or pinned mode (random animations disabled)
                Top = attachedTop;
                Left = winLeftDip + _attachedRelativeX;

                // Check if window resize left the pet stranded outside
                if (BehaviorPlanner.IsSteppedOffWindow((int)Left, (int)Width, (int)winLeftDip, (int)winRightDip))
                {
                    DetachAndFall();
                    return;
                }
            }
        }
    }

    private void FallTo()
    {
        _isAnimating = true;
        _isFalling = true;

        // SystemParameters.PrimaryScreenHeight/WorkArea always reflect the PRIMARY monitor.
        // Determine the monitor the window is actually on instead (same approach as the
        // drag clamp in OnMouseMove), so a fall computed after a drag-release onto a
        // secondary (possibly taller) monitor doesn't compute a negative duration -- which
        // throws ArgumentException when converted to a Duration and crashes the process --
        // or a landing point off that monitor.
        double dipScale = GetDipScale();
        var screenPoint = new System.Drawing.Point((int)(Left / dipScale), (int)(Top / dipScale));
        var primary = System.Windows.Forms.Screen.PrimaryScreen;
        var screen = (primary != null) ? System.Windows.Forms.Screen.FromPoint(screenPoint) : null;

        int screenHeight = (int)((screen?.Bounds.Bottom ?? (int)SystemParameters.PrimaryScreenHeight) * dipScale);
        int landingY = (int)((screen?.WorkingArea.Bottom ?? (int)SystemParameters.PrimaryScreenHeight) * dipScale);

        var currentPos = new PetPoint((int)Left, (int)Top);

        var outcome = BehaviorPlanner.PlanFall(
            currentPos,
            screenHeight: screenHeight,
            landingY: landingY,
            characterHeight: (int)Height,
            SystemRandomSource.Shared);

        SetSprite(RandomFrom(_sprites, "falling"));
        AnimatePosition(new PetPoint((int)Left, outcome.LandingPoint.Y), outcome.DurationMs, onComplete: () =>
        {
            _isFalling = false;
            if (outcome.Crashed)
            {
                _isAnimating = false;
                _isWalking = false;
                _isJumping = false;
                SetSprite(_sprites.TryGetValue("fallingend", out var fallingEndSprite) ? fallingEndSprite : null);
                if (_randomAnimationsEnabled && !_isShuttingDown && !_isDragging && !_isInteracting)
                {
                    StartIdleTimer();
                }
            }
            else
            {
                EnterIdleState();
            }
        });
    }

    private void AnimatePosition(PetPoint target, int durationMs, Action? onComplete)
    {
        var animation = new DoubleAnimation(Top, target.Y, TimeSpan.FromMilliseconds(durationMs));
        animation.Completed += (_, _) =>
        {
            // Release the animation's hold on TopProperty before reassigning, otherwise
            // WPF's property-value precedence keeps the animation in control and the
            // reassignment (and any later manual assignment, e.g. from OnMouseMove) is a
            // silent no-op.
            BeginAnimation(TopProperty, null);
            Top = target.Y;
            onComplete?.Invoke();
        };
        BeginAnimation(TopProperty, animation);
        Left = target.X;
    }

    private void PlayWalk(BehaviorPlanner.WalkDirection? forcedDirection = null)
    {
        var (minX, maxX) = GetWalkJumpXBoundsInDips();
        int planMinX = minX;
        int planMaxX = maxX;
        if (_attachedHwnd != null)
        {
            // Expand walk range beyond window edges so random walk can step off
            planMinX = minX - (int)Width - 80;
            planMaxX = maxX + (int)Width + 80;
        }

        var plan = BehaviorPlanner.PlanWalk(new PetPoint((int)Left, (int)Top), planMinX, planMaxX, (int)Width, SystemRandomSource.Shared, forcedDirection);
        if (plan is null)
        {
            if (_randomAnimationsEnabled && !_isShuttingDown && !_isDragging && !_isFalling && !_isInteracting)
            {
                StartIdleTimer();
            }
            return;
        }

        _isAnimating = true;
        _isWalking = true;
        _loopCurrentAnimation = true;
        string animationName = plan.Direction == BehaviorPlanner.WalkDirection.Left ? "walkleft" : "walkright";
        PlayFrameSequence(animationName, onComplete: static () => { });

        var animation = new DoubleAnimation(Left, plan.TargetX, TimeSpan.FromMilliseconds(plan.DurationMs));
        animation.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            Left = plan.TargetX;
            _isWalking = false;
            _loopCurrentAnimation = false;
            _frameTimer.Stop();

            if (_attachedHwnd is { } hwnd && TryGetAttachedWindowBounds(out var currentRect))
            {
                double scale = GetDipScale();
                double wLeft = currentRect.Left * scale;
                double wRight = currentRect.Right * scale;
                if (BehaviorPlanner.IsSteppedOffWindow((int)Left, (int)Width, (int)wLeft, (int)wRight))
                {
                    DetachAndFall();
                    return;
                }
                _attachedRelativeX = Left - wLeft;
            }

            EnterIdleState();
        };
        BeginAnimation(LeftProperty, animation);
    }

    private void PlayJump(BehaviorPlanner.JumpDirection? forcedDirection = null)
    {
        var (minX, maxX) = GetWalkJumpXBoundsInDips();
        double dipScale = GetDipScale();
        int charHeight = (int)_currentSpriteHeight;
        double currentTop = double.IsNaN(Top) ? 0 : Top;
        double currentLeft = double.IsNaN(Left) ? 0 : Left;

        int planMinX = minX;
        int planMaxX = maxX;
        int effectiveLandingY;

        if (_attachedHwnd is { } hwnd && TryGetAttachedWindowBounds(out var rect))
        {
            planMinX = minX - (int)Width - 60;
            planMaxX = maxX + (int)Width + 60;
            _jumpStartingFeetY = rect.Top * dipScale;
            bool isBubbleAbove = (CurrentBubblePlacement == SpeechBubblePlacement.Top && BubbleContainer.Visibility == Visibility.Visible && Height > charHeight);
            effectiveLandingY = isBubbleAbove
                ? (int)_jumpStartingFeetY - (int)Height + charHeight
                : (int)_jumpStartingFeetY;
        }
        else
        {
            // Ground anchor: the character's ground line is its starting feet position.
            // Since PlanJump computes landTarget.Y = effectiveLandingY - characterHeight,
            // setting effectiveLandingY = (int)Math.Round(currentTop) + charHeight guarantees
            // landTarget.Y = (int)Math.Round(currentTop), exactly returning to the starting ground!
            bool isBubbleAbove = (CurrentBubblePlacement == SpeechBubblePlacement.Top && BubbleContainer.Visibility == Visibility.Visible && Height > charHeight);
            _jumpStartingFeetY = isBubbleAbove ? (currentTop + Height) : (currentTop + charHeight);
            effectiveLandingY = (int)Math.Round(currentTop) + charHeight;
        }

        var plan = BehaviorPlanner.PlanJump(
            new PetPoint((int)Left, (int)Top),
            charHeight,
            planMinX,
            planMaxX - (int)Width,
            effectiveLandingY,
            SystemRandomSource.Shared,
            forcedDirection);
        _isAnimating = true;
        _isFalling = true;
        _isJumping = true;

        string animationName = plan.Direction == BehaviorPlanner.JumpDirection.Left ? "jumpleft" : "jumpright";
        var frames = GetOrLoadFrames(animationName);

        var riseAnimation = new DoubleAnimation(currentTop, plan.RiseTarget.Y, TimeSpan.FromMilliseconds(plan.DurationMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var riseAnimationX = new DoubleAnimation(currentLeft, plan.RiseTarget.X, TimeSpan.FromMilliseconds(plan.DurationMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        if (frames.Count > 0)
        {
            _loopCurrentAnimation = true;
            PlayFrameSequence(animationName, onComplete: static () => { });
        }
        else
        {
            // Hachiware has no animated jump frames — use the single static sprite, matching
            // the original's fallback path.
            SetSprite(_sprites.TryGetValue(animationName, out var jumpSprite) ? jumpSprite : null);
        }

        riseAnimation.Completed += (_, _) =>
        {
            double targetLandingTop;
            if (_attachedHwnd is { } attachedHwnd && TryGetAttachedWindowBounds(out var currentRect))
            {
                double scale = GetDipScale();
                double winTopDip = currentRect.Top * scale;
                bool isBubbleAbove = (CurrentBubblePlacement == SpeechBubblePlacement.Top && BubbleContainer.Visibility == Visibility.Visible && Height > _currentSpriteHeight);
                targetLandingTop = isBubbleAbove ? (winTopDip - Height) : (winTopDip - _currentSpriteHeight);
            }
            else
            {
                bool isBubbleAbove = (CurrentBubblePlacement == SpeechBubblePlacement.Top && BubbleContainer.Visibility == Visibility.Visible && Height > _currentSpriteHeight);
                targetLandingTop = isBubbleAbove ? (_jumpStartingFeetY - Height) : (_jumpStartingFeetY - _currentSpriteHeight);
            }

            var landAnimation = new DoubleAnimation(plan.RiseTarget.Y, targetLandingTop, TimeSpan.FromMilliseconds(plan.DurationMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var landAnimationX = new DoubleAnimation(plan.RiseTarget.X, plan.LandTarget.X, TimeSpan.FromMilliseconds(plan.DurationMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            landAnimation.Completed += (_, _) =>
            {
                CompleteJumpLanding(plan.LandTarget.X);
            };
            BeginAnimation(TopProperty, landAnimation);
            BeginAnimation(LeftProperty, landAnimationX);
        };
        BeginAnimation(TopProperty, riseAnimation);
        BeginAnimation(LeftProperty, riseAnimationX);
    }

    internal void CompleteJumpLanding(int landTargetX)
    {
        BeginAnimation(TopProperty, null);
        BeginAnimation(LeftProperty, null);

        double finalLandingTop;
        if (_attachedHwnd is { } attachedHwnd && TryGetAttachedWindowBounds(out var landingRect))
        {
            double scale = GetDipScale();
            double winTopDip = landingRect.Top * scale;
            bool isBubbleAbove = (CurrentBubblePlacement == SpeechBubblePlacement.Top && BubbleContainer.Visibility == Visibility.Visible && Height > _currentSpriteHeight);
            finalLandingTop = isBubbleAbove ? (winTopDip - Height) : (winTopDip - _currentSpriteHeight);
            Top = finalLandingTop;
            Left = landTargetX;
            _loopCurrentAnimation = false;
            _isFalling = false;
            _isJumping = false;
            _frameTimer.Stop();

            if (_pendingHideBubbleAfterJump)
            {
                _pendingHideBubbleAfterJump = false;
                if (!_alwaysShowBubble)
                {
                    HideSpeechBubble();
                }
            }

            double wLeft = landingRect.Left * scale;
            double wRight = landingRect.Right * scale;
            if (BehaviorPlanner.IsSteppedOffWindow((int)Left, (int)Width, (int)wLeft, (int)wRight))
            {
                DetachAndFall();
                return;
            }
            _attachedRelativeX = Left - wLeft;
        }
        else
        {
            bool isBubbleAbove = (CurrentBubblePlacement == SpeechBubblePlacement.Top && BubbleContainer.Visibility == Visibility.Visible && Height > _currentSpriteHeight);
            finalLandingTop = isBubbleAbove ? (_jumpStartingFeetY - Height) : (_jumpStartingFeetY - _currentSpriteHeight);
            Top = finalLandingTop;
            Left = landTargetX;
            _loopCurrentAnimation = false;
            _isFalling = false;
            _isJumping = false;
            _frameTimer.Stop();

            if (_pendingHideBubbleAfterJump)
            {
                _pendingHideBubbleAfterJump = false;
                if (!_alwaysShowBubble)
                {
                    HideSpeechBubble();
                }
            }
        }

        EnterIdleState();
    }

    public void SmoothMoveTo(PetPoint target, Action? onArrived)
    {
        _idleTimer.Stop();
        _isAnimating = true;
        _loopCurrentAnimation = true;
        string animName = target.X < Left ? "walkleft" : "walkright";
        PlayFrameSequence(animName, static () => { });

        double dist = Math.Sqrt(Math.Pow(target.X - Left, 2) + Math.Pow(target.Y - Top, 2));
        int durationMs = Math.Max(300, (int)(dist * 3.5));

        var animX = new DoubleAnimation(Left, target.X, TimeSpan.FromMilliseconds(durationMs));
        var animY = new DoubleAnimation(Top, target.Y, TimeSpan.FromMilliseconds(durationMs));

        animX.Completed += (_, _) =>
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = target.X;
            Top = target.Y;
            _isAnimating = false;
            _loopCurrentAnimation = false;
            _frameTimer.Stop();
            if (onArrived != null)
            {
                onArrived.Invoke();
            }
            else
            {
                EnterIdleState();
            }
        };

        BeginAnimation(LeftProperty, animX);
        BeginAnimation(TopProperty, animY);
    }
}
