# PlayableKit

Luna / Playworks-safe toolkit for Unity playable ads: lifecycle and store handling, analytics, a built-in tween engine (`TweenLite`), audio, orientation-aware UI, effects, video, input controls, editor compliance tools and automatic Luna build patches.

Package name `com.kat.playablekit` · version 2.1.0 · see [CHANGELOG](CHANGELOG.md).

> [!IMPORTANT]
> **Luna only compiles `Assets/`. The kit is a package, so Luna builds it only when `luna.json` lists its folder.**
>
> If it isn't listed, the Luna build still succeeds, but every `PA*` component is silently dropped: tweens don't play, buttons do nothing, and nothing shows in the Console.
>
> The kit lists itself. On every Editor load (so after each install and update) it writes its current folder into `luna.json` > `unity.scripts.externalSources`, for example `.\Library\PackageCache\com.kat.playablekit@<hash>`. The hash changes with every update, so **don't type that path by hand**. What you do:
> 1. Keep `luna.json` in the project root and **commit it** after the Console shows `[PlayableKit] luna.json externalSources now lists the kit ...`.
> 2. **Commit `Assets/PlayableKitLuna~/`.** The kit writes one empty stub `.asmdef` there per runtime assembly. Luna 7.2 also passes every `Library/ScriptAssemblies` DLL to its compiler and only leaves one out when an `.asmdef` with the same file name exists under `Assets/`. Without the stubs, Luna compiles the kit twice (source plus `PlayableKit.*.dll`) and fails with `CS0121` on every `TweenExtensions` call. Unity ignores folders ending in `~`, so the Editor build never sees the stubs.
> 3. Before each build, run **Tools > PlayableKit > Luna Compliance**. It reports an error when the entry is missing or stale, or when a stub is missing.
> 4. To fix it right away: **Tools > PlayableKit > Luna > Sync luna.json External Sources** (also rewrites the stubs).

## Requirements

| Prerequisite | Notes |
|---|---|
| Unity 2021.3 or later | Compile-tested in empty 2022.3 and Unity 6 projects. |
| **Luna 6.4** or **Playworks 7.2** (optional) | The kit compiles without it. When `com.unity.playworks.upp` or `uk.lunalabs.luna` is installed, the asmdefs define `PLAYABLEKIT_LUNA`, and Luna's own build defines `UNITY_LUNA`; either one enables the real Luna calls. Without Luna, lifecycle and analytics calls are no-ops and `InstallFullGame` only logs. Luna is not a package dependency because it is installed from a local `file:` path. |

Pulled in automatically: `com.unity.ugui`, `com.unity.textmeshpro` (on Unity 6 this resolves to the built-in TMP inside ugui 2.0), and the audio, particle system and video modules. Run **Window > TextMeshPro > Import TMP Essential Resources** once if the project has no TMP font yet.

## Migrating a project that already has a copy of the kit

The package uses the same namespaces, class names and script GUIDs as the old vendored copies, so the two must not live side by side (duplicate types, "ambiguous reference" CS0433, GUID clashes).

1. Delete the old copy first. Known locations: `Assets/PlayableKit` (StickiiPA, CraftPlayable), `Assets/_Template/PlayableKit` + `Assets/_Template/PlayableUIKit` (GangterCity), `Assets/Models/PlayableKit` (Obby-Playable-Ads).
2. Install the package (below). Copies that were 1.x `Assets/PlayableKit` keep their scene references, because the GUIDs are unchanged.
3. Add the new `using PlayableKit.<Module>;` lines to your scripts. Old-layout names (`LunaManager`, `UIShine`, `DoScaleAToB`, ...) map to the `PA*` types; see the migration tables in [CHANGELOG](CHANGELOG.md) and `Docs/ProjectScanReport.md` in the repo.

## Install

Pick one.

1. **Git URL.** Package Manager > Add package from git URL:
   `https://github.com/kat1002/PlayableKit.git?path=Packages/com.kat.playablekit`
   Pin a release with `#v2.0.0` at the end.
2. **manifest.json.** Add to `dependencies`:
   ```json
   "com.kat.playablekit": "https://github.com/kat1002/PlayableKit.git?path=Packages/com.kat.playablekit"
   ```
3. **.unitypackage.** Import `PlayableKit.unitypackage` from the repo root. The files land in `Packages/com.kat.playablekit`.

## Modules

One assembly per module, no dependency cycles. Core keeps the root namespace `PlayableKit`; every other module uses `PlayableKit.<Module>`.

