# PlayableKit

A standalone grab-bag of generic playable-ad effects, tween helpers, small UI behaviours, diagnostics, and SDK-agnostic analytics.
Sister kit of `PlayableUIKit` (which owns UI *controllers*); this one owns reusable *pieces*.
Copy the whole folder into a new Unity project's `Assets/` and it compiles immediately — `Playable.Tweening` (`Runtime/Tweening/TweenLite.cs`) is bundled inside the kit, so no external DOTween installation is required.
Every kit type lives in `namespace PlayableKit` (or `PlayableKit.Analytics`) and none depends on a game-specific type or ad SDK.

## Contents

### Runtime/Effects

| Type | Job |
|------|-----|
| `UIShine` | Sweeping shine band across a UI `Image` (needs the `UI_Shine_M` material). Update-driven, no Sequence (Luna-safe). |
| `UIStarTwinkle` | Twinkling stars around an element: clones an inactive template `Image` child N times in `Awake`, then loops pop-in, spin, hold, pop-out at random spots. Update-driven, no Sequence (Luna-safe). |
| `UIPathTwinkle` | Sparkle wave along a curved path: stars pop in one after another (Arc, cubic Bezier, or draggable `P*` child points), evenly spaced by arc length, then pop out and loop. Clones an inactive template `Image` in `Awake`. Update-driven, no Sequence (Luna-safe). |
| `UIIdleMotion` | Endless idle motion from optional sine channels: bob (Y), sway (Z tilt), pulse (scale), fade (`CanvasGroup` alpha). Update-driven, restores base pose in `OnDisable`. |
| `UIProgressFillFX` | Non-squashing progress bar fill revealed by `UI_ProgressFill.shader` (`Progress` 0..1 masks from the left, with animated stripes, gloss, and leading-edge glow). `[ExecuteAlways]`, Update-driven. |
| `UIPraiseFly` | Pooled fan-spread praise text/sprite fly-up effect (`Play(int stepIndex)` or `Play(Sprite)`). |
| `UIStarBurst` | Pooled one-shot fan burst of stars rising, spinning, and falling (`Play()`). |
| `UITapRing` | Smooth tap ripple: an `Image` scales up (ease-out) and fades in then out; `Play()`, `Play(Vector2)`, `Stop()`, optional `loop`/`loopInterval`; unscaled time, no tweens. Sprite: `Sprites/Ring`. |
| `UIPixelRingPop` | Pooled pixel-stepped expanding ring pop (`Play(Vector2 anchoredPos)`). |
| `UIPixelSmokeBurst` | Pooled pixel-stepped smoke burst (`Burst(Vector2 anchoredPos, int count, float minDist, float maxDist)`). |
| `UITweenRotate` | Endless Z spin (e.g. light rays) with an optional scale pulse; starts in `OnEnable`, killed in `OnDisable`. |
| `FlashTrailRendererDriver` | Drives a `TrailRenderer` for a quick speed-reactive flash/streak. |
| `FallWindFx` | Wind/speed-line feel while something falls: `Begin(Transform target)` then `Land()` or `StopImmediate()`. |
| `IWindFxReceiver` | Optional contract (`SetWindIntensity`, `Kick`) so a camera or rig can react to `FallWindFx`. |

### Runtime/Tweens

| Type | Job |
|------|-----|
| `DoScaleAToB` | Tween scale A to B; optional loop (count, loop type), play-on-enable, `ignoreTimeScale`, `SizeA`, `Play()` / `Stop()` / `SetSizes()`. |
| `DoMoveAToB` | Tween move A to B (local or world position). |
| `DoFadeAToB` | Tween `CanvasGroup` alpha fade A to B. |
| `ButtonRotate` | Minimal endless 360° Z spin started in `Start` (1 s per turn). `UITweenRotate` is the configurable version. |
| `UiSlide` | Slides a `RectTransform` to a Y (or X) position (`SnapTo`/`SlideTo`). |
| `UiPageSlide` | Slides a page in/out (`Show`/`Hide`/`SnapActive`). |

### Runtime/UI

