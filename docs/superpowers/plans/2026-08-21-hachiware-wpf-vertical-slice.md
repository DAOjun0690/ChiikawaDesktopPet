# Hachiware WPF Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a C#/WPF system-tray application that can spawn and fully control the Hachiware character — autonomous movement, drag/drop, tray controls, config-driven timing — matching the corrected behavior of the Python/PyQt6 original, plus a validated asset-optimization pipeline run against Hachiware's own asset set.

**Architecture:** A `ChiikawaDesktopPet.Core` class library holds all decision logic (autonomous action rolls, jump/walk trajectory planning, screen clamping, fall outcome, config lookup) as pure, UI-free functions taking an injectable random source — this is the project's one testing seam. `ChiikawaDesktopPet.Wpf` is a lean code-behind WPF app (no MVVM, no DI) with a tray icon (via WinForms `NotifyIcon` interop) and one borderless, always-on-top, transparent `CharacterWindow` per spawned character, which calls into `ChiikawaDesktopPet.Core` for every decision and only performs the resulting window moves/frame swaps itself. `ChiikawaDesktopPet.AssetPipeline` is a standalone console tool that resizes, recompresses, and frame-resamples a character's PNG animation set into `assets/optimized/<character>/`, validated here against Hachiware's small asset set before it's later pointed at Usagi's much larger one.

**Tech Stack:** .NET 10 (`net10.0-windows`), WPF, `System.Windows.Forms.NotifyIcon` (via `UseWindowsForms`), `System.Media.SoundPlayer`, `System.Text.Json`, `System.Drawing.Common` (asset pipeline only), xUnit.

**Spec:** [docs/specs/hachiware-wpf-port.md](../../specs/hachiware-wpf-port.md)

## Global Constraints

- Target framework: `net10.0-windows`, WPF, Windows-only. Framework-dependent deployment — target machines already have a .NET Desktop Runtime, so no self-contained bundling or runtime-install-prompt handling is in scope.
- Architecture: exactly one pure-logic seam (`ChiikawaDesktopPet.Core`, no WPF/WinForms/System.Drawing references) with all randomness routed through an injectable `IRandomSource`. Everything else is direct, lean WPF code-behind — no MVVM, no DI container.
- Autonomous behavior uses a **corrected** weighted roll — jump 10% / walk 40% / other named animation 45% / no-op 5% — deliberately diverging from the shipped Python version's `roll <= 100` bug, which makes every autonomous tick a jump (see spec User Story 8a).
- The "co-op animation" feature from the Python original is permanently out of scope — no module, stub, or menu entry is created for it.
- Asset pipeline output goes to `assets/optimized/<character>/`; the original `assets/<character>/` tree is never modified by the pipeline.
- Asset bake target: resize so no frame's longer side exceeds **320px**, chosen to comfortably cover on-screen sizes up to a 1440p display (character height ≈144px, or ≈216px for the 1.5×-scaled "dance"-style animations) with headroom for quality, while still shrinking oversized sources. Some softness on 4K displays is an accepted tradeoff (per spec).
- Config file `config.json` deserializes as `Dictionary<string, CharacterConfig>` keyed by lowercase character name (the JSON root **is** the character map, no wrapping key) — default FPS is 40 for any character/animation not present.
- **Known Hachiware asset-set limitations** (verified directly against `assets/hachiware/`, not assumed): there is **no `sounds/` folder at all** for Hachiware, so the sound-playback code paths can only be verified via their graceful-no-file no-op behavior, not by actually hearing anything — real audio verification has to wait for the Usagi slice. There is only **one animation folder** (`walkleft`, 72 frames, 2.8MB) — no `dance`/other named-animation folders, so the "other named animation" 45%-roll branch will always resolve to a no-op for Hachiware specifically (this is correct behavior given the asset set, not a bug). There are no `jumpleft`/`jumpright` **animation** folders either, only static `jumpleft.png`/`jumpright.png` **sprites** — so Hachiware's jump always takes the original's "static sprite" fallback path, never the per-frame animated jump path. Source frames are already ~160×144px, i.e. already at/below the 320px bake target, so the resize step of the asset pipeline will mostly no-op on Hachiware (pass the file through unchanged) — meaningful resize savings only show up once the same pipeline is later run against Usagi's 640×360–735×702px sources. The frame-count-resampling step, however, is meaningfully exercised here (72-frame `walkleft`).

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `src/YahaPet.sln`
- Create: `src/YahaPet.Core/YahaPet.Core.csproj`
- Create: `src/YahaPet.Core/SystemRandomSource.cs`
- Create: `src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
- Create: `src/YahaPet.Core.Tests/SystemRandomSourceTests.cs`

**Interfaces:**
- Produces: `IRandomSource` (to be defined fully in Task 2, but the file/interface skeleton and `SystemRandomSource` implementation are created here since they have no dependency on the behavior logic itself).

- [ ] **Step 1: Scaffold the solution and projects**

Run:
```bash
cd d:/Project/Yaha-Pet
mkdir -p src
dotnet new sln -n YahaPet -o src
dotnet new classlib -n YahaPet.Core -o src/YahaPet.Core -f net8.0
dotnet new xunit -n YahaPet.Core.Tests -o src/YahaPet.Core.Tests -f net8.0
dotnet sln src/YahaPet.sln add src/YahaPet.Core/YahaPet.Core.csproj src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj
dotnet add src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj reference src/YahaPet.Core/YahaPet.Core.csproj
rm src/YahaPet.Core/Class1.cs
```

- [ ] **Step 2: Write `IRandomSource` and `SystemRandomSource`**

```csharp
// src/YahaPet.Core/IRandomSource.cs
namespace YahaPet.Core;

/// Abstraction over System.Random.Next(int,int) so behavior logic can be
/// tested with fixed roll sequences instead of real randomness.
public interface IRandomSource
{
    /// Returns a value in [minInclusive, maxExclusive), matching System.Random.Next(int,int).
    int Next(int minInclusive, int maxExclusive);
}
```

```csharp
// src/YahaPet.Core/SystemRandomSource.cs
namespace YahaPet.Core;

public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random = new();

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
```

- [ ] **Step 3: Write the failing test**

```csharp
// src/YahaPet.Core.Tests/SystemRandomSourceTests.cs
using YahaPet.Core;
using Xunit;

public class SystemRandomSourceTests
{
    [Fact]
    public void Next_ReturnsValueWithinRequestedRange()
    {
        var source = new SystemRandomSource();
        for (int i = 0; i < 1000; i++)
        {
            int value = source.Next(5, 10);
            Assert.InRange(value, 5, 9);
        }
    }
}
```

- [ ] **Step 4: Run the test**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: PASS (this is a smoke test proving the solution/project/test harness is wired correctly before any real behavior logic is added).

- [ ] **Step 5: Commit**

```bash
git add src/YahaPet.sln src/YahaPet.Core src/YahaPet.Core.Tests
git commit -m "chore: scaffold YahaPet.Core solution and test project"
```

---

### Task 2: BehaviorPlanner — autonomous action selection

**Files:**
- Create: `src/YahaPet.Core/AutonomousAction.cs`
- Create: `src/YahaPet.Core/BehaviorPlanner.cs`
- Create: `src/YahaPet.Core.Tests/BehaviorPlannerActionTests.cs`

**Interfaces:**
- Consumes: `IRandomSource` (Task 1).
- Produces: `AutonomousActionKind` enum, `AutonomousAction` record, `BehaviorPlanner.ChooseAutonomousAction(IReadOnlyList<string>, IRandomSource) -> AutonomousAction`, `BehaviorPlanner.NextIdleIntervalMs(IRandomSource) -> int`.

- [ ] **Step 1: Write the failing tests**

```csharp
// src/YahaPet.Core.Tests/BehaviorPlannerActionTests.cs
using System.Collections.Generic;
using YahaPet.Core;
using Xunit;

