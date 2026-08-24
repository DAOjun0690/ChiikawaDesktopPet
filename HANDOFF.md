# Yaha-Pet — C#/WPF Port — Session Handoff

**Written:** 2026-08-21, by Claude Code, for handoff to another AI coding tool (Antigravity) continuing this work in a fresh session.

## What this project is

Yaha-Pet is a desktop pet app featuring characters from the "Chiikawa" universe (non-profit fan project). The **original implementation** is a single Python file, `Yaha-Pet!.py` (PyQt6), packaged via PyInstaller into a ~190MB portable zip+exe. It is **being ported to C#/WPF**, one character at a time, to get a native Windows 11 experience and (eventually) a smaller footprint. The Python original is still in the repo, untouched, as a live behavioral reference — do not modify it.

**Goal of the port:** replicate all *currently-observable* behavior of the Python app in C#/WPF, framework-dependent (assumes target machines already have .NET 8+ Desktop Runtime — this was confirmed true for the maintainer's actual distribution audience of friends), shipped as a portable zip (no installer).

## Current status: All 3 characters ported (Hachiware, Chiikawa, Usagi).

All three ported characters are fully functional: spawn/fall-in, autonomous walk/jump/idle animations, drag/hold-to-shake/release-drop, tray menu control, sound playback (Usagi includes 10 WAV sound files), multi-monitor support. All work is committed to `main` (no open branches, no pending PRs). Working tree should be clean except for local build/publish artifacts (see `.gitignore`).

## Architecture

```
src/
  YahaPet.Core/            Pure C# class library — NO WPF/WinForms/System.Drawing references.
                            All autonomous-behavior decision logic (jump/walk trajectory
                            planning, screen clamping, fall outcome, config FPS lookup) lives
                            here as static functions taking an injectable IRandomSource, so it
                            can be unit tested deterministically. This is the ONE seam in the
                            whole codebase — everything else is direct, lean WPF code-behind
                            (no MVVM, no DI container, by deliberate choice).
  YahaPet.Core.Tests/       xUnit tests for the above (30 tests).
  YahaPet.AssetPipeline/    Standalone console tool: resizes/recompresses PNG sprites (GDI+ via
                            System.Drawing.Common), copies non-PNG assets (WAV, ICO), and can
                            resample (thin out) high-frame-count animations. Reads assets/<character>/,
                            writes assets/optimized/<character>/. Run once per character; output is
                            committed to git (not regenerated on every build).
  YahaPet.AssetPipeline.Tests/  xUnit tests (9 tests).
  YahaPet.Wpf/              The actual app. App.xaml.cs = tray icon + context menu + character
                            spawn/kick bookkeeping. CharacterWindow.xaml(.cs) = one borderless,
                            transparent, always-on-top WPF Window per spawned character; owns
                            all its own animation/drag/sound logic, parameterized by
                            characterName so the SAME class handles every character with zero
                            character-specific code branches.
  YahaPet.Wpf.Tests/        xUnit tests (2 tests, SoundPlayerFactory tests for non-throwing and mute).
```

**Solution file:** `src/YahaPet.sln`. Build with `dotnet build src/YahaPet.sln` from repo root. Run tests with `dotnet test src/YahaPet.sln` (currently 41/41 passing — 30 Core + 9 AssetPipeline + 2 Wpf).

## How to run / build / publish

Run in dev mode: `dotnet run --project src/YahaPet.Wpf`