| Folder | Assembly / namespace | Contents | References |
|---|---|---|---|
| `Runtime/Core` | `PlayableKit.Core` / `PlayableKit` | `PALunaManager`, `PAStoreReturn`, `PALunaStoreGateway`, `GameEventManager`, `GameSessionState`, `GameState`, `EndReason`, `Orientation`, `PALog` / `PALogLevel`, `PAConfig`; `Analytics/` (`PlayableAnalytics`, `IAnalyticsSink`, `LunaAnalyticsSink`, `PAAnalyticsButton`, ns `PlayableKit.Analytics`) | Unity.Luna (optional), ugui |
| `Runtime/Tweening` | `PlayableKit.Tweening` | `TweenLite` (DOTween-style fluent API, no external library); `Components/`: `PADoFadeAToB`, `PADoMoveAToB`, `PADoScaleAToB`, `PALunaTweenAnimation`, `PATweenButton`, `PATweenRotateButton`, `PAUiSlide`, `PAUiPageSlide` | Core, ugui |
| `Runtime/Audio` | `PlayableKit.Audio` | `PAAudioManagerBase`, `PASoundConfig`, `PASoundManager` | Core |
| `Runtime/Effects` | `PlayableKit.Effects` | `PAUIShine`, `PAUIStarBurst`, `PAUIStarTwinkle`, `PAUIFallingFx` (snow / rain / any falling sprite, Image pool), `PAUITapRing`, `PAUIPixelRingPop`, `PAUIPixelSmokeBurst`, `PAUIPraiseFly`, `PAUIProgressFillFX`, `PAUIIdleMotion`, `PAUIPathTwinkle`, `PAUITweenRotate`, `PAUITapHand`, `PAUICoinFly`, `PAFallWindFx`, `PAFlashTrailRendererDriver`, `PAScratchSpriteRenderer`, `PACameraShake`, `PAFreezeSpineRigAfterWarmup`, `PABillboardToCamera` | Core, Tweening, ugui |
| `Runtime/Controls` | `PlayableKit.Controls` | `IMoveInput`, `IDragRotateTarget`, `PAHoldButton`, `PADragScroll`, `PAUiDragRotate`, `PAJoystick` | ugui |
| `Runtime/UI` | `PlayableKit.UI` | `PABaseUIController`, `PACtaOverlay`, `CtaPresenter`, `CountdownTimer`, `IdleTimer`, `Oriented<T>` / `OrientedFields` / `OrientedExtensions`, `PALayout`, `PAOrientationLayout`, `PAAspectFitter`, `PASafeArea`, `PAPortraitFrame`, `PAScreenOrientationChecker`, `PALoadingScreenController`, `PAMuteToggleButton`, `PATextOutline` | Core, Audio, ugui, TMP |
| `Runtime/Video` | `PlayableKit.Video` | `PAVideoBootstrap`, `PAVideoStepController`, `PAVideoLoopSequenceController`, `PAVideoOrientationSizer`, `PAAutoPlayVideoOnEnable`, `PAKeepRatio`, `VideoPlayerLoopUtility` | Core, UI, Unity.Luna |
| `Runtime/Flow` | `PlayableKit.Flow` | `PAGameManagerBase`, `PAGameUIController`, `PACtaClickForwarder` | Core, UI, Audio, Effects, Tweening, TMP |
| `Editor` | `PlayableKit.Editor` (+ `PlayableKit.Editor.Media` in `Editor/Media`: FFmpeg locator / process / probe, video optimizer, backdrop capture) | Editor tools (see below) | UI, Controls, Effects |
| `Tests/Editor` | `PlayableKit.Tests.Editor` | `GameSessionStateTests`, `LunaManagerSessionTests`, `PALunaExternalSourcesTests`, `PAVideoEncodePlanTests`, `PAUIFallingFxTests` | Core, Effects, Editor |

Dependency graph: Core and Controls are leaves. Tweening, Audio and UI sit on Core; Effects on Core + Tweening; Video on Core + UI; Flow on top of everything.

## Quick start

1. Package Manager > PlayableKit > Samples > import **Template Scene**.
2. Drop `Prefabs/PALunaRoot.prefab` into your scene (it holds `PALunaManager` and the CTA UI).
3. Subclass `PAGameManagerBase` (game flow hooks) and `PAGameUIController` (overlay UI).
4. Add a `PASoundManager` and assign a `PASoundConfig` asset (*Create > PlayableKit > Sound Config*).
5. Run **Tools > PlayableKit > Luna Compliance** and **Playable Ads Checker** before building.

More: [Documentation~/Setup.md](Documentation~/Setup.md), [Documentation~/UIKit.md](Documentation~/UIKit.md) and [Documentation~/BuildSelector.md](Documentation~/BuildSelector.md).

## Editor tools

| Menu | What it does |
|---|---|
| Tools/PlayableKit/PlayableKit Control Center | Hub window (Ctrl+Shift+K) for the tools below |
| Tools/PlayableKit/Luna/Build Selector | Prepares luna.json for the open scene from the project's profile registry (scene, creative name, script excludes, texture overrides). Why and how: [Documentation~/BuildSelector.md](Documentation~/BuildSelector.md) |
| Tools/PlayableKit/Sanitize UI For Luna | Rewrites UI that Luna cannot run (ScrollRect, LayoutGroup, Mask, ...) |
| Tools/PlayableKit/Playable Ads Checker | Project audit: missing scripts, asset and code issues |
| Tools/PlayableKit/Luna Compliance | Luna rule scan, including "scene saved and in Build Settings" |
| Tools/PlayableKit/Spine Binary Inspector | Scans Spine `.skel` binaries for Luna issues |
| Tools/PlayableKit/Audio Import Optimizer | Applies WebGL SFX / Music audio import presets to selected clips |
| Tools/PlayableKit/Video/Video Optimizer | Batch-encodes videos into browser-safe H.264 (Baseline, no B-frames, yuv420p) under a size budget: two-pass, keeps aspect without black bars, never upscales or adds frames, separate MP3 option, ffprobe + decode verification, automatic re-encode when over budget, imports the results (no Unity transcoding; MP3 gets the Music audio preset). Needs FFmpeg + FFprobe. |
| Tools/PlayableKit/Video/Backdrop Capture | Bakes one video frame into a cover-cropped, blurred (optionally darkened) JPG sprite to use behind a letterboxed video. Live preview. Needs FFmpeg. |
| Tools/PlayableKit/Luna/Apply Build Patches Now | Forces `PALunaBuildPatcher` to run (see below) |
| Tools/PlayableKit/Export PlayableKit.unitypackage | Exports the package folder (`Samples~` is not included) |

