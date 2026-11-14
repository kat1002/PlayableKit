# PlayableKit

Luna / Playworks-safe toolkit for Unity playable ads: lifecycle and store handling, analytics, a built-in tween engine (`TweenLite`), audio, orientation-aware UI, effects, video, input controls, editor compliance tools and automatic Luna build patches.

Package name `com.kat.playablekit` · version 2.0.0 · see [CHANGELOG](CHANGELOG.md).

## Requirements

| Prerequisite | Notes |
|---|---|
| Unity 2021.3 or later | |
| **Luna 6.4** or **Playworks 7.2** | Must already be installed in the project. The asmdefs reference the `Unity.Luna` assembly by name. Luna is not a package dependency because it is usually installed from a local `file:` path. |
| **TextMeshPro** | Unity 2021/2022: the `com.unity.textmeshpro` package (in the default templates). Unity 6: part of `com.unity.ugui`. The UI and Flow asmdefs reference `Unity.TextMeshPro`. |

`com.unity.ugui` and the audio, particle system and video modules are pulled in automatically.

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
| `Runtime/Core` | `PlayableKit.Core` / `PlayableKit` | `PALunaManager`, `PAStoreReturn`, `PALunaStoreGateway`, `GameEventManager`, `GameSessionState`, `GameState`, `EndReason`, `Orientation`, `PlayableLog`; `Analytics/` (`PlayableAnalytics`, `IAnalyticsSink`, `LunaAnalyticsSink`, `PAAnalyticsButton`, ns `PlayableKit.Analytics`) | Unity.Luna, ugui |
| `Runtime/Tweening` | `PlayableKit.Tweening` | `TweenLite` (DOTween-style fluent API, no external library); `Components/`: `PADoFadeAToB`, `PADoMoveAToB`, `PADoScaleAToB`, `PALunaTweenAnimation`, `PATweenButton`, `PATweenRotateButton`, `PAUiSlide`, `PAUiPageSlide` | ugui |
| `Runtime/Audio` | `PlayableKit.Audio` | `PAAudioManagerBase`, `PASoundConfig`, `PASoundManager` | Core |
| `Runtime/Effects` | `PlayableKit.Effects` | `PAUIShine`, `PAUIStarBurst`, `PAUIStarTwinkle`, `PAUITapRing`, `PAUIPixelRingPop`, `PAUIPixelSmokeBurst`, `PAUIPraiseFly`, `PAUIProgressFillFX`, `PAUIIdleMotion`, `PAUIPathTwinkle`, `PAUITweenRotate`, `PAUITapHand`, `PAUICoinFly`, `PAFallWindFx`, `PAFlashTrailRendererDriver`, `PAScratchSpriteRenderer`, `PACameraShake`, `PAFreezeSpineRigAfterWarmup`, `PABillboardToCamera` | Tweening, ugui |
| `Runtime/Controls` | `PlayableKit.Controls` | `IMoveInput`, `IDragRotateTarget`, `PAHoldButton`, `PADragScroll`, `PAUiDragRotate`, `PAJoystick` | ugui |
| `Runtime/UI` | `PlayableKit.UI` | `PABaseUIController`, `PACtaOverlay`, `CtaPresenter`, `CountdownTimer`, `IdleTimer`, `Oriented<T>` / `OrientedFields` / `OrientedExtensions`, `PALayout`, `PAOrientationLayout`, `PAAspectFitter`, `PASafeArea`, `PAPortraitFrame`, `PAScreenOrientationChecker`, `PALoadingScreenController`, `PAMuteToggleButton`, `PATextOutline` | Core, Audio, ugui, TMP |
| `Runtime/Video` | `PlayableKit.Video` | `PAVideoBootstrap`, `PAVideoStepController`, `PAVideoLoopSequenceController`, `PAVideoOrientationSizer`, `PAAutoPlayVideoOnEnable`, `PAKeepRatio`, `VideoPlayerLoopUtility` | Core, UI, Unity.Luna |
| `Runtime/Flow` | `PlayableKit.Flow` | `PAGameManagerBase`, `PAGameUIController`, `PACtaClickForwarder` | Core, UI, Audio, Effects, Tweening, TMP |
| `Editor` | `PlayableKit.Editor` | Editor tools (see below) | UI, Controls, Effects |
| `Tests/Editor` | `PlayableKit.Tests.Editor` | `GameSessionStateTests`, `LunaManagerSessionTests` | Core |

Dependency graph: Tweening and Core are leaves. Audio and UI sit on Core; Effects on Tweening; Video on Core + UI; Flow on top of everything.

## Quick start

1. Package Manager > PlayableKit > Samples > import **Template Scene**.
2. Drop `Prefabs/PALunaRoot.prefab` into your scene (it holds `PALunaManager` and the CTA UI).
3. Subclass `PAGameManagerBase` (game flow hooks) and `PAGameUIController` (overlay UI).
4. Add a `PASoundManager` and assign a `PASoundConfig` asset (*Create > PlayableKit > Sound Config*).
5. Run **Tools > PlayableKit > Luna Compliance** and **Playable Ads Checker** before building.

More: [Documentation~/Setup.md](Documentation~/Setup.md) and [Documentation~/UIKit.md](Documentation~/UIKit.md).

## Editor tools

| Menu | What it does |
|---|---|
| Tools/PlayableKit/PlayableKit Control Center | Hub window (Ctrl+Shift+K) for the tools below |
| Tools/PlayableKit/Sanitize UI For Luna | Rewrites UI that Luna cannot run (ScrollRect, LayoutGroup, Mask, ...) |
| Tools/PlayableKit/Playable Ads Checker | Project audit: missing scripts, asset and code issues |
| Tools/PlayableKit/Luna Compliance | Luna rule scan, including "scene saved and in Build Settings" |
| Tools/PlayableKit/Spine Binary Inspector | Scans Spine `.skel` binaries for Luna issues |
| Tools/PlayableKit/Audio Import Optimizer | Applies WebGL SFX / Music audio import presets to selected clips |
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

## Luna gotchas

- No generic `[Serializable]` fields. `Oriented<T>` is runtime-only; serialize `OrientedFields` types instead.
- No `AudioListener.pause` in Luna. `PALunaManager` pauses by lowering `AudioListener.volume`.
- No `PlayerPrefs`.
- No logic that depends on try/catch, and no lambdas or LINQ in per-frame code.
- Shaders: built-in render pipeline only. No Shader Graph, no `#pragma target` above 3.0.
- ParticleSystems render in world space or under a Screen Space - Camera canvas, not on Overlay canvases.
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
