# Spec: Port Yaha-Pet to C# + WPF — Hachiware Vertical Slice

## Problem Statement

Yaha-Pet is currently a single-file Python + PyQt6 desktop pet application (`Yaha-Pet!.py`), packaged with PyInstaller into a portable ~190MB zip+exe that the maintainer shares with friends on Windows. The maintainer wants a native Windows 11 experience (faster startup, no PyInstaller unpack step, feels more "native") and, ideally, a smaller shippable size — but the current 190MB is driven almost entirely by unoptimized character animation assets (151MB of assets, 139MB of which belongs to one character, Usagi) rather than by the Python/PyQt6 runtime itself. A straight reimplementation in another language does not by itself solve the size problem; the asset pipeline needs separate attention.

The maintainer wants to replace the Python implementation with a C# + WPF implementation that replicates all of the application's currently-observable behavior, built incrementally one character at a time so behavior parity can be verified in small, checkable increments rather than attempting all three characters (Usagi, Hachiware, Chiikawa) and the asset-optimization work in one pass.

## Solution

Build a C# + WPF (.NET 8/10, Windows-only) reimplementation of Yaha-Pet (ChiikawaDesktopPet), living alongside the existing Python version in the same repository (`src/ChiikawaDesktopPet.Wpf/`) so the Python original remains available as a live behavioral reference during the port. This spec covers the first vertical slice only: a system tray application that can spawn and fully control **Hachiware**, matching the Python version's behavior for that character end to end (autonomous movement, drag/drop, sound, tray controls). Chiikawa, Usagi, and all asset re-encoding/optimization work are deliberately out of scope for this ticket and will be filed as follow-on specs once this slice proves out the porting pattern.

The codebase style is intentionally lean, code-behind-driven WPF (no MVVM, no DI container) — matching the directness of the original single-file script — with one pure-logic seam separating behavior decisions from window/UI execution, so the app's random/timing-driven behavior can be unit tested without driving real windows.

## User Stories

1. As a user, I want to see a system tray icon when the app launches, so that I know the app is running and can access its controls.
2. As a user, I want the tray icon's context menu to offer a "Spawn Character" submenu listing Hachiware, so that I can bring a character onto my desktop.
3. As a user, I want a freshly spawned Hachiware to appear centered horizontally at the top of my screen and fall down to rest just above the taskbar, so that spawning feels consistent with the original app's entrance animation.
4. As a user, I want Hachiware to occasionally, autonomously walk left or right across the screen without my input, so that the desktop pet feels alive.
5. As a user, I want Hachiware's walking animation to automatically mirror between left-facing and right-facing sprites depending on travel direction, so that only one direction's source frames need to exist as assets.
6. As a user, I want Hachiware to occasionally, autonomously perform a jump animation with an arced trajectory (rise then fall, eased in/out) that avoids jumping off the edges of my screen, so that the movement looks natural and never strands the character off-screen.
7. As a user, I want Hachiware to occasionally, autonomously play one of its other available idle/expressive animations (e.g. dancing) chosen at random, so that behavior doesn't feel repetitive.
8. As a user, I want the frequency of autonomous behavior rolls to be randomized within the same range as the original app (roughly every 3–10 seconds), so that the pacing feels the same as before.
8a. As a user, I want the autonomous action roll to be weighted across jump (10%), walk (40%), and another random named animation (45%, e.g. dancing) — a deliberate bug fix: the shipped Python version has a `roll <= 100` condition that is always true given a `0–99` roll, so in the current release autonomous ticks only ever trigger a jump; walk/dance are otherwise only reachable via the manual "Play Animation" menu. The maintainer chose to fix this to the layered distribution the code's structure clearly intended, rather than reproduce the bug.
9. As a user, I want to be able to left-click-and-drag Hachiware anywhere on screen, so that I can reposition it manually.
10. As a user, when I start dragging Hachiware, I want its sprite to switch to a random "grabbed" pose for the duration of the drag, so that it visually reacts to being picked up.
11. As a user, if I hold Hachiware (mouse button down, dragging or not) for about 4.5 seconds, I want it to switch to a "shaken" sprite and visibly jitter around my cursor with small random offsets, so that prolonged holding has a distinct reaction.
12. As a user, when I first press down on Hachiware, I want a random "grabbed" sound effect (if any exist for the character) to play once, so that picking it up has audio feedback.
13. As a user, when I release a dragged Hachiware, I want it to play a falling animation down to just above the taskbar (or nearest valid screen position under the cursor), so that dropping it feels physically consistent with spawning.
14. As a user, I want the falling/drop animation to sometimes end in a "crash" pose (roughly 30% of the time) and otherwise end in a normal landing pose (roughly 70% of the time), matching the original's randomized outcome.
15. As a user, when dragging, I want the character's position to be clamped to the visible/available area of whichever monitor is under my cursor (not the taskbar area), so that I can't drag it off-screen or under the taskbar, and so multi-monitor setups behave correctly.
16. As a user, I want each animation that has an associated sound file to play that sound automatically when the animation starts, so that animations have audio feedback consistent with the original.
17. As a user, I want animation playback speed (frames per second) to be configurable per character per animation via a config file, falling back to a default FPS (40) when no config entry exists, so that behavior matches the original's config-driven timing.
18. As a user, I want a "Say hi!" tray menu action that, if any character is currently spawned, picks one at random, shows a tray balloon notification with that character's name, and plays that character's "hi" sound, so that I get an original-equivalent easter-egg interaction.
19. As a user, if I click "Say hi!" with no characters spawned, I want a tray notification telling me no character has been spawned yet, so that the app doesn't fail silently.
20. As a user, I want a "Play Animation" tray submenu that, once Hachiware is spawned, lists all of Hachiware's available animations by name so I can manually trigger any of them on demand, so that I have direct manual control matching the original.
21. As a user, I want a "Stop/Resume Random Animations of..." tray submenu entry for Hachiware that toggles whether it continues to act autonomously, so that I can freeze a character in place if I want.
22. As a user, I want a "Kick" tray submenu entry that removes Hachiware from my desktop entirely (stopping its window, timers, and freeing it from all menus), so that I can get rid of a spawned character.
23. As a user, I want a "Mute All" tray toggle that silences all sound effects for all currently spawned characters, so that I can quickly go silent without closing the app.
24. As a user, I want an "Exit" tray menu action that fully closes the application (including any spawned characters), so that I have a clean way to quit.
25. As a user, I want Hachiware's window to have no title bar, no taskbar/alt-tab entry, always-on-top behavior, and a fully transparent background outside the sprite's visible pixels, so that it behaves like a borderless desktop overlay rather than a normal application window.
26. As a user, I want the app to prevent spawning a second Hachiware while one is already active (the tray menu should reject/ignore a duplicate spawn with a notification), matching the original's one-instance-per-character-name behavior.
27. As a developer maintaining this codebase, I want the autonomous-behavior decision logic (which action to take on a random-timer tick, jump target/height calculation, walk target range calculation, screen-edge clamping, and fall-outcome roll) implemented as pure, UI-free functions that accept an injectable random source, so that this logic can be unit tested deterministically without instantiating real WPF windows.
28. As a developer, I want a repeatable, scriptable asset-optimization pipeline (resize source PNGs to a 1080p/1440p bake target, recompress, and resample high-frame-count animations down in frame count) validated against Hachiware's small asset set, so that the same pipeline can later be pointed at Usagi's much larger asset set with confidence.