| Type | Job |
|------|-----|
| `UITapHand` | Tap-instruction hand: swaps an `Image` between a raised and a pressed sprite (`Sprites/TapHand_1` / `TapHand_2`) on a loop, with a small scale dip and optional `UITapRing`; unscaled time, no tweens. |
| `HoldButton` | Press-and-hold control exposing `IsHeld`, with an optional handle that slides down while held; needs a raycast-target `Graphic`. |
| `UiDragRotate` | Drag on a UI area to rotate something. |
| `IDragRotateTarget` | Optional contract (`Hold(bool)`, `Drag(float dx, float dy)`) for what gets rotated. |
| `BillboardToCamera` | Keeps a transform facing the main camera. |

### Runtime/Tweening

| Type | Job |
|------|-----|
| `TweenLite` (`Playable.Tweening`) | Self-contained, Luna-safe tween engine (`TweenLite.Sequence()`, `DOVirtual`, `DOScale`, `DOMove`, `DOLocalMove`, `DORotate`, `DOLocalRotate`, `DOAnchorPos`, `DOFade`, `DOColor`, `DOFillAmount`, `DOJump`, `DOShakePosition`, `TweenLite.ApplyEase`). |

### Runtime/Diagnostics

| Type | Job |
|------|-----|
| `PlayableLog` | Static console logger (also shows in the Luna browser console): `Info`, `Click`, `Cta`, `Tap(screenPos)` (logs the topmost UI object under the tap). Set `PlayableLog.Enabled = false` before shipping. |

### Runtime/Analytics

| Type | Job |
|------|-----|
| `PlayableAnalytics` | Static SDK-agnostic hub: `LevelStart/LevelWon/LevelFailed/LevelRetry(int)`, `TutorialStarted/TutorialComplete/EndCardShown()`, `Score(int)`, `Custom(name, int)`, `Button(name)` (sends `btn_<name>` = 1). Logs via `PlayableLog` (`LogToConsole`), forwards to sinks, raises `Tracked`. |
| `IAnalyticsSink` | `Send(kind, name, value)`; implement for an SDK and register with `PlayableAnalytics.AddSink`. |
| `AnalyticsEventKind` | Standard event kinds plus `Custom`. |
| `AnalyticsButton` | Add to a `Button`; sends `btn_<eventName>` (or the GameObject name) on click. |

### Editor

| Type | Job |
|------|-----|
| `UIShineEditor` | Inspector for `UIShine` with Play Shine / Stop buttons in play mode. |

### Shaders / Materials / Sprites

| Asset | Job |
|-------|-----|
| `Shaders/UI_Shine.shader` + `Materials/UI_Shine_M.mat` | UI shine shader (`PlayableKit/UI_Shine`) and material; assign to the `Image` that has `UIShine`. |
| `Shaders/UI_ProgressFill.shader` + `Materials/UI_ProgressFill_M.mat` | Progress fill shader (`PlayableKit/UI_ProgressFill`) and material; assign to the `Image` that has `UIProgressFillFX`. |
| `Sprites/Star.png`, `Sprites/Star2.png`, `Sprites/Light.png` | Stars for `UIStarTwinkle` / `UIPathTwinkle` / `UIStarBurst` templates; soft light sprite. |
| `Sprites/TapHand_1.png`, `Sprites/TapHand_2.png` | Raised / pressed hand sprites for `UITapHand`. |
| `Sprites/Ring.png` | Ring sprite for `UITapRing`. |

## Dependencies & Coexistence with DOTween

- **UGUI** (`UnityEngine.UI`).
- **Bundled `Playable.Tweening`** (`Runtime/Tweening/TweenLite.cs`).
- **Projects that also have DOTween installed**: `Playable.Tweening` uses DOTween-compatible type and extension-method names (`Tween`, `Sequence`, `Ease`, `LoopType`, `DOVirtual`, `DOScale`, etc.), so never put `using DG.Tweening;` and `using Playable.Tweening;` in the same file. Alias one of them (`using PT = Playable.Tweening;`) if a file needs both, or delete `Runtime/Tweening/TweenLite.cs` if you prefer to compile against `DG.Tweening`.
