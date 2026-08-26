// src/YahaPet.Wpf/CharacterWindow.Animation.cs
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YahaPet.Core;

namespace YahaPet.Wpf;

public partial class CharacterWindow
{
    private static readonly FrozenSet<string> ExcludedAnimationFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "walkleft", "walkright", "jumpleft", "jumpright", "falling", "bounce"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private void DiscoverOtherAnimations()
    {
        string animationsDir = Path.Combine(_assetRoot, "animations");
        if (!Directory.Exists(animationsDir))
        {
            _otherAnimationNames = [];
            return;
        }

        var list = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(animationsDir))
        {
            string name = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(name) && !ExcludedAnimationFolders.Contains(name))
            {
                list.Add(name);
            }
        }
        _otherAnimationNames = list;
    }

    public IReadOnlyList<string> InPlaceAnimationNames()
    {
        var names = new List<string>(_otherAnimationNames.Count + 1);
        if (Directory.Exists(Path.Combine(_assetRoot, "animations", "bounce")))
        {
            names.Add("bounce");
        }
        names.AddRange(_otherAnimationNames);
        return names;
    }

    public IReadOnlyList<string> AllAnimationNames()
    {
        var names = new List<string>(_otherAnimationNames.Count + 4);
        if (Directory.Exists(Path.Combine(_assetRoot, "animations", "bounce")))
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

    // True if this character can actually play both directions of a left/right pair --
    // either direction's animation FOLDER existing is enough (GetOrLoadFrames mirrors the
    // missing side), but a static SPRITE fallback (PlayJump's else-branch, which has no
    // mirroring) needs BOTH sprites, or the missing direction would throw
    // KeyNotFoundException the first time it's rolled.
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
        if (Directory.Exists(Path.Combine(_assetRoot, "animations", "bounce")))
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

        // If this direction's own folder is missing, check the OPPOSITE direction before
        // giving up -- e.g. Chiikawa only has animations/jumpright, no jumpleft.
        string ownFolder = Path.Combine(_assetRoot, "animations", animationName);
        bool ownExists = Directory.Exists(ownFolder);
        string? sourceFolder = ownExists ? ownFolder
            : isMirrorable ? Path.Combine(_assetRoot, "animations", mirroredName)
            : null;

        if (sourceFolder is null || !Directory.Exists(sourceFolder))
        {
            _frames[animationName] = [];
            return _frames[animationName];
        }

        var sourceFrames = SpriteLoader.LoadFrames(sourceFolder, _physicalCharacterWidth * 4, _physicalCharacterHeight * 4);
        var result = ownExists ? sourceFrames : sourceFrames.ConvertAll(SpriteLoader.Mirror);
        _frames[animationName] = result;

        // Populating both directions from a single decode if mirrored folder doesn't exist on disk
        if (isMirrorable && !Directory.Exists(Path.Combine(_assetRoot, "animations", mirroredName)))
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

