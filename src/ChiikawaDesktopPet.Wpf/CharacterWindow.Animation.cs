// src/ChiikawaDesktopPet.Wpf/CharacterWindow.Animation.cs
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ChiikawaDesktopPet.Core;

namespace ChiikawaDesktopPet.Wpf;

public partial class CharacterWindow
{
    private static readonly FrozenSet<string> ExcludedAnimationFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "walkleft", "walkright", "jumpleft", "jumpright", "falling", "bounce"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private void DiscoverOtherAnimations()
    {
        _otherAnimationNames = _assetPackage.DiscoverAnimationNames(ExcludedAnimationFolders).ToList();
    }

    public IReadOnlyList<string> InPlaceAnimationNames()
    {
        var names = new List<string>(_otherAnimationNames.Count + 1);
        if (_assetPackage.HasAnimation("bounce"))
        {
            names.Add("bounce");
        }
        names.AddRange(_otherAnimationNames);
        return names;
    }

    public IReadOnlyList<string> AllAnimationNames()
    {
        var names = new List<string>(_otherAnimationNames.Count + 4);
        if (_assetPackage.HasAnimation("bounce"))
        {
            names.Add("bounce");
        }
        names.AddRange(_otherAnimationNames);
        names.Add("walkleft");
        names.Add("walkright");

        if (HasDirectionalCapability("jumpleft", "jumpright"))
        {
            names.Add("jumpleft");
            names.Add("jumpright");
        }
        return names;
    }

    private bool HasDirectionalCapability(string leftName, string rightName)
    {
        return _assetPackage.HasDirectionalCapability(leftName, rightName);
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

    public void PlayRandomAction()
    {
        if (_isDragging) return;

        // Stop current animation/movement to switch immediately
        _idleTimer.Stop();
        _frameTimer.Stop();
        _loopCurrentAnimation = false;
        double currentTop = Top;
        double currentLeft = Left;
        BeginAnimation(TopProperty, null);
        BeginAnimation(LeftProperty, null);
        Top = currentTop;
        Left = currentLeft;
        _isAnimating = false;
        _isFalling = false;
        _isWalking = false;
        _isJumping = false;

        var candidateActions = new List<Action>();

        // Walk
        candidateActions.Add(() => PlayWalk());

        // Jump (if enabled and capable)
        if (_jumpEnabled && HasDirectionalCapability("jumpleft", "jumpright"))
        {
            candidateActions.Add(() => PlayJump());
        }

        // Special animations
        foreach (var animName in _otherAnimationNames)
        {
            string nameCopy = animName;
            candidateActions.Add(() => PlayNamedAnimation(nameCopy));
        }

        // Bounce
        if (_assetPackage.HasAnimation("bounce"))
        {
            candidateActions.Add(() => PlayAnimationByName("bounce"));
        }

        if (candidateActions.Count > 0)
        {
            var chosen = candidateActions[Random.Shared.Next(candidateActions.Count)];
            chosen();
        }
        else
        {
            EnterIdleState();
        }
    }

    private void PlayTimedAnimation(string animationName, int durationMs)
    {
        _isAnimating = true;
        _loopCurrentAnimation = true;
        PlayFrameSequence(animationName, onComplete: static () => { });

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            EnterIdleState();
        };
        timer.Start();
    }

    private static bool TryGetMirroredAnimationName(string animationName, out string mirroredName)
    {
        bool isWalkOrJump = animationName.StartsWith("walk", StringComparison.OrdinalIgnoreCase) ||
                            animationName.StartsWith("jump", StringComparison.OrdinalIgnoreCase);
        if (isWalkOrJump && animationName.Length > 4 && animationName.EndsWith("left", StringComparison.OrdinalIgnoreCase))
        {
            mirroredName = string.Concat(animationName.AsSpan(0, animationName.Length - 4), "right");
            return true;
        }
        if (isWalkOrJump && animationName.Length > 5 && animationName.EndsWith("right", StringComparison.OrdinalIgnoreCase))
        {
            mirroredName = string.Concat(animationName.AsSpan(0, animationName.Length - 5), "left");
            return true;
        }
        mirroredName = "";
        return false;
    }

    private List<BitmapSource> GetOrLoadFrames(string animationName)
    {
        if (_frames.TryGetValue(animationName, out var cached)) return cached;

        bool isMirrorable = TryGetMirroredAnimationName(animationName, out string mirroredName);

        bool ownExists = _assetPackage.HasAnimation(animationName);
        bool mirrorExists = isMirrorable && _assetPackage.HasAnimation(mirroredName);

        if (!ownExists && !mirrorExists)
        {
            _frames[animationName] = [];
            return _frames[animationName];
        }

        string sourceAnim = ownExists ? animationName : mirroredName;
        var sourceFrames = _assetPackage.LoadAnimationFrames(sourceAnim, _physicalCharacterWidth * 4, _physicalCharacterHeight * 4);
        var result = ownExists ? sourceFrames : sourceFrames.ConvertAll(SpriteLoader.Mirror);
        _frames[animationName] = result;

        // Populating both directions from a single decode if mirrored animation does not exist
        if (isMirrorable && !mirrorExists)
        {
            _frames[mirroredName] = ownExists ? sourceFrames.ConvertAll(SpriteLoader.Mirror) : sourceFrames;
        }

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
        _frameTimer.Start();
    }

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

    private void PlayNamedAnimation(string animationName)
    {
        _isAnimating = true;
        _loopCurrentAnimation = false;
        PlayFrameSequence(animationName, onComplete: () =>
        {
            EnterIdleState();
        });
    }
}

