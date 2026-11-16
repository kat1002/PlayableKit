# Changelog

All notable changes to this package are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [2.0.1]

### Added
- `PALunaExternalSources`: keeps the kit listed in `luna.json` > `unity.scripts.externalSources`. Luna only compiles `Assets/`, and Unity installs the kit under `Library/PackageCache/com.kat.playablekit@<hash>`, a folder that changes on every update. With a missing or stale entry the Luna build succeeded but dropped every `PA*` component without an error. The entry is now rewritten after every domain reload (any install or update), only when it is wrong; other entries and the rest of the file stay as they are. Manual run: Tools/PlayableKit/Luna/Sync luna.json External Sources.
- Luna Compliance reports an Error when `luna.json` does not list the kit's current folder.
- README: a callout at the top explaining the `luna.json` entry, plus notes in Build with Luna, Update the package and Troubleshooting.

### Fixed
- Luna build failed with CS0019 / CS0266 in `PAVideoLoopSequenceController` when the project declares its own global `TScreen` enum. Luna compiles the kit and project scripts together, so the global type shadowed `PlayableKit.UI.TScreen`; the Video namespace now aliases `TScreen` to the kit type.

## [2.0.0]

### Added
- Real UPM package layout (`Packages/com.kat.playablekit`), installable from the git URL with `?path=Packages/com.kat.playablekit`.
- One assembly definition per module: Core, Tweening, Audio, Effects, Controls, UI, Video, Flow, Editor, Tests. No dependency cycles.
- **Audio module:** `PASoundConfig` (ScriptableObject with BGM and string-id SFX entries) and `PASoundManager` (channel pool, per-sound min interval, pitch jitter, start offset, mute via `PAAudioManagerBase`).
- `PAUICoinFly` (UI coins scatter, then fly to a target), `PA_ConfettiBurst` prefab and `PA_Confetti_M` material, `PAJoystick` (implements `IMoveInput`).
- Shaders: `PA_UI_CircleReveal`, `PA_UI_BackdropBlur`, `PA_UI_GlowOutline`, `PA_FakeShadow`.
- `PALunaBuildPatcher`: automatically patches Luna's generated `LunaTemp` JavaScript (mesh-collider inertia crash in `physics3d-0.js`, VideoPlayer looping in `UnityEngine.js`). Idempotent. Manual run: Tools/PlayableKit/Luna/Apply Build Patches Now.
- `PAAudioImportOptimizer`: applies WebGL SFX / Music audio import presets.
- Video tools, ported from Playable-Ads-Fake-Video and rewritten (`Editor/Media`, ns `PlayableKit.Editor.Media`):
  - **Video Optimizer** (Tools/PlayableKit/Video): batch queue with drag & drop. Over the original it adds: keep-aspect output without black bars, no upscaling, no frame duplication for low-fps sources, an encoder speed option, automatic re-encode when over the size budget, verification that does not block the Editor, and automatic import settings.
  - **Backdrop Capture**: live, debounced preview, and sprite import sized to the output.
  - Shared `PAFfmpegLocator` (auto-detects PATH, winget, scoop, choco and Homebrew; works on macOS/Linux), `PAFfmpegProcess`, `PAFfmpegJob` (async steps with progress), `PAMediaProbe` (one JSON ffprobe call).
  - The saved FFmpeg path from the original tools is kept.
  - Encode sizing rules are covered by `PAVideoEncodePlanTests`.
  - Not ported: Playable Orientation Setup. It depends on `AspectRatioFitter`, which Luna cannot run, and on scene object names.
- `PAAudioImportOptimizer.ApplyPreset(assetPath, sfx)` is public for reuse.
- Luna Compliance now reports an unsaved active scene (Error) and a scene missing from Build Settings (Warning).
- Samples: Template Scene and Luna Config (`luna.json`).
- Drop-in install: Luna is optional (`PLAYABLEKIT_LUNA` from asmdef version defines, or `UNITY_LUNA` in Luna builds, guards every `Luna.Unity` call), and `com.unity.textmeshpro` is a declared dependency (resolves to the ugui 2.0 TMP on Unity 6). Compile-tested in empty Unity 2022.3 and Unity 6 projects and in a 2022.3 project with Playworks 7.2.
- Root README rewritten as the full guide: install, scene setup, config, sound, Luna build, migration, updating, troubleshooting, contact.
- README section on migrating projects that contain a vendored copy of the kit.
- `PALog` logger with levels (`PALogLevel`: Verbose, Info, Warning, Error, None), tags, Console context objects, preconfigured `MinLevel` (Verbose in Editor, Info in builds) and the `PA_LOG_OFF` define. Includes `PALog.Log(cls, msg[, state])`, the same call shape as the per-project PALog, with Luna play time in each line. The `PAConfig` scene component sets log on/off, level and time. Every kit runtime log now goes through it with the `[PA][Tag]` prefix; Tweening and Effects reference Core for it.