Publish for sharing (framework-dependent, single-file, ~2.3MB — recipients need .NET 8+ Desktop Runtime already installed, which was confirmed true for this app's actual audience):
```bash
dotnet publish src/YahaPet.Wpf/YahaPet.Wpf.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```
Zip the whole `publish/` folder (it contains `YahaPet.Wpf.exe`, `config.json`, `assets/`) and share that.

Self-contained alternative (no .NET runtime needed on the target machine, but ~157MB instead of 2.3MB):
```bash
dotnet publish src/YahaPet.Wpf/YahaPet.Wpf.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-standalone
```

To port a new character's assets, run the asset pipeline first:
```bash
dotnet run --project src/YahaPet.AssetPipeline -- assets/<character> assets/optimized/<character> --max-dimension 320 [--frame-stride N]
```
Then add a `<Content Include="..\..\assets\optimized\<character>\**\*.*">` item to `YahaPet.Wpf.csproj` (copy the existing Hachiware/Chiikawa/Usagi ones) and a Spawn Character menu entry in `App.xaml.cs`'s `OnStartup`.

## Where the specs/plans live

- `docs/specs/hachiware-wpf-port.md` — the original spec for the Hachiware slice.
- `docs/superpowers/plans/2026-08-21-hachiware-wpf-vertical-slice.md` — the detailed 14-task TDD implementation plan used to build the Hachiware slice originally.
- Usagi port and SoundPlayer refactor completed in August 2026 session.

## Known issues / tracked technical debt (not yet fixed, all non-blocking for current 3-character functionality)

1. **App has no DPI-awareness manifest.** The whole codebase has several places that convert between WPF's DIP coordinate space and `System.Windows.Forms.Screen`'s physical-pixel space using a scale factor computed from `SystemParameters.PrimaryScreenWidth / Screen.PrimaryScreen.Bounds.Width` (see `GetDipScale()` in `CharacterWindow.xaml.cs`). This works and is tested, but it's a workaround, not a proper fix — the correct long-term fix is to declare Per-Monitor-V2 DPI awareness for the app (an `app.manifest` entry) and then remove all the scale-factor conversions, since WPF would then report real physical pixels directly. This is a bigger, separate piece of work (touches every screen-coordinate calculation in `CharacterWindow.xaml.cs`) — do NOT attempt as a quick patch, do it as its own deliberate pass with its own verification.
2. **FPS/sound lookups for a mirrored-only animation direction use the literal requested name, not the "source" direction's name.** E.g. if `config.json` ever gets a `chiikawa.jumpright.fps` entry to tune Chiikawa's real jump animation, the mirrored `jumpleft` direction would silently keep using the default FPS instead of matching. Zero current impact (no such config entries exist yet for Chiikawa), but noted so it doesn't surprise someone later.
3. Several cosmetic/minor items from earlier code reviews (a `using` alias defined but not used consistently, a leftover `AssemblyInfo.cs` from project scaffolding, etc.) — genuinely not worth chasing, mentioned only for completeness.

## Next planned work

1. **DPI-awareness pass (#1 above)** — now that all three characters are ported and their screen-coordinate needs are fully known, do this as one dedicated, carefully-verified pass rather than piecemeal.
2. Address the remaining tracked-debt items (#2–#3 above) opportunistically.

## Important facts/gotchas discovered this session (read before touching related code)

- **`config.json` cannot be committed at that literal path** — the repo's root `.gitignore` has a bare `config.json` line (left over from the Python app's local-dev-config convention) which matches that basename *anywhere* in the tree per git's ignore rules. The WPF app's actual config lives at `src/YahaPet.Wpf/config.default.json` (a different, non-ignored filename) and an MSBuild `<Content Include="config.default.json"><Link>config.json</Link>...` item renames it to `config.json` only in the build output. If you need to edit the shipped config, edit `config.default.json`, not a `config.json` you create — it'll silently fail to `git add`.
- **`Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location)` breaks under `PublishSingleFile`** — `Assembly.Location` returns `""` for an embedded single-file assembly, and this throws at startup. The tray icon now loads `assets/hachiware/icons/icon.png` directly via `System.Drawing.Bitmap` + `Icon.FromHandle(bitmap.GetHicon())` instead — this also happens to look nicer (character artwork instead of the generic exe icon). If you ever need a different fallback icon-loading approach, avoid `Assembly.Location` for anything that must survive single-file publishing.
- **`SizeToContent="WidthAndHeight"` on `CharacterWindow` caused massive (10–24x) actual-render-size corruption on DPI-scaled displays**, even though the `Width`/`Height` *properties* read back correctly. This is now removed from `CharacterWindow.xaml` — the code already sets `Width`/`Height` explicitly on every sprite change (`SetSprite()`), so `SizeToContent` was redundant and was the actual cause of the corruption. If you ever add it back "to be safe," you will reintroduce this bug on any non-100%-scaled monitor.
- **`GetOrLoadFrames` (animation frame loading + left/right mirroring) must work regardless of which direction is requested first.** Chiikawa ships only `animations/jumpright` (no `jumpleft` folder or static sprite) — the original implementation only mirrored correctly if the "real" direction happened to load first, which would have hit a `KeyNotFoundException` for Chiikawa's jump immediately, and had a latent 50%-chance bug for Hachiware's walk too. This is now fixed (see `GetOrLoadFrames`, `TryGetMirroredAnimationName`, `HasDirectionalCapability` in `CharacterWindow.xaml.cs`) — the fix populates both directions from a single decode, symmetric regardless of request order. Any future character with an asymmetric asset set (animation folder for only one direction, or a static sprite for only one direction) needs this same symmetry — don't special-case a new character's loading logic, extend the existing generic path instead.
- **Jump's edge-avoidance math didn't reserve the character's own width**, unlike walk's (which already correctly subtracts `characterWidth` from its rightward boundary). This barely mattered when the boundary was the edge of the whole desktop; it became very visible once "confine to current monitor" (see below) made the boundary a monitor seam — the character's body would visibly poke into the neighboring monitor mid-jump. Fixed by reserving `_currentSpriteWidth` at the `PlayJump()` call site (not inside `BehaviorPlanner.PlanJump`, to avoid touching its well-tested core logic). If you add a similar boundary-aware behavior in the future, remember both edges need width reservation, not just the "natural" one.
- **Multi-monitor support has a tray toggle**, `限制角色只能在單一螢幕內移動` ("Restrict character to a single monitor"), default **ON**. When ON, autonomous walk/jump stay confined to whichever monitor the character is currently on (dragging it to another monitor is still always allowed — only *its own* subsequent autonomous movement is confined). When OFF, walk/jump can freely cross the whole multi-monitor virtual desktop. Implemented via `CharacterWindow.ConfineToCurrentMonitor` (static bool) and `GetWalkJumpXBoundsInDips()`.
- **The tray context menu is in Traditional Chinese.** Character names (Hachiware/Chiikawa) are kept in their original romanized form since those strings double as internal asset-folder/dictionary keys — do not translate those specifically without also updating every place that uses them as a lookup key.
- **This session did NOT have a real second physical monitor available for testing** — multi-monitor behavior was verified via runtime harnesses that directly invoke `CharacterWindow`'s private methods via reflection and manually force window positions to simulate a second monitor, cross-checked against `System.Windows.Forms.Screen.AllScreens` in this sandbox (which does happen to report 2 real monitors). If you have real multi-monitor hardware, a live sanity check (spawn a character, drag it to the second monitor, watch it walk/jump around, toggle the confinement checkbox) is still worth doing.

## Next planned work (in order, per earlier discussion)

1. **Fix the `SoundPlayerFactory` disposal issue (#2 above) before porting Usagi** — Usagi is the first character with real sound assets, so this bug becomes reachable there for the first time.
2. **Port Usagi.** This is a much bigger asset-optimization job than Hachiware/Chiikawa: `assets/usagi/` is ~139MB (vs. Chiikawa's 7.7MB, Hachiware's 4.3MB), driven by a handful of animation folders with unusually high frame counts and resolution (`dance` 194 frames/60MB, `mock` 84 frames/29MB, `walkleft` 138 frames/28MB, `danceswirl` 48 frames/16MB — all much higher-resolution source images than Hachiware/Chiikawa's). The asset pipeline (resize to a 1080p/1440p-appropriate bake target + frame-count resampling, both already built and proven on Hachiware) needs to actually be pointed at this asset set for real, which is expected to meaningfully shrink it — this was the original whole point of doing a C#/WPF rewrite in the first place (the 190MB Python release's size problem was determined early on to be almost entirely Usagi's unoptimized assets, not the Python packaging itself). Do a real design/sizing pass before running the pipeline blind — Usagi is qualitatively different enough from the first two characters that it probably deserves its own short spec, at least for the asset-optimization parameters (bake resolution, frame-stride per animation).
3. **DPI-awareness pass (#1 above)** — once all three characters are ported and their screen-coordinate needs are fully known, do this as one dedicated, carefully-verified pass rather than piecemeal.
4. Address the other tracked-debt items (#3–#5 above) opportunistically, ideally alongside the Usagi work since #2 and #3 both specifically matter more once Usagi's much larger asset set is in play.

## Workflow notes (how this session did things, for consistency if you want to keep the same rigor)

- Every behavior change to `BehaviorPlanner` (`YahaPet.Core`) was paired with new/updated xUnit tests using a `FixedRandomSource` test double (a queue-based fake `IRandomSource`) — this is the established pattern, keep using it for any new pure-logic changes.
- WPF-layer changes (`CharacterWindow.xaml.cs`, `App.xaml.cs`) have no automated tests by design; they were verified during this session via small throwaway C# console harnesses (referencing `YahaPet.Wpf.csproj` as a `ProjectReference`, constructing a real `CharacterWindow` or `App`-like tray icon setup, and using `System.Reflection` to invoke private methods directly) run from a scratch directory outside the repo, then deleted afterward. This is a reasonable pattern to reuse for verifying anything in `CharacterWindow`/`App` that can't be exercised via a normal `xUnit` test.
- Every fix in this session was verified empirically (built, ran, and specifically reproduced-then-disproved the reported symptom) before being called done — several bugs in this codebase were subtle enough (DPI/physical-pixel unit mismatches, order-dependent caching) that "looks right on reading" was not sufficient; actually running a harness caught real bugs that code review alone missed. Recommend keeping this discipline.
