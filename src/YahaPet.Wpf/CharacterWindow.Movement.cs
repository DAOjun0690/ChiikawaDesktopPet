// src/YahaPet.Wpf/CharacterWindow.Movement.cs
using System;
using System.Windows.Media.Animation;
using YahaPet.Core;

namespace YahaPet.Wpf;

public partial class CharacterWindow
{
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
            characterHeight: (int)Height,
            SystemRandomSource.Shared);

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
        var plan = BehaviorPlanner.PlanWalk(new PetPoint((int)Left, (int)Top), minX, maxX, (int)Width, SystemRandomSource.Shared, forcedDirection);
        if (plan is null) return;

        _isAnimating = true;
        _loopCurrentAnimation = true;
        string animationName = plan.Direction == BehaviorPlanner.WalkDirection.Left ? "walkleft" : "walkright";
        PlayFrameSequence(animationName, onComplete: static () => { });

        var animation = new DoubleAnimation(Left, plan.TargetX, TimeSpan.FromMilliseconds(plan.DurationMs));
        animation.Completed += (_, _) =>
        {
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
            (int)Height,
            minX,
            maxX - (int)Width,
            landingY,
            SystemRandomSource.Shared,
            forcedDirection);
        _isAnimating = true;
        _isFalling = true;

        string animationName = plan.Direction == BehaviorPlanner.JumpDirection.Left ? "jumpleft" : "jumpright";
        var frames = GetOrLoadFrames(animationName);

        var riseAnimation = new DoubleAnimation(Top, plan.RiseTarget.Y, TimeSpan.FromMilliseconds(plan.DurationMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var riseAnimationX = new DoubleAnimation(Left, plan.RiseTarget.X, TimeSpan.FromMilliseconds(plan.DurationMs))
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
            SetSprite(_sprites[animationName]);
        }

        riseAnimation.Completed += (_, _) =>
        {
            var landAnimation = new DoubleAnimation(Top, plan.LandTarget.Y, TimeSpan.FromMilliseconds(plan.DurationMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var landAnimationX = new DoubleAnimation(Left, plan.LandTarget.X, TimeSpan.FromMilliseconds(plan.DurationMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            landAnimation.Completed += (_, _) =>
            {
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
}