## Implementation Decisions

- **Target platform**: .NET 8/10 (LTS), WPF, `net10.0-windows`, framework-dependent deployment.
- **Project location**: new project under `src/ChiikawaDesktopPet.Wpf/` in the existing repository, alongside the current `assets/` (assets are reused as-is for this slice; no re-encoding happens here). The Python original stays in place as a running reference during development and is not modified or removed by this ticket.
- **Codebase style**: lean, code-behind WPF — no MVVM, no dependency-injection container, no repository/service abstractions beyond the one seam described below. Menu wiring, window management, and event handlers are written directly, mirroring the original script's directness.
- **The one testing seam**: a UI-free behavior/decision module (referred to below as the *behavior module*) owns all of the following pure computations, each taking explicit inputs (current position, screen/available-area bounds, character name/state) and an injectable random source, and returning a plain result/intent rather than performing any side effect:
  - Which autonomous action to take on an idle-timer tick (walk / jump / other named animation), using a corrected weighted roll over `[0,100)`: `0–9` → jump (10%), `10–49` → walk (40%), `50–94` → another random named animation excluding walk/jump (45%), `95–99` → no-op (5%, reserved — matches the original's unreachable co-op slot, which stays out of scope). This intentionally diverges from the shipped Python behavior (see User Story 8a) where a `roll <= 100` bug makes every tick a jump.
  - Jump trajectory calculation: direction choice (avoiding screen edges), first-half arc target point, jump height, and total duration.
  - Walk trajectory calculation: direction choice, valid target x-range given current position and screen width, and duration.
  - Screen/available-area clamping given a candidate point and a widget size.
  - Fall-animation outcome roll (crash vs. normal landing) and target landing point.
  - Config FPS lookup for a given character/animation, with the default fallback (40).
  - Random idle-timer interval selection (3000–10000ms range).
  - Random "grabbed" sound effect selection and random "spawn"/"falling" sprite selection.
- **Randomness injection**: the behavior module takes an abstraction over `System.Random` (a small delegate or interface, e.g. something like `Func<int,int,int>` for ranged rolls) so tests can supply fixed sequences instead of real randomness. This is the only abstraction introduced beyond direct WPF code.
- **Window/execution layer**: a per-character WPF `Window` (frameless, `AllowsTransparency`, topmost, excluded from the taskbar/alt-tab via the appropriate window style) owns an `Image` element for the current sprite frame, a `DispatcherTimer`-driven frame-advance loop per active animation, and mouse event handlers for drag/hold/release. It calls into the behavior module for all decisions and only performs the resulting window moves/resizes/frame swaps itself.
- **Animation playback**: matches the original's approach — pre-load a character's animation frame sequences from the existing per-folder PNG files on first use (not necessarily eagerly on spawn vs. lazily on first play — mirror whichever the original does, i.e. eager preload of the character's full animation set right after spawn), auto-generate the mirrored counterpart of any `walk*`/`jump*` animation instead of requiring separate left/right asset folders, and advance frames on a timer at the FPS looked up from the behavior module.
- **Tray icon**: implemented via `System.Windows.Forms.NotifyIcon` (enabling `UseWindowsForms` in the WPF project) rather than adding a third-party NuGet package, since it is the standard, dependency-free way to get a tray icon + context menu from a WPF app.
- **Sound playback**: implemented via `System.Media.SoundPlayer` (BCL, WAV-only, matching the project's existing WAV-only sound assets), instantiated per playback so overlapping sounds (e.g. a "grabbed" effect and an animation's own sound) can play concurrently without one cutting off the other, mirroring the original's use of separate `QSoundEffect` instances.
- **Config file**: read the same `config.json` shape as the original (`{ characterName: { animations: { animationName: { fps: <int> } } } }`), tolerating a missing file or missing entries by falling back to the default FPS of 40 for any animation without a configured value — since no real `config.json` exists in the repository or was recoverable from the maintainer, this inferred shape/default is the spec for this ticket.
- **Scope guard**: the never-triggered "co-op animation" code path present in the Python original (`assets/coanimations`, `available_coop_animations`, the unreachable `roll >= 95` branch) is not ported — no equivalent module, menu, or stub is created for it.

## Testing Decisions

- A good test here exercises only the behavior module's external contract — given a set of inputs (position, bounds, injected random values), assert on the returned intent/result (e.g. "given these bounds and this roll sequence, the walk target x falls within the expected range and never exceeds screen width", or "given a roll of 29, the result is a jump intent, not a walk intent") — not on any WPF window, timer, or rendering detail.
- Modules under test: the behavior module only, covering the weighted action-selection roll boundaries, jump direction/edge-avoidance logic (including the near-edge special cases), walk target-range calculation, screen/available-area clamping arithmetic, the fall crash/land roll, and config FPS lookup with fallback.
- There is no existing automated test suite in this repository (the Python original has none) — this ticket establishes the first one. Use xUnit (the standard, low-ceremony default for new .NET projects) with no additional test framework dependencies.
- Window/UI behavior (actual dragging, actual rendering, actual tray menu clicks) is not covered by automated tests in this ticket; it is verified manually against the running Python original as the reference implementation, per the maintainer's stated reason for keeping both versions in the same repo during the port.

## Out of Scope

- Porting Chiikawa or Usagi (separate, later specs — Usagi in particular carries the bulk of the planned asset-optimization work).
- Running the asset-optimization pipeline at scale on Usagi's much larger (~139MB) asset set — that remains a dedicated future ticket. This ticket does, however, build and validate the pipeline itself (resize to a 1080p/1440p bake target, recompress, frame-count resampling) against Hachiware's own small (~4.3MB) asset set, as a low-risk proving ground before it's pointed at Usagi.
- Implementing the co-op animation feature (permanently descoped — it was dead code in the original and is not being resurrected).
- MVVM, dependency injection, or any architectural layering beyond the single behavior-module seam described above.
- Self-contained deployment, installer/MSIX packaging, or runtime-auto-install handling (framework-dependent deployment was chosen because target machines already have .NET 8+).
- Recovering or defining the real `config.json` schema/values beyond the inferred default described above — if the maintainer later supplies a real config file, that would be a follow-on change.
- Localization/multi-language support (not a feature of the original).

## Further Notes

- Context for future specs: the current release is ~190MB, of which ~151MB is `assets/` and ~139MB of that belongs to Usagi alone (specifically four animation folders with unusually high frame counts: `dance` 194 frames/60MB, `mock` 84 frames/29MB, `walkleft` 138 frames/28MB, `danceswirl` 48 frames/16MB). Chiikawa's entire asset set, by contrast, is 7.7MB. The eventual asset-optimization ticket should bake sprites to a resolution that looks acceptable on common 1080p/1440p displays (some softness on 4K accepted as a tradeoff) and is expected to reduce frame counts on the animations above via resampling (e.g. every-other-frame) — both were agreed upfront by the maintainer as acceptable, automated (no new source art available), lossy trade-offs.
- Porting order after this ticket: Hachiware (this ticket) → Chiikawa → Usagi, with the asset-optimization ticket expected to land around the same time as the Usagi port since that's where it's actually needed.
- No issue tracker or triage label vocabulary was configured in this environment (`/setup-matt-pocock-skills` has not been run, no `gh` CLI, no authenticated tracker MCP), so this spec is being kept as a Markdown file in-repo rather than filed as a ticket with a `ready-for-agent` label. It should be moved into the real tracker (and labeled) once tracker setup is complete.