### Changed
- Back-ported from StickiiPA: `PALunaManager` pauses audio through `AudioListener.volume`; `PAUIShine` supports 9-sliced images (`_ShineSliceUV`, `_ShineSliceDrawn`); `PADragScroll.ScrollTo(float, bool)` and `IsMoving`.
- All menu items live under `Tools/PlayableKit/`.
- `PABaseUIController` reads `Orientation.IsLandscape` in Awake and polls it every frame, calling `OnOrientationChanged` on a flip. No orientation source or scene checker needed.
- `PAScreenOrientationChecker` and `PAOrientationLayout` use `Orientation.IsLandscape` as the single definition of landscape.
- `PAPackageExporter` exports `Packages/com.kat.playablekit` (`Samples~` is not included).
- Docs rewritten: package README, Setup, UIKit.

### Removed
- `IOrientationSource`, `PAGameEventOrientationSource`, `IStoreGateway`, `ISteerInput`, and the `PABaseUIController.Store` property. `PALunaStoreGateway` stays as a plain component.
- `Assets/Reference/` (`SoundManager.cs.txt`, a stray `luna.json`). Use `PASoundManager` and the Luna Config sample instead.
- The `Tweens/` folder (merged into `Tweening/Components`) and the standalone `Assets/PlayableKit` layout.

### Breaking changes
Namespaces changed, script GUIDs did not: existing scenes and prefabs keep their references.

| Old | New |
|---|---|
| `Assets/PlayableKit/**` | `Packages/com.kat.playablekit/**` (GUIDs unchanged) |
| `PlayableKit.UI.PAUIControllerBase` | `PlayableKit.Flow.PAGameUIController`. Now derives from `PABaseUIController`: override `OnControllerAwake`, `OnControllerUpdate` and `OnOrientationChanged` instead of `Awake` and `Update`. |
| `PlayableKit.UI.PACtaClickForwarder` | `PlayableKit.Flow.PACtaClickForwarder` |
| `PlayableKit.PAGameManagerBase` | `PlayableKit.Flow.PAGameManagerBase` |
| `PlayableKit.PAAudioManagerBase` | `PlayableKit.Audio.PAAudioManagerBase` |
| `PlayableKit.PACameraShake`, `PAFreezeSpineRigAfterWarmup`, all `PAUI*` effects, `PAFallWindFx`, `PAFlashTrailRendererDriver`, `PAScratchSpriteRenderer`, `IWindFxReceiver` | `PlayableKit.Effects.*` |
| `PlayableKit.UI.PAUITapHand`, `PlayableKit.UI.PABillboardToCamera` | `PlayableKit.Effects.*` |
| `PlayableKit.PADoFadeAToB`, `PADoMoveAToB`, `PADoScaleAToB`, `PALunaTweenAnimation`, `PATweenButton`, `PATweenRotateButton`, `PAUiSlide`, `PAUiPageSlide` | `PlayableKit.Tweening.*` |
| `PlayableKit.UI.PAHoldButton`, `PADragScroll`, `PAUiDragRotate`, `IDragRotateTarget`, `IMoveInput` | `PlayableKit.Controls.*` |
| `PlayableKit.PlayableLog` | `PlayableKit.PALog` (`Info(msg)`, `Click`, `Cta`, `Tap` kept; prefix `[LunaLog]` is now `[PA]`) |
| `PlayableKit.LunaAnalyticsSink` | `PlayableKit.Analytics.LunaAnalyticsSink` |
| `IOrientationSource`, `PAGameEventOrientationSource` | Removed. Override `PABaseUIController.OnOrientationChanged`. |
| `IStoreGateway`, `PABaseUIController.Store` | Removed. Use the protected `GoToStore()` or the `PALunaStoreGateway` component. |
| `PABaseUIController.Cta` (CtaPresenter property) | `PABaseUIController.CtaPanel`. `Cta()` is now only the CTA button method on `PAGameUIController`. |
| `ISteerInput` | Removed |
| Menus `Tools/PA`, `Tools/Sp Unity`, `Tools/MyLunaHelper` | `Tools/PlayableKit/...` |
| `Assets/Reference/*` | Removed. Use `PASoundManager` and `Samples~/LunaConfig`. |

After updating, add the matching `using PlayableKit.<Module>;` lines to your own scripts.

## [1.0.2]
- Consolidated the toolkit under `Assets/PlayableKit`, decoupled TextMeshPro, merged editor tools and re-exported the package.

## [1.0.1]
- Added the scene template tool.