### PALunaBuildPatcher

Luna generates JavaScript under `LunaTemp/` after each build, and two of those files ship with bugs. `PALunaBuildPatcher` fixes them for you:

- It watches `LunaTemp/` (checked every few seconds in the Editor, never during play mode or compilation) and patches the generated files as soon as they appear or change.
- **Physics:** `physics3d-0.js` has a mesh-collider inertia crash. The patch makes it safe.
- **Video:** `UnityEngine.js` ignores `VideoPlayer.isLooping`. Three small patches make video looping work.
- It is **idempotent**: already-patched files are recognised and left alone, so it is safe to run repeatedly.
- It does **nothing when the Luna 7.2 strings do not match** (another Luna version, or a file Luna already fixed). It never guesses.
- Each patched file is logged in the Console.
- Run it by hand any time from **Tools/PlayableKit/Luna/Apply Build Patches Now**; the log reports how many files were patched.

## Logging (`PALog`, `PAConfig`)

All kit logs go through `PALog` (Core, namespace `PlayableKit`). Format: `[PA][Class] 3.20s | message | state`. The time is the Luna play time, or `pre` before the first tap. The lines also show in the Luna build's browser console.

```csharp
PALog.Log("MyGame", "picked item", "score=3");   // game-state line, same call as the old per-project PALog
PALog.Info("MyGame", "Level started");
PALog.Warn("MyGame", "No target", this);         // context object is clickable in the Console
PALog.Error("MyGame", "Missing ref", this);
PALog.Verbose("MyGame", "Per-step detail");      // Editor only by default
PALog.Click("PlayButton"); PALog.Cta("EndCard"); PALog.Tap(Input.mousePosition);
```

| Setting | Default | Override |
|---|---|---|
| `PALog.Enabled` | `true` | `PAConfig` > Enable Game Log |
| `PALog.MinLevel` | `Verbose` in the Editor, `Info` in builds | `PAConfig` > Min Log Level |
| `PALog.ShowTime` | `true` | `PAConfig` > Show Log Time |
| Define `PA_LOG_OFF` | not set | Silences every log at compile time |

`PAConfig` is a scene component. It applies its values on Awake, with execution order -1000.

## Luna gotchas

- No generic `[Serializable]` fields. `Oriented<T>` is runtime-only; serialize `OrientedFields` types instead.
- No `AudioListener.pause` in Luna. `PALunaManager` pauses by lowering `AudioListener.volume`.
- No `PlayerPrefs`.
- No logic that depends on try/catch, and no lambdas or LINQ in per-frame code.
- Shaders: built-in render pipeline only. No Shader Graph, no `#pragma target` above 3.0.
- ParticleSystems render in world space or under a Screen Space - Camera canvas, not on Overlay canvases. On an Overlay canvas use an Image pool instead (`PAUIStarBurst`, `PAUIFallingFx`).
- Open the store synchronously inside the click handler (`PALunaManager.GoToStore()`); do not defer it.
- Check Unity objects with `== null`, not `?.` or `??`.
- Luna transpiles C# to JS, so keep runtime code simple. Editor code is excluded by `luna.json`.

## Assets

| Type | Items |
|---|---|
| Shaders (`PlayableKit/...`) | `PA_ChromaKeyGreenVideo`, `PA_PlayworksUnlitTexture`, `PA_SpriteOutline`, `PA_UI_ProgressFill`, `PA_UI_Shine`, `PA_UI_CircleReveal`, `PA_UI_BackdropBlur`, `PA_UI_GlowOutline`, `PA_FakeShadow` |
| Materials | `PA_UI_ProgressFill_M`, `PA_UI_Shine_M`, `PA_Confetti_M` |
| Prefabs | `PALunaRoot`, `PACtaButton`, `PA_ConfettiBurst` (ParticleSystem only; call `Play()`) |
| Sprites | `PA_Light`, `PA_Ring`, `PA_Star`, `PA_Star2`, `PA_TapHand_1`, `PA_TapHand_2` |
| Samples | `Template Scene`, `Luna Config` (starter `luna.json`) |

## Upgrading from 1.x

See the rename/move table in the [CHANGELOG](CHANGELOG.md#breaking-changes).