public class BehaviorPlannerActionTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;
        public FixedRandomSource(params int[] values) => _values = new Queue<int>(values);
        public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
    }

    [Theory]
    [InlineData(0, AutonomousActionKind.Jump)]
    [InlineData(9, AutonomousActionKind.Jump)]
    [InlineData(10, AutonomousActionKind.Walk)]
    [InlineData(49, AutonomousActionKind.Walk)]
    [InlineData(95, AutonomousActionKind.NoOp)]
    [InlineData(99, AutonomousActionKind.NoOp)]
    public void ChooseAutonomousAction_UsesLayeredRollBoundaries(int roll, AutonomousActionKind expectedKind)
    {
        var random = new FixedRandomSource(roll);
        var result = BehaviorPlanner.ChooseAutonomousAction(new List<string> { "dance" }, random);
        Assert.Equal(expectedKind, result.Kind);
    }

    [Fact]
    public void ChooseAutonomousAction_MidRangeRoll_PicksNamedAnimation()
    {
        // roll=50 selects the "other animation" branch, then a second roll picks index 1 ("tapdance").
        var random = new FixedRandomSource(50, 1);
        var result = BehaviorPlanner.ChooseAutonomousAction(new List<string> { "dance", "tapdance" }, random);
        Assert.Equal(AutonomousActionKind.PlayAnimation, result.Kind);
        Assert.Equal("tapdance", result.AnimationName);
    }

    [Fact]
    public void ChooseAutonomousAction_MidRangeRoll_EmptyAnimationList_IsNoOp()
    {
        // Matches Hachiware's real asset set: no named animations besides walkleft/jump.
        var random = new FixedRandomSource(60);
        var result = BehaviorPlanner.ChooseAutonomousAction(new List<string>(), random);
        Assert.Equal(AutonomousActionKind.NoOp, result.Kind);
    }

    [Fact]
    public void NextIdleIntervalMs_ReturnsValueFromRandomSource()
    {
        var random = new FixedRandomSource(4321);
        Assert.Equal(4321, BehaviorPlanner.NextIdleIntervalMs(random));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: FAIL to compile — `AutonomousActionKind`, `AutonomousAction`, and `BehaviorPlanner` do not exist yet.

- [ ] **Step 3: Implement**

```csharp
// src/YahaPet.Core/AutonomousAction.cs
namespace YahaPet.Core;

public enum AutonomousActionKind { Jump, Walk, PlayAnimation, NoOp }

public sealed record AutonomousAction(AutonomousActionKind Kind, string? AnimationName = null);
```

```csharp
// src/YahaPet.Core/BehaviorPlanner.cs
namespace YahaPet.Core;

/// Pure, UI-free decision logic for autonomous pet behavior. Every method here
/// takes an IRandomSource explicitly so callers can test with fixed roll sequences.
public static partial class BehaviorPlanner
{
    /// Corrected weighted roll (jump 10% / walk 40% / other 45% / no-op 5%).
    /// ponytail: the shipped Python original has `roll <= 100` (always true for a
    /// 0-99 roll), so it only ever jumps autonomously — deliberately not reproduced,
    /// see spec User Story 8a.
    public static AutonomousAction ChooseAutonomousAction(IReadOnlyList<string> otherAnimationNames, IRandomSource random)
    {
        int roll = random.Next(0, 100);
        if (roll < 10) return new AutonomousAction(AutonomousActionKind.Jump);
        if (roll < 50) return new AutonomousAction(AutonomousActionKind.Walk);
        if (roll < 95)
        {
            if (otherAnimationNames.Count == 0) return new AutonomousAction(AutonomousActionKind.NoOp);
            int index = random.Next(0, otherAnimationNames.Count);
            return new AutonomousAction(AutonomousActionKind.PlayAnimation, otherAnimationNames[index]);
        }
        return new AutonomousAction(AutonomousActionKind.NoOp);
    }

    public static int NextIdleIntervalMs(IRandomSource random) => random.Next(3000, 10000);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/YahaPet.Core/AutonomousAction.cs src/YahaPet.Core/BehaviorPlanner.cs src/YahaPet.Core.Tests/BehaviorPlannerActionTests.cs
git commit -m "feat: add autonomous action selection to BehaviorPlanner"
```

---

### Task 3: BehaviorPlanner — jump trajectory planning

**Files:**
- Create: `src/YahaPet.Core/PetPoint.cs`
- Create: `src/YahaPet.Core/BehaviorPlanner.Jump.cs`
- Create: `src/YahaPet.Core.Tests/BehaviorPlannerJumpTests.cs`

**Interfaces:**
- Consumes: `IRandomSource` (Task 1).
- Produces: `PetPoint` record struct, `BehaviorPlanner.JumpDirection` enum, `BehaviorPlanner.JumpPlan` record, `BehaviorPlanner.PlanJump(PetPoint currentPos, int characterHeight, int screenWidth, int landingY, IRandomSource random) -> JumpPlan`.

- [ ] **Step 1: Write the failing tests**

```csharp
// src/YahaPet.Core.Tests/BehaviorPlannerJumpTests.cs
using System.Collections.Generic;
using YahaPet.Core;
using Xunit;

public class BehaviorPlannerJumpTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;
        public FixedRandomSource(params int[] values) => _values = new Queue<int>(values);
        public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
    }

    [Fact]
    public void PlanJump_MidScreen_GoingLeft_ComputesExpectedTargetsAndDuration()
    {
        // directionRoll=0 (left), offset roll=40, jumpHeight roll=100
        var random = new FixedRandomSource(0, 40, 100);
        var plan = BehaviorPlanner.PlanJump(
            currentPos: new PetPoint(500, 800),
            characterHeight: 100,
            screenWidth: 1920,
            landingY: 1000,
            random: random);

        Assert.Equal(BehaviorPlanner.JumpDirection.Left, plan.Direction);
        // endRangeX = 500 - 40 = 460; distanceX = 40; firstHalfX = |460 + 20| = 480
        Assert.Equal(480, plan.RiseTarget.X);
        Assert.Equal(800 - 100 - 100, plan.RiseTarget.Y); // currentY - jumpHeight - characterHeight
        Assert.Equal(1000 - 100, plan.LandTarget.Y);      // landingY - characterHeight
        Assert.Equal(460, plan.LandTarget.X);
        Assert.Equal(1000, plan.DurationMs);              // jumpHeight(100) * 10
    }

    [Fact]
    public void PlanJump_TooCloseToRightEdge_ForcesLeftInstead()
    {
        // directionRoll=1 (right), but position is within 100px of the right edge -> forced to 0 (left)
        var random = new FixedRandomSource(1, 10, 50);
        var plan = BehaviorPlanner.PlanJump(
            currentPos: new PetPoint(1850, 800),
            characterHeight: 100,
            screenWidth: 1920,
            landingY: 1000,
            random: random);

        Assert.Equal(BehaviorPlanner.JumpDirection.Left, plan.Direction);
        Assert.True(plan.LandTarget.X < 1850);
    }

    [Fact]
    public void PlanJump_TooCloseToLeftEdge_ReplicatesOriginalDirectionMismatchQuirk()
    {
        // directionRoll=0 (left) but currentPos.X <= 100 -> original falls into the "go right"
        // math branch for end_range_x while still labeling the animation "jumpleft". This is a
        // faithful port of that original quirk, not a new bug — see Task 3 notes.
        var random = new FixedRandomSource(0, 20, 60);
        var plan = BehaviorPlanner.PlanJump(
            currentPos: new PetPoint(50, 800),
            characterHeight: 100,
            screenWidth: 1920,
            landingY: 1000,
            random: random);

        Assert.Equal(BehaviorPlanner.JumpDirection.Left, plan.Direction); // animation label
        Assert.Equal(70, plan.LandTarget.X); // actual movement: 50 + 20 = 70 (rightward)
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: FAIL to compile — `PetPoint`, `BehaviorPlanner.JumpDirection`/`JumpPlan`/`PlanJump` do not exist yet.

- [ ] **Step 3: Implement**

```csharp
// src/YahaPet.Core/PetPoint.cs
namespace YahaPet.Core;

public readonly record struct PetPoint(int X, int Y);
```

```csharp
// src/YahaPet.Core/BehaviorPlanner.Jump.cs
using System;

namespace YahaPet.Core;

public static partial class BehaviorPlanner
{
    public enum JumpDirection { Left, Right }

    public sealed record JumpPlan(
        JumpDirection Direction,
        int DurationMs,
        PetPoint RiseTarget,
        PetPoint LandTarget);

    /// Faithful port of the original's jump direction/edge-avoidance logic, including its
    /// known quirk: when rolled "left" but too close to the left edge, the animation still
    /// plays as "jumpleft" even though the computed movement goes right. This mirrors the
    /// shipped Python behavior exactly (chosen_direction always follows direction_roll,
    /// independent of which end_range_x branch actually ran).
    public static JumpPlan PlanJump(PetPoint currentPos, int characterHeight, int screenWidth, int landingY, IRandomSource random)
    {
        int directionRoll = random.Next(0, 2);
        if (directionRoll == 1 && currentPos.X >= screenWidth - 100)
            directionRoll = 0;

        int endRangeX;
        if (directionRoll == 0 && currentPos.X > 100)
        {
            endRangeX = currentPos.X - random.Next(0, 101);
            if (endRangeX > screenWidth) endRangeX = screenWidth - 1;
        }
        else
        {
            endRangeX = currentPos.X + random.Next(0, 101);
            if (endRangeX < 0) endRangeX = 1;
        }

        var direction = directionRoll == 0 ? JumpDirection.Left : JumpDirection.Right;

        int jumpHeight = random.Next(50, 301);
        int distanceX = Math.Abs(endRangeX - currentPos.X);
        int firstHalfX = direction == JumpDirection.Left
            ? Math.Abs(endRangeX + distanceX / 2)
            : Math.Abs(endRangeX - distanceX / 2);

        int durationMs = jumpHeight * 10;
        var riseTarget = new PetPoint(firstHalfX, currentPos.Y - jumpHeight - characterHeight);
        var landTarget = new PetPoint(endRangeX, landingY - characterHeight);

        return new JumpPlan(direction, durationMs, riseTarget, landTarget);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/YahaPet.Core/PetPoint.cs src/YahaPet.Core/BehaviorPlanner.Jump.cs src/YahaPet.Core.Tests/BehaviorPlannerJumpTests.cs
git commit -m "feat: add jump trajectory planning to BehaviorPlanner"
```

---

### Task 4: BehaviorPlanner — walk trajectory planning

**Files:**
- Create: `src/YahaPet.Core/BehaviorPlanner.Walk.cs`
- Create: `src/YahaPet.Core.Tests/BehaviorPlannerWalkTests.cs`

**Interfaces:**
- Consumes: `PetPoint` (Task 3), `IRandomSource` (Task 1).
- Produces: `BehaviorPlanner.WalkDirection` enum, `BehaviorPlanner.WalkPlan` record, `BehaviorPlanner.PlanWalk(PetPoint currentPos, int screenWidth, int characterWidth, IRandomSource random) -> WalkPlan?`.

- [ ] **Step 1: Write the failing tests**

```csharp
// src/YahaPet.Core.Tests/BehaviorPlannerWalkTests.cs
using System.Collections.Generic;
using YahaPet.Core;
using Xunit;

public class BehaviorPlannerWalkTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;
        public FixedRandomSource(params int[] values) => _values = new Queue<int>(values);
        public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
    }

    [Fact]
    public void PlanWalk_RollLeft_TargetWithinZeroToCurrentMinusMargin()
    {
        // rollDirection=0 (left); target-x roll returns 200 (must be in [0, currentX-100)=[0,400))
        var random = new FixedRandomSource(0, 200);
        var plan = BehaviorPlanner.PlanWalk(new PetPoint(500, 800), screenWidth: 1920, characterWidth: 100, random);

        Assert.NotNull(plan);
        Assert.Equal(BehaviorPlanner.WalkDirection.Left, plan!.Direction);
        Assert.Equal(200, plan.TargetX);
        Assert.Equal(5 * 300, plan.DurationMs); // 5 * |200 - 500|
    }

    [Fact]
    public void PlanWalk_RollRight_TargetWithinCurrentPlusMarginToScreenEdge()
    {
        var random = new FixedRandomSource(1, 700);
        var plan = BehaviorPlanner.PlanWalk(new PetPoint(500, 800), screenWidth: 1920, characterWidth: 100, random);

        Assert.NotNull(plan);
        Assert.Equal(BehaviorPlanner.WalkDirection.Right, plan!.Direction);
        Assert.Equal(700, plan.TargetX);
    }

    [Fact]
    public void PlanWalk_TooCloseToLeftEdge_ReturnsNull()
    {
        // rollDirection=0 (left); startRange=0, endRange = currentX(50) - 100 = -50 -> infeasible
        var random = new FixedRandomSource(0);
        var plan = BehaviorPlanner.PlanWalk(new PetPoint(50, 800), screenWidth: 1920, characterWidth: 100, random);

        Assert.Null(plan);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: FAIL to compile — `BehaviorPlanner.WalkDirection`/`WalkPlan`/`PlanWalk` do not exist yet.

- [ ] **Step 3: Implement**

```csharp
// src/YahaPet.Core/BehaviorPlanner.Walk.cs
using System;

namespace YahaPet.Core;

public static partial class BehaviorPlanner
{
    public enum WalkDirection { Left, Right }

    public sealed record WalkPlan(WalkDirection Direction, int TargetX, int DurationMs);

    /// Returns null when no valid walk range exists (character too close to the
    /// rolled-direction edge), matching the original's silent no-op in that case.
    /// ponytail: unlike the original, this does not fall back to the opposite
    /// direction's frames when one animation folder is missing — the WPF port
    /// always auto-mirrors walkleft/walkright (see Task 10), so both directions
    /// are always available and that fallback branch has no work left to do.
    public static WalkPlan? PlanWalk(PetPoint currentPos, int screenWidth, int characterWidth, IRandomSource random)
    {
        const int minMovementDistance = 100;
        int rollDirection = random.Next(0, 2);

        int startRange, endRange;
        WalkDirection direction;
        if (rollDirection == 0)
        {
            startRange = 0;
            endRange = currentPos.X - minMovementDistance;
            direction = WalkDirection.Left;
        }
        else
        {
            startRange = currentPos.X + minMovementDistance;
            endRange = screenWidth - characterWidth;
            direction = WalkDirection.Right;
        }

        if (startRange >= endRange) return null;

        int targetX = random.Next(startRange, endRange);
        int durationMs = 5 * Math.Abs(targetX - currentPos.X);
        return new WalkPlan(direction, targetX, durationMs);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/YahaPet.Core/BehaviorPlanner.Walk.cs src/YahaPet.Core.Tests/BehaviorPlannerWalkTests.cs
git commit -m "feat: add walk trajectory planning to BehaviorPlanner"
```

---

### Task 5: BehaviorPlanner — screen clamping and fall outcome

**Files:**
- Create: `src/YahaPet.Core/PetBounds.cs`
- Create: `src/YahaPet.Core/BehaviorPlanner.Clamp.cs`
- Create: `src/YahaPet.Core/BehaviorPlanner.Fall.cs`
- Create: `src/YahaPet.Core.Tests/BehaviorPlannerClampAndFallTests.cs`

**Interfaces:**
- Consumes: `PetPoint` (Task 3), `IRandomSource` (Task 1).
- Produces: `PetBounds` record struct, `BehaviorPlanner.ClampToBounds(PetPoint, PetBounds, int width, int height) -> PetPoint`, `BehaviorPlanner.FallOutcome` record, `BehaviorPlanner.PlanFall(PetPoint currentPos, int screenHeight, int landingY, int characterHeight, IRandomSource random) -> FallOutcome`.

- [ ] **Step 1: Write the failing tests**

```csharp
// src/YahaPet.Core.Tests/BehaviorPlannerClampAndFallTests.cs
using System.Collections.Generic;
using YahaPet.Core;
using Xunit;

public class BehaviorPlannerClampAndFallTests
{
    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;
        public FixedRandomSource(params int[] values) => _values = new Queue<int>(values);
        public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
    }

    [Fact]
    public void ClampToBounds_PointInsideBounds_IsUnchanged()
    {
        var bounds = new PetBounds(Left: 0, Top: 0, Right: 1920, Bottom: 1040);
        var result = BehaviorPlanner.ClampToBounds(new PetPoint(500, 500), bounds, width: 100, height: 100);
        Assert.Equal(new PetPoint(500, 500), result);
    }

    [Fact]
    public void ClampToBounds_PointBeyondRightEdge_IsClampedWithFivePixelMargin()
    {
        var bounds = new PetBounds(Left: 0, Top: 0, Right: 1920, Bottom: 1040);
        var result = BehaviorPlanner.ClampToBounds(new PetPoint(2000, 500), bounds, width: 100, height: 100);
        Assert.Equal(1920 - 100 + 5, result.X);
    }

    [Fact]
    public void ClampToBounds_PointBeyondBottomEdge_IsClampedWithOnePixelMargin()
    {
        var bounds = new PetBounds(Left: 0, Top: 0, Right: 1920, Bottom: 1040);
        var result = BehaviorPlanner.ClampToBounds(new PetPoint(500, 2000), bounds, width: 100, height: 100);
        Assert.Equal(1040 - 100 + 1, result.Y);
    }

    [Fact]
    public void PlanFall_LowRoll_IsCrash()
    {
        var random = new FixedRandomSource(30);
        var outcome = BehaviorPlanner.PlanFall(new PetPoint(500, 200), screenHeight: 1080, landingY: 1040, characterHeight: 100, random);
        Assert.True(outcome.Crashed);
        Assert.Equal(new PetPoint(500, 940), outcome.LandingPoint);
        Assert.Equal((int)(1.5 * (1080 - 200)), outcome.DurationMs);
    }

    [Fact]
    public void PlanFall_HighRoll_IsNormalLanding()
    {
        var random = new FixedRandomSource(31);
        var outcome = BehaviorPlanner.PlanFall(new PetPoint(500, 200), screenHeight: 1080, landingY: 1040, characterHeight: 100, random);
        Assert.False(outcome.Crashed);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: FAIL to compile — `PetBounds`, `ClampToBounds`, `FallOutcome`, `PlanFall` do not exist yet.

- [ ] **Step 3: Implement**

```csharp
// src/YahaPet.Core/PetBounds.cs
namespace YahaPet.Core;

public readonly record struct PetBounds(int Left, int Top, int Right, int Bottom);
```

```csharp
// src/YahaPet.Core/BehaviorPlanner.Clamp.cs
using System;

namespace YahaPet.Core;

public static partial class BehaviorPlanner
{
    /// Faithful port of the original's clamp_to_screen: keeps a 5px margin on the
    /// right/width axis and a 1px margin on the bottom/height axis, matching the
    /// original's asymmetric constants exactly (not a rounding artifact).
    public static PetPoint ClampToBounds(PetPoint point, PetBounds availableBounds, int width, int height)
    {
        int x = Math.Max(availableBounds.Left, Math.Min(point.X, availableBounds.Right - width + 5));
        int y = Math.Max(availableBounds.Top, Math.Min(point.Y, availableBounds.Bottom - height + 1));
        return new PetPoint(x, y);
    }
}
```

```csharp
// src/YahaPet.Core/BehaviorPlanner.Fall.cs
namespace YahaPet.Core;

public static partial class BehaviorPlanner
{
    public sealed record FallOutcome(bool Crashed, PetPoint LandingPoint, int DurationMs);

    /// Crash odds match the original's random.randint(0,100) <= 30 (31/101 ≈ 30.7%,
    /// documented in the spec as "roughly 30%").
    public static FallOutcome PlanFall(PetPoint currentPos, int screenHeight, int landingY, int characterHeight, IRandomSource random)
    {
        int durationMs = (int)(1.5 * (screenHeight - currentPos.Y));
        bool crashed = random.Next(0, 101) <= 30;
        var landingPoint = new PetPoint(currentPos.X, landingY - characterHeight);
        return new FallOutcome(crashed, landingPoint, durationMs);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/YahaPet.Core/PetBounds.cs src/YahaPet.Core/BehaviorPlanner.Clamp.cs src/YahaPet.Core/BehaviorPlanner.Fall.cs src/YahaPet.Core.Tests/BehaviorPlannerClampAndFallTests.cs
git commit -m "feat: add screen clamping and fall outcome to BehaviorPlanner"
```

---

### Task 6: Config loading and FPS lookup

**Files:**
- Create: `src/YahaPet.Core/AnimationConfig.cs`
- Create: `src/YahaPet.Core/CharacterConfig.cs`
- Create: `src/YahaPet.Core/BehaviorPlanner.Config.cs`
- Create: `src/YahaPet.Core/ConfigLoader.cs`
- Create: `src/YahaPet.Core.Tests/ConfigTests.cs`

**Interfaces:**
- Produces: `AnimationConfig`, `CharacterConfig`, `BehaviorPlanner.DefaultFps` constant, `BehaviorPlanner.GetFps(IReadOnlyDictionary<string, CharacterConfig>?, string characterName, string animationName) -> int`, `ConfigLoader.Load(string path) -> Dictionary<string, CharacterConfig>`.

- [ ] **Step 1: Write the failing tests**

```csharp
// src/YahaPet.Core.Tests/ConfigTests.cs
using System.Collections.Generic;
using System.IO;
using YahaPet.Core;
using Xunit;

public class ConfigTests
{
    [Fact]
    public void GetFps_NullConfig_ReturnsDefault()
    {
        Assert.Equal(40, BehaviorPlanner.GetFps(null, "hachiware", "walkleft"));
    }

    [Fact]
    public void GetFps_CharacterNotPresent_ReturnsDefault()
    {
        var config = new Dictionary<string, CharacterConfig>();
        Assert.Equal(40, BehaviorPlanner.GetFps(config, "hachiware", "walkleft"));
    }

    [Fact]
    public void GetFps_AnimationPresent_ReturnsConfiguredValue()
    {
        var config = new Dictionary<string, CharacterConfig>
        {
            ["hachiware"] = new CharacterConfig
            {
                Animations = new Dictionary<string, AnimationConfig>
                {
                    ["walkleft"] = new AnimationConfig { Fps = 24 }
                }
            }
        };
        Assert.Equal(24, BehaviorPlanner.GetFps(config, "hachiware", "walkleft"));
    }

    [Fact]
    public void ConfigLoader_Load_MissingFile_ReturnsEmptyDictionary()
    {
        var result = ConfigLoader.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + System.Guid.NewGuid() + ".json"));
        Assert.Empty(result);
    }

    [Fact]
    public void ConfigLoader_Load_ValidFile_ParsesCharacterMap()
    {
        string path = Path.Combine(Path.GetTempPath(), "yahapet-config-" + System.Guid.NewGuid() + ".json");
        File.WriteAllText(path, """
        {
          "hachiware": { "animations": { "walkleft": { "fps": 24 } } }
        }
        """);
        try
        {
            var result = ConfigLoader.Load(path);
            Assert.Equal(24, BehaviorPlanner.GetFps(result, "hachiware", "walkleft"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConfigLoader_Load_MalformedJson_ReturnsEmptyDictionary()
    {
        string path = Path.Combine(Path.GetTempPath(), "yahapet-config-" + System.Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{ not valid json");
        try
        {
            var result = ConfigLoader.Load(path);
            Assert.Empty(result);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: FAIL to compile — none of the config types exist yet.

- [ ] **Step 3: Implement**

```csharp
// src/YahaPet.Core/AnimationConfig.cs
namespace YahaPet.Core;

public sealed class AnimationConfig
{
    public int Fps { get; set; } = 40;
}
```

```csharp
// src/YahaPet.Core/CharacterConfig.cs
using System;
using System.Collections.Generic;

namespace YahaPet.Core;

public sealed class CharacterConfig
{
    public Dictionary<string, AnimationConfig> Animations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
```

```csharp
// src/YahaPet.Core/BehaviorPlanner.Config.cs
using System.Collections.Generic;

namespace YahaPet.Core;

public static partial class BehaviorPlanner
{
    public const int DefaultFps = 40;

    public static int GetFps(IReadOnlyDictionary<string, CharacterConfig>? config, string characterName, string animationName)
    {
        if (config is null) return DefaultFps;
        if (!config.TryGetValue(characterName, out var character)) return DefaultFps;
        if (!character.Animations.TryGetValue(animationName, out var anim)) return DefaultFps;
        return anim.Fps;
    }
}
```

```csharp
// src/YahaPet.Core/ConfigLoader.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YahaPet.Core;

/// Reads config.json, whose root object IS the character map (no wrapping key),
/// e.g. { "hachiware": { "animations": { "walkleft": { "fps": 24 } } } }.
public static class ConfigLoader
{
    public static Dictionary<string, CharacterConfig> Load(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, CharacterConfig>(StringComparer.OrdinalIgnoreCase);

        try
        {
            string json = File.ReadAllText(path);
            var result = JsonSerializer.Deserialize<Dictionary<string, CharacterConfig>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new Dictionary<string, CharacterConfig>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, CharacterConfig>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/YahaPet.Core.Tests/YahaPet.Core.Tests.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/YahaPet.Core/AnimationConfig.cs src/YahaPet.Core/CharacterConfig.cs src/YahaPet.Core/BehaviorPlanner.Config.cs src/YahaPet.Core/ConfigLoader.cs src/YahaPet.Core.Tests/ConfigTests.cs
git commit -m "feat: add config.json loading and per-animation FPS lookup"
```

---

### Task 7: Asset pipeline — resize/recompress tool

**Files:**
- Create: `src/YahaPet.AssetPipeline/YahaPet.AssetPipeline.csproj`
- Create: `src/YahaPet.AssetPipeline/ImageOptimizer.cs`
- Create: `src/YahaPet.AssetPipeline/Program.cs`
- Create: `src/YahaPet.AssetPipeline.Tests/YahaPet.AssetPipeline.Tests.csproj`
- Create: `src/YahaPet.AssetPipeline.Tests/ImageOptimizerTests.cs`

**Interfaces:**
- Produces: `ImageOptimizer.OptimizeImage(string sourcePath, string outputPath, int maxDimension)`, `ImageOptimizer.OptimizeDirectory(string sourceDir, string outputDir, int maxDimension) -> int`.

- [ ] **Step 1: Scaffold the projects**

Run:
```bash
dotnet new console -n YahaPet.AssetPipeline -o src/YahaPet.AssetPipeline -f net8.0-windows
dotnet new xunit -n YahaPet.AssetPipeline.Tests -o src/YahaPet.AssetPipeline.Tests -f net8.0-windows
dotnet sln src/YahaPet.sln add src/YahaPet.AssetPipeline/YahaPet.AssetPipeline.csproj src/YahaPet.AssetPipeline.Tests/YahaPet.AssetPipeline.Tests.csproj
dotnet add src/YahaPet.AssetPipeline.Tests/YahaPet.AssetPipeline.Tests.csproj reference src/YahaPet.AssetPipeline/YahaPet.AssetPipeline.csproj
dotnet add src/YahaPet.AssetPipeline/YahaPet.AssetPipeline.csproj package System.Drawing.Common
dotnet add src/YahaPet.AssetPipeline.Tests/YahaPet.AssetPipeline.Tests.csproj package System.Drawing.Common
rm src/YahaPet.AssetPipeline.Tests/UnitTest1.cs
```

- [ ] **Step 2: Write the failing tests**

```csharp
// src/YahaPet.AssetPipeline.Tests/ImageOptimizerTests.cs
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using YahaPet.AssetPipeline;
using Xunit;

public class ImageOptimizerTests
{
    private static string CreateTempPng(int width, int height)
    {
        string path = Path.Combine(Path.GetTempPath(), $"pipeline-test-{Guid.NewGuid()}.png");
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.CornflowerBlue);
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    [Fact]
    public void OptimizeImage_LargerThanTarget_IsDownscaledPreservingAspectRatio()
    {
        string source = CreateTempPng(400, 300);
        string output = Path.Combine(Path.GetTempPath(), $"pipeline-out-{Guid.NewGuid()}.png");
        try
        {
            ImageOptimizer.OptimizeImage(source, output, maxDimension: 200);
            using var result = new Bitmap(output);
            Assert.Equal(200, result.Width);
            Assert.Equal(150, result.Height);
        }
        finally
        {
            File.Delete(source);
            File.Delete(output);
        }
    }

    [Fact]
    public void OptimizeImage_AlreadyWithinTarget_IsCopiedUnchanged()
    {
        string source = CreateTempPng(100, 80);
        string output = Path.Combine(Path.GetTempPath(), $"pipeline-out-{Guid.NewGuid()}.png");
        try
        {
            ImageOptimizer.OptimizeImage(source, output, maxDimension: 320);
            using var result = new Bitmap(output);
            Assert.Equal(100, result.Width);
            Assert.Equal(80, result.Height);
        }
        finally
        {
            File.Delete(source);
            File.Delete(output);
        }
    }

    [Fact]
    public void OptimizeDirectory_ProcessesAllPngsRecursively()
    {
        string sourceDir = Path.Combine(Path.GetTempPath(), $"pipeline-src-{Guid.NewGuid()}");
        string outputDir = Path.Combine(Path.GetTempPath(), $"pipeline-dst-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(sourceDir, "walkleft"));
        try
        {
            File.Copy(CreateTempPng(400, 300), Path.Combine(sourceDir, "walkleft", "1.png"));
            File.Copy(CreateTempPng(400, 300), Path.Combine(sourceDir, "walkleft", "2.png"));

            int count = ImageOptimizer.OptimizeDirectory(sourceDir, outputDir, maxDimension: 200);

            Assert.Equal(2, count);
            Assert.True(File.Exists(Path.Combine(outputDir, "walkleft", "1.png")));
            Assert.True(File.Exists(Path.Combine(outputDir, "walkleft", "2.png")));
        }
        finally
        {
            Directory.Delete(sourceDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test src/YahaPet.AssetPipeline.Tests/YahaPet.AssetPipeline.Tests.csproj`
Expected: FAIL to compile — `ImageOptimizer` does not exist yet.

- [ ] **Step 4: Implement**

```csharp
// src/YahaPet.AssetPipeline/ImageOptimizer.cs
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace YahaPet.AssetPipeline;

public static class ImageOptimizer
{
    public static int OptimizeDirectory(string sourceDir, string outputDir, int maxDimension)
    {
        int count = 0;
        foreach (var sourceFile in Directory.EnumerateFiles(sourceDir, "*.png", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            string outputFile = Path.Combine(outputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
            OptimizeImage(sourceFile, outputFile, maxDimension);
            count++;
        }
        return count;
    }

    public static void OptimizeImage(string sourcePath, string outputPath, int maxDimension)
    {
        int sourceWidth, sourceHeight;
        using (var probe = new Bitmap(sourcePath))
        {
            sourceWidth = probe.Width;
            sourceHeight = probe.Height;
        }

        int longSide = Math.Max(sourceWidth, sourceHeight);
        if (longSide <= maxDimension)
        {
            // ponytail: already within budget — copy through rather than re-encoding,
            // since GDI+'s PNG encoder isn't guaranteed to shrink an already-small file.
            File.Copy(sourcePath, outputPath, overwrite: true);
            return;
        }

        double scale = (double)maxDimension / longSide;
        int newWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int newHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));

        using var source = new Bitmap(sourcePath);
        using var resized = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(resized))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, 0, 0, newWidth, newHeight);
        }
        resized.Save(outputPath, ImageFormat.Png);
    }
}
```

```csharp
// src/YahaPet.AssetPipeline/Program.cs
using System;
using YahaPet.AssetPipeline;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: YahaPet.AssetPipeline <sourceDir> <outputDir> [--max-dimension N] [--frame-stride N]");
    return 1;
}

string sourceDir = args[0];
string outputDir = args[1];
int maxDimension = 320;
int frameStride = 1;

for (int i = 2; i < args.Length - 1; i++)
{
    if (args[i] == "--max-dimension" && int.TryParse(args[i + 1], out var dim)) maxDimension = dim;
    if (args[i] == "--frame-stride" && int.TryParse(args[i + 1], out var stride)) frameStride = stride;
}

int resized = ImageOptimizer.OptimizeDirectory(sourceDir, outputDir, maxDimension);
Console.WriteLine($"Resized/copied {resized} PNG files into {outputDir}");

if (frameStride > 1)
{
    int removed = FrameResampler.ResampleDirectoryInPlace(outputDir, frameStride);
    Console.WriteLine($"Removed {removed} resampled-out frames (stride={frameStride})");
}

return 0;
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src/YahaPet.AssetPipeline.Tests/YahaPet.AssetPipeline.Tests.csproj`
Expected: PASS (the `FrameResampler` reference in `Program.cs` is unused by these tests but must exist to compile the console project — implemented next in Task 8; until then, leave `Program.cs` out of this commit's build by completing Task 8 immediately after, or stub `FrameResampler.ResampleDirectoryInPlace` as a no-op returning 0 here and replace it in Task 8).

- [ ] **Step 5a: Add a temporary stub so Task 7 builds independently**

```csharp
// src/YahaPet.AssetPipeline/FrameResampler.cs
namespace YahaPet.AssetPipeline;

public static class FrameResampler
{
    // ponytail: placeholder until Task 8 implements real frame-count resampling.
    public static int ResampleDirectoryInPlace(string directory, int stride) => 0;
}
```

Run: `dotnet build src/YahaPet.AssetPipeline/YahaPet.AssetPipeline.csproj`
Expected: builds successfully.

- [ ] **Step 6: Commit**

```bash
git add src/YahaPet.AssetPipeline src/YahaPet.AssetPipeline.Tests src/YahaPet.sln
git commit -m "feat: add asset pipeline resize/recompress tool"
```

---

### Task 8: Asset pipeline — frame-count resampling, run against Hachiware

**Files:**
- Modify: `src/YahaPet.AssetPipeline/FrameResampler.cs`
- Create: `src/YahaPet.AssetPipeline.Tests/FrameResamplerTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `FrameResampler.Resample<T>(IReadOnlyList<T> orderedFrames, int stride) -> IReadOnlyList<T>`, `FrameResampler.SortFramesByLeadingNumber(IEnumerable<string> filePaths) -> List<string>`, `FrameResampler.ResampleDirectoryInPlace(string directory, int stride) -> int` (replaces the Task 7 stub).

- [ ] **Step 1: Write the failing tests**

```csharp
// src/YahaPet.AssetPipeline.Tests/FrameResamplerTests.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YahaPet.AssetPipeline;
using Xunit;

public class FrameResamplerTests
{
    [Fact]
    public void Resample_StrideTwo_KeepsFirstLastAndEveryOther()
    {
        var frames = Enumerable.Range(1, 10).ToList();
        var result = FrameResampler.Resample(frames, stride: 2);
        Assert.Equal(new List<int> { 1, 3, 5, 7, 9, 10 }, result);
    }

    [Fact]
    public void Resample_StrideOne_ReturnsAllFrames()
    {
        var frames = Enumerable.Range(1, 5).ToList();
        var result = FrameResampler.Resample(frames, stride: 1);
        Assert.Equal(frames, result);
    }

    [Fact]
    public void Resample_TwoOrFewerFrames_ReturnsAllFrames()
    {
        var frames = new List<int> { 1, 2 };
        var result = FrameResampler.Resample(frames, stride: 5);
        Assert.Equal(frames, result);
    }

    [Fact]
    public void SortFramesByLeadingNumber_SortsNumericallyNotLexically()
    {
        // Matches the original's sort key: int(f.stem.split('-')[0]), so "10.png" sorts
        // after "9.png" (not before it, as plain string sort would place it).
        var files = new List<string> { "10.png", "2.png", "1.png", "9-stop.png" };
        var sorted = FrameResampler.SortFramesByLeadingNumber(files);
        Assert.Equal(new List<string> { "1.png", "2.png", "9-stop.png", "10.png" }, sorted);
    }

    [Fact]
    public void ResampleDirectoryInPlace_RemovesFilesNotKeptByStride()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"resample-{System.Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        try
        {
            for (int i = 1; i <= 10; i++)
                File.WriteAllBytes(Path.Combine(dir, $"{i}.png"), new byte[] { 1 });

            int removed = FrameResampler.ResampleDirectoryInPlace(dir, stride: 2);

            Assert.Equal(4, removed); // 10 frames -> 6 kept (1,3,5,7,9,10) -> 4 removed
            var remaining = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToList();
            Assert.Equal(6, remaining.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/YahaPet.AssetPipeline.Tests/YahaPet.AssetPipeline.Tests.csproj`
Expected: FAIL — the stub `ResampleDirectoryInPlace` always returns 0, and `Resample`/`SortFramesByLeadingNumber` do not exist yet.

- [ ] **Step 3: Implement**

```csharp
// src/YahaPet.AssetPipeline/FrameResampler.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YahaPet.AssetPipeline;

public static class FrameResampler
{
    /// Keeps every `stride`-th frame, always preserving the first and last frame so an
    /// animation's start/end pose is never dropped. stride<=1 or <=2 frames is a no-op.
    public static IReadOnlyList<T> Resample<T>(IReadOnlyList<T> orderedFrames, int stride)
    {
        if (stride <= 1 || orderedFrames.Count <= 2) return orderedFrames;

        var kept = new List<T> { orderedFrames[0] };
        for (int i = stride; i < orderedFrames.Count - 1; i += stride)
            kept.Add(orderedFrames[i]);
        kept.Add(orderedFrames[^1]);
        return kept;
    }

    /// Matches the original's sort key: the integer before the first '-' in the filename
    /// stem (e.g. "9-stop.png" sorts as 9, "10.png" sorts as 10).
    public static List<string> SortFramesByLeadingNumber(IEnumerable<string> filePaths)
    {
        return filePaths
            .OrderBy(f =>
            {
                string stem = Path.GetFileNameWithoutExtension(f);
                string leading = stem.Split('-')[0];
                return int.TryParse(leading, out int n) ? n : int.MaxValue;
            })
            .ToList();
    }

    /// Applies frame resampling to every animation subfolder under `directory`,
    /// deleting files that resampling drops. Returns the total number of files removed.
    public static int ResampleDirectoryInPlace(string directory, int stride)
    {
        if (stride <= 1) return 0;

        int removed = 0;
        foreach (var folder in Directory.GetDirectories(directory))
        {
            var allFiles = Directory.GetFiles(folder, "*.png");
            var sorted = SortFramesByLeadingNumber(allFiles);
            var kept = new HashSet<string>(Resample(sorted, stride));

            foreach (var file in sorted)
            {
                if (!kept.Contains(file))
                {
                    File.Delete(file);
                    removed++;
                }
            }
        }
        return removed;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/YahaPet.AssetPipeline.Tests/YahaPet.AssetPipeline.Tests.csproj`
Expected: PASS

- [ ] **Step 5: Run the pipeline against Hachiware's real asset set**

Run:
```bash
dotnet run --project src/YahaPet.AssetPipeline -- assets/hachiware assets/optimized/hachiware --max-dimension 320 --frame-stride 2
```
Expected output: `Resized/copied 85 PNG files into assets/optimized/hachiware` followed by `Removed <N> resampled-out frames (stride=2)` — inspect the console output and `assets/optimized/hachiware/` afterward. Per the Global Constraints note, expect the resize step to mostly copy files through unchanged (Hachiware's sources are already ~160×144, under the 320px target) while the `walkleft` folder (72 frames) shrinks meaningfully from frame resampling. Manually open a couple of `before`/`after` frames from `assets/optimized/hachiware/walkleft/` side by side to confirm the sprite still reads correctly at the reduced frame count.

- [ ] **Step 6: Commit**

```bash
git add src/YahaPet.AssetPipeline/FrameResampler.cs src/YahaPet.AssetPipeline.Tests/FrameResamplerTests.cs assets/optimized/hachiware
git commit -m "feat: add frame-count resampling; run asset pipeline against Hachiware"
```

---

### Task 9: WPF app shell — tray icon and window-style helper

**Files:**
- Create: `src/YahaPet.Wpf/YahaPet.Wpf.csproj`
- Create: `src/YahaPet.Wpf/App.xaml`
- Create: `src/YahaPet.Wpf/App.xaml.cs`
- Create: `src/YahaPet.Wpf/NativeMethods.cs`

**Interfaces:**
- Consumes: `YahaPet.Core` (Tasks 1–6).
- Produces: `App` (holds the `NotifyIcon`, tray `ContextMenuStrip`, and a `Dictionary<string, CharacterWindow>` of spawned characters — `CharacterWindow` itself is created in Task 10), `NativeMethods.MakeToolWindow(IntPtr hwnd)`.

- [ ] **Step 1: Scaffold the WPF project**

Run:
```bash
dotnet new wpf -n YahaPet.Wpf -o src/YahaPet.Wpf -f net8.0-windows
dotnet sln src/YahaPet.sln add src/YahaPet.Wpf/YahaPet.Wpf.csproj
dotnet add src/YahaPet.Wpf/YahaPet.Wpf.csproj reference src/YahaPet.Core/YahaPet.Core.csproj
```

Edit `src/YahaPet.Wpf/YahaPet.Wpf.csproj` to add Windows Forms support (needed for `NotifyIcon`) and mark assets as content:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Content Include="..\..\assets\optimized\hachiware\**\*.*">
      <Link>assets\hachiware\%(RecursiveDir)%(Filename)%(Extension)</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write the tool-window native helper**

```csharp
// src/YahaPet.Wpf/NativeMethods.cs
using System;
using System.Runtime.InteropServices;

namespace YahaPet.Wpf;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// Removes the window from the taskbar and Alt-Tab list, matching the original's
    /// Qt.WindowType.Tool flag. WPF's ShowInTaskbar=False alone does not fully replicate this.
    public static void MakeToolWindow(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle = (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }
}
```

- [ ] **Step 3: Write the app shell with tray icon**

```xml
<!-- src/YahaPet.Wpf/App.xaml -->
<Application x:Class="YahaPet.Wpf.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
</Application>
```

```csharp
// src/YahaPet.Wpf/App.xaml.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace YahaPet.Wpf;

public partial class App : Application
{
    private NotifyIcon? _trayIcon;
    private ToolStripMenuItem? _playAnimationMenu;
    private ToolStripMenuItem? _kickMenu;
    private ToolStripMenuItem? _stopResumeMenu;
    private ToolStripMenuItem? _muteAllItem;
    private readonly Dictionary<string, CharacterWindow> _characters = new(StringComparer.OrdinalIgnoreCase);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ponytail: the Python original shows an invisible, contentless full-screen
        // background window (`yahawindow`) that nothing ever draws on and no user story
        // observably depends on. ShutdownMode=OnExplicitShutdown already keeps this app
        // alive with only a tray icon, so that vestigial window is not ported.

        var contextMenu = new ContextMenuStrip();

        var spawnMenu = new MenuItem("Spawn Character");
        var spawnHachiware = new MenuItem("Hachiware");
        spawnHachiware.Click += (_, _) => SpawnCharacter("hachiware");
        spawnMenu.DropDownItems.Add(spawnHachiware);
        contextMenu.Items.Add(spawnMenu);

        _playAnimationMenu = new MenuItem("Play Animation") { Enabled = false };
        contextMenu.Items.Add(_playAnimationMenu);

        var sayHiItem = new MenuItem("Say hi!");
        sayHiItem.Click += (_, _) => SayHi();
        contextMenu.Items.Add(sayHiItem);

        _kickMenu = new MenuItem("Kick") { Enabled = false };
        contextMenu.Items.Add(_kickMenu);

        _muteAllItem = new MenuItem("Mute All") { Enabled = false };
        _muteAllItem.Click += (_, _) => ToggleMuteAll();
        contextMenu.Items.Add(_muteAllItem);

        _stopResumeMenu = new MenuItem("Stop/Resume Random Animations of...") { Enabled = false };
        contextMenu.Items.Add(_stopResumeMenu);

        var exitItem = new MenuItem("Exit");
        exitItem.Click += (_, _) => Shutdown();
        contextMenu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),
            Visible = true,
            ContextMenuStrip = contextMenu,
            Text = "Yaha-Pet"
        };
    }

    private void SpawnCharacter(string name)
    {
        // Full spawn wiring (window creation, menu population) lands in Tasks 10-14.
        // This method exists now so the tray menu is clickable and testable end to end
        // once those tasks fill it in.
    }

    private void SayHi()
    {
        // Implemented in Task 14.
    }

    private void ToggleMuteAll()
    {
        // Implemented in Task 12.
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/YahaPet.Wpf`
Expected: a tray icon appears (Windows may place it in the overflow area — check there); right-clicking shows the full menu structure with "Play Animation", "Kick", "Mute All", and "Stop/Resume..." greyed out, and "Spawn Character > Hachiware" clickable (does nothing yet). "Exit" quits the app cleanly with no lingering process. No separate application window appears anywhere (confirms the `yahawindow`-skip decision has no visible effect).

- [ ] **Step 5: Commit**

```bash
git add src/YahaPet.Wpf src/YahaPet.sln
git commit -m "feat: add WPF app shell with tray icon and context menu skeleton"
```

---

### Task 10: CharacterWindow — spawn, frame preloading, fall-in

**Files:**
- Create: `src/YahaPet.Wpf/CharacterWindow.xaml`
- Create: `src/YahaPet.Wpf/CharacterWindow.xaml.cs`
- Create: `src/YahaPet.Wpf/SpriteLoader.cs`

**Interfaces:**
- Consumes: `PetPoint`, `PetBounds`, `BehaviorPlanner.ClampToBounds`, `BehaviorPlanner.PlanFall`, `FallOutcome` (Task 5), `ConfigLoader`, `BehaviorPlanner.GetFps` (Task 6).
- Produces: `CharacterWindow(string characterName)` constructor, `CharacterWindow.Spawn()`, `SpriteLoader.LoadFrames(string animationFolder, int maxWidth, int maxHeight) -> List<BitmapSource>`, `SpriteLoader.LoadSingle(string filePath, int maxWidth, int maxHeight) -> BitmapSource`, `SpriteLoader.Mirror(BitmapSource source) -> BitmapSource`.

- [ ] **Step 1: Write the sprite loader**

```csharp
// src/YahaPet.Wpf/SpriteLoader.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YahaPet.Wpf;

/// Loads and scales PNG frames, preserving aspect ratio like the original's
/// QPixmap.scaled(..., KeepAspectRatio). Assumes files are pre-optimized by
/// YahaPet.AssetPipeline (Tasks 7-8), so decoding at native size is cheap.
public static class SpriteLoader
{
    public static BitmapSource LoadSingle(string filePath, int maxWidth, int maxHeight)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        double scale = Math.Min((double)maxWidth / bitmap.PixelWidth, (double)maxHeight / bitmap.PixelHeight);
        if (scale >= 1.0) return bitmap;

        var scaled = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }

    public static List<BitmapSource> LoadFrames(string animationFolder, int maxWidth, int maxHeight)
    {
        var files = Directory.GetFiles(animationFolder, "*.png")
            .OrderBy(f =>
            {
                string stem = Path.GetFileNameWithoutExtension(f);
                string leading = stem.Split('-')[0];
                return int.TryParse(leading, out int n) ? n : int.MaxValue;
            })
            .ToList();

        return files.Select(f => LoadSingle(f, maxWidth, maxHeight)).ToList();
    }

    public static BitmapSource Mirror(BitmapSource source)
    {
        var mirrored = new TransformedBitmap(source, new ScaleTransform(-1, 1));
        mirrored.Freeze();
        return mirrored;
    }
}
```

- [ ] **Step 2: Write the CharacterWindow shell**

```xml
<!-- src/YahaPet.Wpf/CharacterWindow.xaml -->
<Window x:Class="YahaPet.Wpf.CharacterWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        SizeToContent="WidthAndHeight">
    <Image x:Name="SpriteImage" Stretch="Fill" />
</Window>
```

```csharp
// src/YahaPet.Wpf/CharacterWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using YahaPet.Core;

namespace YahaPet.Wpf;

public partial class CharacterWindow : Window
{
    public string CharacterName { get; }

    private readonly int _characterWidth;
    private readonly int _characterHeight;
    private readonly Dictionary<string, List<BitmapSource>> _frames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapSource> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _assetRoot;
    private readonly Random _spriteRandom = new();

    public CharacterWindow(string characterName)
    {
        InitializeComponent();
        CharacterName = characterName.ToLowerInvariant();
        _assetRoot = Path.Combine(AppContext.BaseDirectory, "assets", CharacterName);

        _characterWidth = (int)(SystemParameters.PrimaryScreenWidth / 10);
        _characterHeight = (int)(SystemParameters.PrimaryScreenHeight / 10);

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeToolWindow(hwnd);
        };
    }

    public void Spawn()
    {
        LoadStaticSprites();

        double startX = SystemParameters.PrimaryScreenWidth / 2;
        Left = startX;
        Top = 0;
        SetSprite(RandomFrom(_sprites, "spawn"));
        Show();

        FallTo(landingY: (int)SystemParameters.WorkArea.Bottom);
    }

    private void LoadStaticSprites()
    {
        string spritesDir = Path.Combine(_assetRoot, "sprites");
        foreach (var file in Directory.GetFiles(spritesDir, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            _sprites[name] = SpriteLoader.LoadSingle(file, _characterWidth, _characterHeight);
        }
    }

    private BitmapSource RandomFrom(Dictionary<string, BitmapSource> pool, string prefix)
    {
        // Original picks randomly among files sharing a prefix (e.g. "spawn", "spawn1", "spawn2").
        var candidates = new List<BitmapSource>();
        foreach (var kvp in pool)
            if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                candidates.Add(kvp.Value);
        return candidates.Count > 0 ? candidates[_spriteRandom.Next(candidates.Count)] : pool[prefix];
    }

    private void SetSprite(BitmapSource sprite)
    {
        SpriteImage.Source = sprite;
        Width = sprite.PixelWidth;
        Height = sprite.PixelHeight;
    }

    private void FallTo(int landingY)
    {
        var currentPos = new PetPoint((int)Left, (int)Top);
        var outcome = BehaviorPlanner.PlanFall(
            currentPos,
            screenHeight: (int)SystemParameters.PrimaryScreenHeight,
            landingY: landingY,
            characterHeight: (int)Height,
            new SystemRandomSource());

        SetSprite(RandomFrom(_sprites, "falling"));
        AnimatePosition(new PetPoint((int)Left, outcome.LandingPoint.Y), outcome.DurationMs, onComplete: () =>
        {
            SetSprite(outcome.Crashed ? _sprites["fallingend"] : RandomFrom(_sprites, "spawn"));
        });
    }

    private void AnimatePosition(PetPoint target, int durationMs, Action? onComplete)
    {
        var animation = new System.Windows.Media.Animation.DoubleAnimation(Top, target.Y, TimeSpan.FromMilliseconds(durationMs));
        if (onComplete != null) animation.Completed += (_, _) => onComplete();
        BeginAnimation(TopProperty, animation);
        Left = target.X;
    }
}
```

- [ ] **Step 3: Wire spawning into `App.xaml.cs`**

```csharp
// src/YahaPet.Wpf/App.xaml.cs (modify SpawnCharacter)
private void SpawnCharacter(string name)
{
    string key = name.ToLowerInvariant();
    if (_characters.ContainsKey(key))
    {
        System.Windows.MessageBox.Show("Character already spawned!", "Fail");
        return;
    }

    var window = new CharacterWindow(key);
    _characters[key] = window;
    window.Spawn();

    _playAnimationMenu!.Enabled = true;
    _kickMenu!.Enabled = true;
    _muteAllItem!.Enabled = true;
    _stopResumeMenu!.Enabled = true;
}
```

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/YahaPet.Wpf`, right-click tray icon, choose Spawn Character > Hachiware.
Expected: Hachiware appears centered horizontally at the top of the screen and animates falling down to rest just above the taskbar, ending on either a normal or a "crashed" pose (run several times to observe both outcomes — roughly 3 in 10 spawns should crash). The window has no border/title bar and does not appear in the taskbar or Alt-Tab (Alt-Tab through open windows to confirm). Spawning "Hachiware" a second time shows the "Character already spawned!" message box instead of a second character.

- [ ] **Step 5: Commit**

```bash
git add src/YahaPet.Wpf/CharacterWindow.xaml src/YahaPet.Wpf/CharacterWindow.xaml.cs src/YahaPet.Wpf/SpriteLoader.cs src/YahaPet.Wpf/App.xaml.cs
git commit -m "feat: spawn Hachiware with fall-in animation and static sprite loading"
```

---

### Task 11: CharacterWindow — autonomous behavior loop (walk, jump, idle)

**Files:**
- Modify: `src/YahaPet.Wpf/CharacterWindow.xaml.cs`

**Interfaces:**
- Consumes: `BehaviorPlanner.NextIdleIntervalMs`, `ChooseAutonomousAction`, `PlanJump`, `PlanWalk`, `GetFps` (Tasks 2–4, 6), `ConfigLoader.Load` (Task 6), `SpriteLoader.LoadFrames`/`Mirror` (Task 10).

- [ ] **Step 1: Add animation-frame loading (with auto-mirroring) and the idle timer**

```csharp
// src/YahaPet.Wpf/CharacterWindow.xaml.cs (additions)
using System.Windows.Threading;

// --- add fields ---
private readonly DispatcherTimer _idleTimer = new();
private readonly DispatcherTimer _frameTimer = new();
private readonly Dictionary<string, CharacterConfig> _config;
private List<string> _otherAnimationNames = new();
private bool _isAnimating;
private bool _isDragging;
private List<BitmapSource> _currentAnimationFrames = new();
private int _currentFrameIndex;

// --- add to constructor, after LoadStaticSprites-related setup would run ---
// (config is loaded once per app; for this slice, load it directly here for simplicity)
_config = ConfigLoader.Load(Path.Combine(AppContext.BaseDirectory, "config.json"));

_idleTimer.Tick += (_, _) => OnIdleTick();
_frameTimer.Tick += (_, _) => OnFrameTick();

// --- new methods ---
private void DiscoverOtherAnimations()
{
    string animationsDir = Path.Combine(_assetRoot, "animations");
    var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "walkleft", "walkright", "jumpleft", "jumpright", "falling" };
    _otherAnimationNames = Directory.Exists(animationsDir)
        ? new List<string>(Directory.GetDirectories(animationsDir))
            .ConvertAll(Path.GetFileName)!
            .FindAll(n => n != null && !excluded.Contains(n))
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
    if (!_isAnimating && !_isDragging)
    {
        var action = BehaviorPlanner.ChooseAutonomousAction(_otherAnimationNames, new SystemRandomSource());
        switch (action.Kind)
        {
            case AutonomousActionKind.Jump: PlayJump(); break;
            case AutonomousActionKind.Walk: PlayWalk(); break;
            case AutonomousActionKind.PlayAnimation: PlayNamedAnimation(action.AnimationName!); break;
            case AutonomousActionKind.NoOp: break;
        }
    }
    StartIdleTimer();
}

private List<BitmapSource> GetOrLoadFrames(string animationName)
{
    if (_frames.TryGetValue(animationName, out var cached)) return cached;

    string folder = Path.Combine(_assetRoot, "animations", animationName);
    if (Directory.Exists(folder))
    {
        var loaded = SpriteLoader.LoadFrames(folder, _characterWidth, _characterHeight);
        _frames[animationName] = loaded;

        if (animationName.StartsWith("walk", StringComparison.OrdinalIgnoreCase) ||
            animationName.StartsWith("jump", StringComparison.OrdinalIgnoreCase))
        {
            string mirroredName = animationName.EndsWith("left", StringComparison.OrdinalIgnoreCase)
                ? animationName[..^4] + "right"
                : animationName[..^5] + "left";
            if (!_frames.ContainsKey(mirroredName))
                _frames[mirroredName] = loaded.ConvertAll(SpriteLoader.Mirror);
        }
        return loaded;
    }

    _frames[animationName] = new List<BitmapSource>();
    return _frames[animationName];
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

private Action? _pendingOnComplete;

private void OnFrameTick()
{
    if (_currentFrameIndex >= _currentAnimationFrames.Count)
    {
        _frameTimer.Stop();
        _isAnimating = false;
        _pendingOnComplete?.Invoke();
        return;
    }
    SetSprite(_currentAnimationFrames[_currentFrameIndex]);
    _currentFrameIndex++;
}

private void PlayWalk()
{
    var plan = BehaviorPlanner.PlanWalk(new PetPoint((int)Left, (int)Top), (int)SystemParameters.PrimaryScreenWidth, (int)Width, new SystemRandomSource());
    if (plan is null) return;

    _isAnimating = true;
    string animationName = plan.Direction == BehaviorPlanner.WalkDirection.Left ? "walkleft" : "walkright";
    PlayFrameSequence(animationName, onComplete: () => _isAnimating = false);

    var animation = new System.Windows.Media.Animation.DoubleAnimation(Left, plan.TargetX, TimeSpan.FromMilliseconds(plan.DurationMs));
    BeginAnimation(LeftProperty, animation);
}

private void PlayJump()
{
    var plan = BehaviorPlanner.PlanJump(new PetPoint((int)Left, (int)Top), (int)Height, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.WorkArea.Bottom, new SystemRandomSource());
    _isAnimating = true;

    string animationName = plan.Direction == BehaviorPlanner.JumpDirection.Left ? "jumpleft" : "jumpright";
    var frames = GetOrLoadFrames(animationName);

    var riseAnimation = new System.Windows.Media.Animation.DoubleAnimation(Top, plan.RiseTarget.Y, TimeSpan.FromMilliseconds(plan.DurationMs));
    var riseAnimationX = new System.Windows.Media.Animation.DoubleAnimation(Left, plan.RiseTarget.X, TimeSpan.FromMilliseconds(plan.DurationMs));

    if (frames.Count > 0)
    {
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
        var landAnimation = new System.Windows.Media.Animation.DoubleAnimation(Top, plan.LandTarget.Y, TimeSpan.FromMilliseconds(plan.DurationMs));
        var landAnimationX = new System.Windows.Media.Animation.DoubleAnimation(Left, plan.LandTarget.X, TimeSpan.FromMilliseconds(plan.DurationMs));
        landAnimation.Completed += (_, _) =>
        {
            _isAnimating = false;
            SetSprite(RandomFrom(_sprites, "spawn"));
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
    PlayFrameSequence(animationName, onComplete: () =>
    {
        _isAnimating = false;
        SetSprite(RandomFrom(_sprites, "spawn"));
    });
}
```

- [ ] **Step 2: Call discovery/timer start from `Spawn()`**

```csharp
// src/YahaPet.Wpf/CharacterWindow.xaml.cs (modify Spawn)
public void Spawn()
{
    LoadStaticSprites();
    DiscoverOtherAnimations();

    double startX = SystemParameters.PrimaryScreenWidth / 2;
    Left = startX;
    Top = 0;
    SetSprite(RandomFrom(_sprites, "spawn"));
    Show();

    FallTo(landingY: (int)SystemParameters.WorkArea.Bottom);
    StartIdleTimer();
}
```

- [ ] **Step 3: Manual verification**

Run the app, spawn Hachiware, and leave it idle for a few minutes. Expected: it occasionally walks left/right (with the sprite visibly mirrored depending on direction) and occasionally jumps in an arc using the static `jumpleft.png`/`jumpright.png` sprite (no per-frame jump animation, per the Global Constraints note). Since `_otherAnimationNames` is empty for Hachiware, the "other animation" roll branch should never visibly do anything (no crash, no visible action) — this is expected, not a bug. Confirm walking never sends it fully off-screen and jumping never crosses a screen edge.

- [ ] **Step 4: Commit**

```bash
git add src/YahaPet.Wpf/CharacterWindow.xaml.cs
git commit -m "feat: wire autonomous idle/walk/jump behavior into CharacterWindow"
```

---

### Task 12: CharacterWindow — sound playback

**Files:**
- Create: `src/YahaPet.Wpf/SoundPlayerFactory.cs`
- Modify: `src/YahaPet.Wpf/CharacterWindow.xaml.cs`
- Modify: `src/YahaPet.Wpf/App.xaml.cs`
- Create: `src/YahaPet.Wpf.Tests/YahaPet.Wpf.Tests.csproj`
- Create: `src/YahaPet.Wpf.Tests/SoundPlayerFactoryTests.cs`

**Interfaces:**
- Produces: `SoundPlayerFactory.PlayIfExists(string filePath)`.

- [ ] **Step 1: Scaffold a test project for the one piece of WPF-adjacent logic worth testing directly**

Run:
```bash
dotnet new xunit -n YahaPet.Wpf.Tests -o src/YahaPet.Wpf.Tests -f net8.0-windows
dotnet sln src/YahaPet.sln add src/YahaPet.Wpf.Tests/YahaPet.Wpf.Tests.csproj
dotnet add src/YahaPet.Wpf.Tests/YahaPet.Wpf.Tests.csproj reference src/YahaPet.Wpf/YahaPet.Wpf.csproj
rm src/YahaPet.Wpf.Tests/UnitTest1.cs
```

- [ ] **Step 2: Write the failing test**

```csharp
// src/YahaPet.Wpf.Tests/SoundPlayerFactoryTests.cs
using System;
using System.IO;
using YahaPet.Wpf;
using Xunit;

public class SoundPlayerFactoryTests
{
    [Fact]
    public void PlayIfExists_MissingFile_DoesNotThrow()
    {
        // Verifies the exact scenario Hachiware exercises today: no sounds/ folder at
        // all, so every animation/spawn/grabbed sound lookup misses. Must be a silent
        // no-op, matching the original's Path.exists() guard.
        string missingPath = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".wav");
        var exception = Record.Exception(() => SoundPlayerFactory.PlayIfExists(missingPath));
        Assert.Null(exception);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test src/YahaPet.Wpf.Tests/YahaPet.Wpf.Tests.csproj`
Expected: FAIL to compile — `SoundPlayerFactory` does not exist yet.

- [ ] **Step 4: Implement**

```csharp
// src/YahaPet.Wpf/SoundPlayerFactory.cs
using System.IO;
using System.Media;

namespace YahaPet.Wpf;

/// Plays a WAV file fire-and-forget. A new SoundPlayer per call lets overlapping
/// sounds (e.g. a "grabbed" effect over an animation's own sound) play concurrently,
/// matching the original's per-instance QSoundEffect usage.
public static class SoundPlayerFactory
{
    public static bool MuteAll { get; set; }

    public static void PlayIfExists(string filePath)
    {
        if (MuteAll || !File.Exists(filePath)) return;

        var player = new SoundPlayer(filePath);
        player.Play(); // async, fire-and-forget
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test src/YahaPet.Wpf.Tests/YahaPet.Wpf.Tests.csproj`
Expected: PASS

- [ ] **Step 6: Wire sound calls into CharacterWindow and the tray's Mute All / Say hi**

```csharp
// src/YahaPet.Wpf/CharacterWindow.xaml.cs (add helper, call from Spawn/PlayFrameSequence)
private void PlayAnimationSound(string animationName) =>
    SoundPlayerFactory.PlayIfExists(Path.Combine(_assetRoot, "sounds", $"{animationName}.wav"));

// In Spawn(), after SetSprite(RandomFrom(_sprites, "spawn")):
PlayAnimationSound("spawn");

// In PlayFrameSequence(string animationName, ...), right after `_pendingOnComplete = onComplete;`:
PlayAnimationSound(animationName);
```

```csharp
// src/YahaPet.Wpf/App.xaml.cs (modify ToggleMuteAll and SayHi)
private bool _muteAll;

private void ToggleMuteAll()
{
    _muteAll = !_muteAll;
    SoundPlayerFactory.MuteAll = _muteAll;
    _muteAllItem!.Text = _muteAll ? "Unmute All" : "Mute All";
}

private void SayHi()
{
    if (_characters.Count == 0)
    {
        _trayIcon!.ShowBalloonTip(500, "Wait!", "You have not spawned anyone yet!", ToolTipIcon.Info);
        return;
    }
    var names = new List<string>(_characters.Keys);
    string chosen = names[new Random().Next(names.Count)];
    SoundPlayerFactory.PlayIfExists(System.IO.Path.Combine(AppContext.BaseDirectory, "assets", chosen, "sounds", "hi.wav"));
    _trayIcon!.ShowBalloonTip(500, $"{chosen} says:", "Hi!", ToolTipIcon.Info);
}
```

- [ ] **Step 7: Manual verification**

Run the app and spawn Hachiware. Expected: no exceptions in the console/debugger output at spawn time or during any animation, even though Hachiware has no `sounds/` folder — confirming the graceful no-op path works. Click "Say hi!" — expect the balloon tip to appear with no audio (again, expected for Hachiware; real audio verification is deferred to the Usagi slice per the Global Constraints note). Click "Mute All" and confirm the menu label toggles to "Unmute All".

- [ ] **Step 8: Commit**

```bash
git add src/YahaPet.Wpf/SoundPlayerFactory.cs src/YahaPet.Wpf/CharacterWindow.xaml.cs src/YahaPet.Wpf/App.xaml.cs src/YahaPet.Wpf.Tests src/YahaPet.sln
git commit -m "feat: add sound playback with graceful no-file no-op, wire Mute All and Say hi"
```

---

### Task 13: CharacterWindow — drag, hold-shake, release-fall

**Files:**
- Modify: `src/YahaPet.Wpf/CharacterWindow.xaml.cs`

**Interfaces:**
- Consumes: `BehaviorPlanner.ClampToBounds`, `PetBounds` (Task 5).

- [ ] **Step 1: Add mouse handlers and the 4.5s hold-shake timer**

```csharp
// src/YahaPet.Wpf/CharacterWindow.xaml.cs (additions)
using System.Windows.Input;

// --- add fields ---
private System.Windows.Point _dragOffset;
private readonly DispatcherTimer _holdTimer = new() { Interval = TimeSpan.FromMilliseconds(4500) };
private bool _isShaking;
private readonly Random _dragRandom = new();
private BitmapSource? _grabbedSprite;

// --- wire up in constructor ---
MouseLeftButtonDown += OnMouseLeftButtonDown;
MouseMove += OnMouseMove;
MouseLeftButtonUp += OnMouseLeftButtonUp;
_holdTimer.Tick += (_, _) =>
{
    _holdTimer.Stop();
    _isShaking = true;
    SetSprite(_sprites["shaken"]);
};

private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (_isAnimating) return;

    _isDragging = true;
    _dragOffset = e.GetPosition(this);
    _grabbedSprite = null;
    _isShaking = false;
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

    var cursor = PointToScreen(e.GetPosition(this));
    var candidate = new PetPoint((int)(cursor.X - _dragOffset.X), (int)(cursor.Y - _dragOffset.Y));

    var bounds = new PetBounds(
        (int)SystemParameters.WorkArea.Left,
        (int)SystemParameters.WorkArea.Top,
        (int)SystemParameters.WorkArea.Right,
        (int)SystemParameters.WorkArea.Bottom);

    var clamped = BehaviorPlanner.ClampToBounds(candidate, bounds, (int)Width, (int)Height);

    if (_isShaking)
    {
        Left = clamped.X + _dragRandom.Next(0, 11);
        Top = clamped.Y + _dragRandom.Next(0, 11);
    }
    else
    {
        Left = clamped.X;
        Top = clamped.Y;
        if (_grabbedSprite is null)
        {
            _grabbedSprite = RandomFrom(_sprites, "grabbed");
            SetSprite(_grabbedSprite);
        }
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

    FallTo(landingY: (int)SystemParameters.WorkArea.Bottom);
}
```

- [ ] **Step 2: Manual verification**

Run the app, spawn Hachiware, and:
1. Click-and-drag it around the screen — confirm the sprite switches to a "grabbed" pose immediately and follows the cursor, clamped so it can't go under the taskbar or off any screen edge (test near all four edges, and — if a second monitor is available — dragging across the boundary between monitors).
2. Press and hold without releasing for about 4.5 seconds — confirm it switches to the "shaken" sprite and starts jittering around the cursor with small random offsets.
3. Release the mouse button — confirm it plays the fall animation down to just above the taskbar, ending in either a normal or crashed pose.
4. Confirm no interaction is possible while it's mid-animation (e.g. try clicking during the fall-in on spawn — nothing should happen until it lands).

- [ ] **Step 3: Commit**

```bash
git add src/YahaPet.Wpf/CharacterWindow.xaml.cs
git commit -m "feat: add drag, hold-to-shake, and release-to-fall interaction to CharacterWindow"
```

---

### Task 14: Full tray menu wiring and end-to-end verification

**Files:**
- Modify: `src/YahaPet.Wpf/App.xaml.cs`
- Modify: `src/YahaPet.Wpf/CharacterWindow.xaml.cs`

**Interfaces:**
- Consumes: everything produced by Tasks 9–13.

- [ ] **Step 1: Populate "Play Animation" and "Kick" submenus on spawn, and expose the actions they need**

```csharp
// src/YahaPet.Wpf/CharacterWindow.xaml.cs (add public entry points)
public IReadOnlyList<string> AllAnimationNames()
{
    var names = new List<string>(_otherAnimationNames) { "walkleft", "walkright" };
    if (Directory.Exists(Path.Combine(_assetRoot, "animations", "jumpleft")) ||
        File.Exists(Path.Combine(_assetRoot, "sprites", "jumpleft.png")))
    {
        names.Add("jumpleft");
        names.Add("jumpright");
    }
    return names;
}

public void PlayAnimationByName(string animationName)
{
    if (_isAnimating || _isDragging) return;
    if (animationName is "jumpleft" or "jumpright") { PlayJump(); return; }
    if (animationName is "walkleft" or "walkright") { PlayWalk(); return; }
    PlayNamedAnimation(animationName);
}

private bool _randomAnimationsEnabled = true;

public void ToggleRandomAnimations()
{
    _randomAnimationsEnabled = !_randomAnimationsEnabled;
    if (_randomAnimationsEnabled) StartIdleTimer();
    else _idleTimer.Stop();
}

public bool RandomAnimationsEnabled => _randomAnimationsEnabled;

public void Shutdown()
{
    _idleTimer.Stop();
    _frameTimer.Stop();
    _holdTimer.Stop();
    Close();
}
```

```csharp
// src/YahaPet.Wpf/CharacterWindow.xaml.cs (modify OnIdleTick to respect the toggle)
private void OnIdleTick()
{
    _idleTimer.Stop();
    if (_randomAnimationsEnabled && !_isAnimating && !_isDragging)
    {
        var action = BehaviorPlanner.ChooseAutonomousAction(_otherAnimationNames, new SystemRandomSource());
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
```

- [ ] **Step 2: Wire the tray menus in `App.xaml.cs`**

```csharp
// src/YahaPet.Wpf/App.xaml.cs (modify SpawnCharacter, add KickCharacter and per-character submenu wiring)
private void SpawnCharacter(string name)
{
    string key = name.ToLowerInvariant();
    if (_characters.ContainsKey(key))
    {
        System.Windows.MessageBox.Show("Character already spawned!", "Fail");
        return;
    }

    var window = new CharacterWindow(key);
    _characters[key] = window;
    window.Spawn();

    var playSubmenu = new ToolStripMenuItem(key);
    foreach (var animName in window.AllAnimationNames())
    {
        var item = new ToolStripMenuItem(animName);
        item.Click += (_, _) => window.PlayAnimationByName(animName);
        playSubmenu.DropDownItems.Add(item);
    }
    _playAnimationMenu!.DropDownItems.Add(playSubmenu);
    _playAnimationMenu.Enabled = true;

    var kickItem = new ToolStripMenuItem(key);
    kickItem.Click += (_, _) => KickCharacter(key, playSubmenu, kickItem);
    _kickMenu!.DropDownItems.Add(kickItem);
    _kickMenu.Enabled = true;

    var stopResumeItem = new ToolStripMenuItem($"{key} (click to disable)");
    stopResumeItem.Click += (_, _) =>
    {
        window.ToggleRandomAnimations();
        stopResumeItem.Text = window.RandomAnimationsEnabled ? $"{key} (click to disable)" : $"{key} (click to enable)";
    };
    _stopResumeMenu!.DropDownItems.Add(stopResumeItem);
    _stopResumeMenu.Enabled = true;

    _muteAllItem!.Enabled = true;
}

private void KickCharacter(string key, ToolStripMenuItem playSubmenu, ToolStripMenuItem kickItem)
{
    if (_characters.TryGetValue(key, out var window))
    {
        window.Shutdown();
        _characters.Remove(key);
    }
    _playAnimationMenu!.DropDownItems.Remove(playSubmenu);
    _kickMenu!.DropDownItems.Remove(kickItem);

    if (_characters.Count == 0)
    {
        _playAnimationMenu.Enabled = false;
        _kickMenu.Enabled = false;
        _muteAllItem!.Enabled = false;
        _stopResumeMenu!.Enabled = false;
    }
}
```

- [ ] **Step 3: End-to-end manual verification checklist**

Run `dotnet run --project src/YahaPet.Wpf` and, side by side with `python "Yaha-Pet!.py"` running from the repo root as the behavioral reference, walk through:

1. Tray icon appears; menu starts with only "Spawn Character > Hachiware" and "Say hi!"/"Exit" enabled.
2. Spawn Hachiware — falls in, lands (normal or crash), "Play Animation > hachiware", "Kick > hachiware", "Mute All", and "Stop/Resume... > hachiware" all become enabled.
3. Leave it idle 5+ minutes — observe a mix of walk and jump behavior (not jump-only, confirming the Task 2 bug fix), each within screen bounds.
4. Use "Play Animation > hachiware > walkleft" (and walkright, jumpleft, jumpright) to manually trigger each — confirm each plays once and returns to an idle spawn pose.
5. Drag, hold-to-shake, and release — confirm behavior matches Task 13's checklist.
6. "Stop/Resume Random Animations of... > hachiware" — confirm autonomous behavior stops, and the menu label flips to "(click to enable)"; click again to resume.
7. "Mute All" — confirm the label flips to "Unmute All" (no audio to verify either way for Hachiware, per the Global Constraints note).
8. "Say hi!" — confirm the balloon tip appears naming "hachiware".
9. "Kick > hachiware" — confirm the character window disappears immediately and all the per-character menu entries are removed; "Play Animation"/"Kick"/"Mute All"/"Stop/Resume..." grey out again since no characters remain.
10. "Exit" — confirm the app and any spawned character windows close, and the process fully terminates (check Task Manager).
11. Confirm attempting to spawn Hachiware a second time (before kicking the first) shows "Character already spawned!" and does not create a second window.

Record any deviation from this checklist as a bug to fix before considering the Hachiware slice done — this checklist is the acceptance criteria for the spec's user stories that aren't covered by the `YahaPet.Core` unit tests (window/UI behavior, per the spec's Testing Decisions section).

- [ ] **Step 4: Run the full automated test suite one more time**

Run: `dotnet test src/YahaPet.sln`
Expected: all tests across `YahaPet.Core.Tests`, `YahaPet.AssetPipeline.Tests`, and `YahaPet.Wpf.Tests` PASS.

- [ ] **Step 5: Commit**

```bash
git add src/YahaPet.Wpf/App.xaml.cs src/YahaPet.Wpf/CharacterWindow.xaml.cs
git commit -m "feat: complete tray menu wiring for Hachiware vertical slice"
```

---

## Self-Review Notes

- **Spec coverage:** User stories 1–3, 25, 26 → Tasks 9–10. Stories 4–8, 8a → Tasks 2–4, 11. Stories 9–15 → Tasks 5, 13. Stories 16–17 → Tasks 6, 12. Stories 18–19, 23 → Task 12. Stories 20–22, 24 → Task 14. Story 27 → Tasks 2–6 (the seam itself). Story 28 → Tasks 7–8. The co-op-animation and MVVM/DI/self-contained-deployment exclusions from the spec's Out of Scope are respected throughout (no such code appears in any task).
- **Placeholder scan:** no "TBD"/"handle appropriately" phrasing; the one intentionally-temporary stub (Task 7 Step 5a's `FrameResampler` no-op) is explicitly called out as temporary and is replaced with a real implementation in the very next task, not left dangling.
- **Type consistency:** `PetPoint`, `PetBounds`, `AutonomousAction(Kind)`, `BehaviorPlanner.JumpPlan/WalkPlan/FallOutcome`, `CharacterConfig/AnimationConfig`, and `SpriteLoader`/`SoundPlayerFactory`/`NativeMethods` method names are used identically across every task that references them.
