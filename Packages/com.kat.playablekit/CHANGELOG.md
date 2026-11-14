# Changelog

All notable changes to this package are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [2.0.0]

### Added
- Real UPM package layout (`Packages/com.kat.playablekit`), installable from the git URL with `?path=Packages/com.kat.playablekit`.
- One assembly definition per module: Core, Tweening, Audio, Effects, Controls, UI, Video, Flow, Editor, Tests. No dependency cycles.
- **Audio module:** `PASoundConfig` (ScriptableObject with BGM and string-id SFX entries) and `PASoundManager` (channel pool, per-sound min interval, pitch jitter, start offset, mute via `PAAudioManagerBase`).
- `PAUICoinFly` (UI coins scatter, then fly to a target), `PA_ConfettiBurst` prefab and `PA_Confetti_M` material, `PAJoystick` (implements `IMoveInput`).
- Shaders: `PA_UI_CircleReveal`, `PA_UI_BackdropBlur`, `PA_UI_GlowOutline`, `PA_FakeShadow`.
- `PALunaBuildPatcher`: automatically patches Luna's generated `LunaTemp` JavaScript (mesh-collider inertia crash in `physics3d-0.js`, VideoPlayer looping in `UnityEngine.js`). Idempotent. Manual run: Tools/PlayableKit/Luna/Apply Build Patches Now.
- `PAAudioImportOptimizer`: applies WebGL SFX / Music audio import presets.
- Luna Compliance now reports an unsaved active scene (Error) and a scene missing from Build Settings (Warning).
- Samples: Template Scene and Luna Config (`luna.json`).

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
