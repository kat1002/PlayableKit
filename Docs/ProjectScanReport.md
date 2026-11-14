# Project scan report

Scan of the playable-ad projects under `D:\Phuc\Projects`, done to decide what belongs in PlayableKit 2.0.0. Scans were read-only. Script purposes are mostly inferred from file names unless stated; see [Known gaps](#known-gaps).

Scanned: BikePlayable, CraftPlayable, GangterCity, Home-Clean-Playable / SatisFix, perfectcleaning (SatisFix), mukbang_pa, Obby-Playable-Ads, PlayableStarterKit, scoop_pa, squishy_pa, StickiiPA, VideoPlayer, Playable-Ads-Fake-Video, and the loose `Package/` folder.

## Summary

| Project | Unity | Luna / Playworks | PlayableKit vendored |
|---|---|---|---|
| BikePlayable | 2022.3.62f3 | Playworks 7.2 (`file:` path) | No (own `_Template`, DOTween) |
| CraftPlayable | 6000.0.79f1 | Playworks 7.2 | Yes, folder copy (`Assets/PlayableKit`) |
| GangterCity | 2022.3.62f2 | Playworks 7.2 | Yes, `_Template/PlayableKit` + `PlayableUIKit` |
| Home-Clean-Playable / SatisFix | 2022.3.25f1 | Playworks 7.2 | No |
| perfectcleaning (SatisFix) | 2022.3.25f1 | Playworks 7.2 | Not confirmed |
| mukbang_pa (IdolMukbang) | 2022.3.62f2 | Playworks 7.2 | Not confirmed (PA_UI_Shine copy found) |
| Obby-Playable-Ads | 2022.3.62f3 | Playworks 7.2 | Not confirmed |
| PlayableStarterKit | not a project | targets 7.2, DOTween | Loose script library (source of the 1.x layout) |
| scoop_pa (ScoopAndPack) | 2022.3.62f2 (AGENTS.md says 6000.2.7f2) | Luna 6.4.0 | Partial (11 pattern matches) |
| squishy_pa (SquishyDumpling) | 2022.3.62f2 | Playworks 7.2 | Not confirmed |
| StickiiPA | 6000.0.79f1 | Playworks 7.2 | Yes, folder copy (`Assets/PlayableKit`), newest fixes |
| VideoPlayer | 2022.3.25f1 | Playworks 7.2 | No |
| Playable-Ads-Fake-Video | 2022.3.62f3 | Playworks 7.2 | No (older renamed copies) |

## Per project

### BikePlayable
- **Found:** `_Template` core (LunaManager, UIControllerBase, GameManagerBase, AudioManagerBase, CameraShake, LoadingScreenController, MuteToggleButton, Orientation), editor preflight and export tools, game-specific traffic and car code.
- **Adopted:** the scene-saved / scene-in-Build-Settings preflight check, as `ScanSceneSaved()` in `PALunaCompliancePanel` (Error if unsaved, Warning if not in Build Settings).
- **Rejected:** `LunaPreflightValidator` and `CheckComponentPresent` (need game types); `CheckNoMissingScripts` (already in `PADependencyScannerPanel`); `CheckNoMeshColliders` (contradicts the physics patch); `CheckLunaJsonHasCompilerV2` (project policy, brittle string match); `PlayableExportTools`, `PlayableTemplateSceneBuilder`, `PlayableUIInstaller`, `PlayableSceneBuilder` (older generators, replaced by the Template Scene sample); `AudioManagerBase` (PlayerPrefs and try/catch; `PAAudioManagerBase` is its successor); `UI_WindEffect` shader.

### CraftPlayable
- **Found:** vendored PlayableKit (older layout), `Craft01AudioManager`, `SoundEnifi`, `Craft01CircleReveal.shader`, confetti prefabs, `PlayableSpiderLunaPhysicsPatch`.
- **Adopted:** channel pooling idea from `Craft01AudioManager` (first idle channel, else the oldest) into `PASoundManager`; `Craft01CircleReveal.shader` as `Shaders/PA_UI_CircleReveal`; physics patch into `PALunaBuildPatcher`.
- **Rejected:** `SoundEnifi` (trivial wrapper); `Craft01Block`, `Voxel`, `VoxelGlass` shaders (game-specific); confetti prefabs (origin unverified, likely third-party based; `PA_ConfettiBurst` was built from scratch); `Craft0x` game classes.

### GangterCity
- **Found:** `_Template/PlayableKit` + `PlayableUIKit`, `LunaFakeShadow.shader`, `LunaMapStandard*` shaders, `SoundManager` (CT Game and PlayableSpider), `Editor/LunaTools`.
- **Adopted:** `LunaFakeShadow` as `Shaders/PA_FakeShadow`; transient-source pooling idea for `PASoundManager`.
- **Rejected:** `LunaMapStandard*` (heavy lit clones, unverified); gameplay `SoundManager` cues; traffic and VenomGame code; `FinishConfetti`, `WindEffect`, `ExplosionFX` (likely third-party).
- **Not evaluated:** `Editor/LunaTools` (`LunaAssetsSyncMenu`, `LunaAssetsSyncOverlay`, `LunaConfigBridge`).

### Home-Clean-Playable / SatisFix, perfectcleaning
- **Found:** Bombus common UI and utils, `PlayableAdsCheckerWindow`, `SoundManager` (Hashtable + Pooling), many DragObject / ClickToBreak game scripts, ScratchCard and AllIn1 shader libs, Shader Graph assets.
- **Adopted:** nothing new. `PlayableAdsCheckerWindow` was already adopted as `PADependencyScannerPanel` (`diff -w` shows only class and namespace renames).
- **Rejected:** `SoundManager` (Hashtable, missing Pooling class, try blocks, static flags); ScratchCard and AllIn1 (third-party); Shader Graph files (Luna risk); level-specific FX prefabs.
- **Not evaluated:** `BaoPlayable_FindRefsInSceneWindow`.

### mukbang_pa
- **Found:** `PASmokeFx`, `PASwapFx`, `PALiveTransformFx`, `EmojiParticlePool`, `SoundManager` (static flags, Pooling). Script list was cut off.
- **Adopted:** nothing.
- **Rejected:** game-themed FX (`PAUICoinFly` and `PAUIPixelSmokeBurst` cover the generic need); `SoundManager`.

### Obby-Playable-Ads
- **Found:** `AudioManager` with string-id `SfxEntry`, min interval, pitch and start offset; `SimpleJoystick` (`VariableJoystick_PetalMeadow`); `TutorialHand`; `LunaMapStandard*`, `LunaStudWall` shaders. Script list was cut off.
- **Adopted:** SFX model into `PASoundConfig` / `PASoundManager`; joystick logic into `PAJoystick` (`IMoveInput`).
- **Rejected:** `TutorialHand` (`PAUITapHand` exists); `LunaStudWall` (URP only); Obby game classes. `SoundManagerRP` was not read.

### PlayableStarterKit
- **Found:** loose extraction of the older PlayableKit (LunaManager, GameEventManager, adapters, PlayableUIKit) and the physics patch.
- **Adopted:** nothing (superseded by the 2.0 package). Its physics patch is part of `PALunaBuildPatcher`.
- **Rejected:** adapters (`LunaStoreGateway`, `GameEventOrientationSource`), `ISteerInput`.

### scoop_pa
- **Found:** `AudioSizeFixer` (~1900 lines), `SoundConfig` (1377 lines), `CoinMoveFX`, `CountFXManager`, `MyLunaUtils` (ScratchSpriteRenderer, compliance scanner, spine scanner), `ThankyouCardBackgroundBlur.shader`.
- **Adopted:** audio import presets (SFX and Music, WebGL) into `PAAudioImportOptimizer`; `CoinMoveFX` concept into `PAUICoinFly` (rewritten without DOTween, SoundConfig, TrashMan).
- **Rejected:** ffmpeg encode, loop trim, size estimates and the Android / iOS scopes from `AudioSizeFixer`; `SoundConfig` (one method per cue); `CountFXManager`, `CoinMoveFxImage` (currency-specific, `Resources.Load`, DOTween); `ScratchSpriteRenderer` (DOTween; `PAScratchSpriteRenderer` is the successor); `SoundManager`; blur shader (overlaps `PA_UI_BackdropBlur`).

### squishy_pa
- **Found:** same Bombus set as scoop (`CoinMoveFX`, `AudioSizeFixer`, `SoundConfig`, `SoundManager`), plus `FireworkBurn` and `UIPhaseTransitionWipe` shaders.
- **Adopted:** `CoinMoveFX` concept (see scoop_pa).
- **Rejected:** `FireworkBurn`, `UIPhaseTransitionWipe` (styled for the project); `SoundConfig`, `SoundManager`.

### StickiiPA
- **Found:** newest vendored PlayableKit (script and shader GUIDs identical to this repo), `PA01_UI*` shaders, StickerKit shaders, `PAShoreFieldGenerator`.
- **Adopted (back-ported):** `PALunaManager` audio pause via `AudioListener.volume`; `PAUIShine` 9-slice support and matching `PA_UI_Shine.shader` properties; `PADragScroll.ScrollTo(float, bool)` and `IsMoving`; `PA01_UIGlowOutline` as `Shaders/PA_UI_GlowOutline`.
- **Rejected:** `PAShoreFieldGenerator` (project-specific SDF baker); `PA01_UIHoleLine`, `PA01_UIHoleShadow`, `StickerKit` shaders (game-specific); `PA01_UIShineWave` (overlaps `PA_UI_Shine`).

### VideoPlayer
- **Found:** `CodexLunaStage4AutoPatch` (VideoPlayer loop patch), `CodexLunaTools`, `CodexEditorCommandRunner`, `CodexVideoLoopScenePlayModeTester`, video loop sequence scripts.
- **Adopted:** the loop patches into `PALunaBuildPatcher`. Video scripts were already in the kit.
- **Rejected:** the other Codex tools (agent tooling for one project).

### Playable-Ads-Fake-Video
- **Found:** older renamed copies of the Video and Tween scripts, `ResponsiveVideoBackdropBlur.shader`, orientation / converter / backdrop editor windows.
- **Adopted:** blur shader as `Shaders/PA_UI_BackdropBlur` (9-tap, target 2.0).
- **Rejected:** duplicate chroma-key, unlit and sprite-outline shaders (already `PA_` shaders).
- **Not evaluated:** `PlayableOrientationSetupWindow`, `PlayableVideoConverterWindow`, `VideoBackdropCaptureWindow`.

### Package/ (loose files)
- **Found:** `PlayableAdsCheckerWindow`, `ScratchSpriteRenderer`, `PixelRailStyleGenerator`, `Shader3d`, green-screen video shader folder, FakeSkyBox, and 11 `.unitypackage` files.
- **Adopted:** nothing new (checker already adopted).
- **Rejected:** `ScratchSpriteRenderer` (DOTween); `PixelRailStyleGenerator` (art-specific); every `.unitypackage` (DOTween Pro, Odin, Epic Toon FX, Loading Screen Studio, UIParticleSystem and others are third-party).

## Shaders

| Shader | Source | Decision |
|---|---|---|
| `PA_UI_CircleReveal` | CraftPlayable `Craft01CircleReveal` | Adopted (renamed) |
| `PA_UI_BackdropBlur` | Fake-Video `ResponsiveVideoBackdropBlur` | Adopted (renamed, 9-tap, target 2.0) |
| `PA_UI_GlowOutline` | StickiiPA `PA01_UIGlowOutline` | Adopted (renamed, UnityCG + UnityUI only) |
| `PA_FakeShadow` | GangterCity `LunaFakeShadow` | Adopted (renamed, blob shadow, built-in) |
| `PA_UI_Shine` | Several projects | Already in kit; StickiiPA 9-slice properties back-ported |
| ChromaKey, PlayworksUnlit, SpriteOutline copies | Fake-Video, SatisFix, Package | Rejected: kit already has the `PA_` versions |
| `PA01_UIShineWave` | StickiiPA | Rejected: overlaps `PA_UI_Shine` |
| `PA01_UIHoleLine`, `PA01_UIHoleShadow`, StickerKit, Craft01 Block / Voxel / VoxelGlass | StickiiPA, CraftPlayable | Rejected: game-specific |
| `LunaMapStandard` Fade / Opaque / Transparent | Obby, GangterCity, Package | Rejected: heavy lit clones, not verified |
| `LunaStudWall` | Obby | Rejected: URP only |
| `ThankyouCardBackgroundBlur` | scoop, squishy | Rejected: overlaps `PA_UI_BackdropBlur` |
| `UI_WindEffect`, `FireworkBurn`, `UIPhaseTransitionWipe` | BikePlayable, squishy | Rejected: styled for their projects |
| ScratchCard family, AllIn1 | SatisFix, scoop, squishy, mukbang | Rejected: third-party |
| Shader Graph assets | SatisFix, GangterCity | Rejected: Luna risk |

No `geometry`, `hull`, `domain` or `target` pragmas were found in the checked custom shaders; the pragma audit was a sample, not exhaustive.

## Sound manager comparison

| Source | Model | Pooling | Mute | Luna notes | Decision |
|---|---|---|---|---|---|
| `PAAudioManagerBase` (kit) | Loop crossfade, `PlayTap`, one-shots | No | Yes, synced to `PALunaManager`, no PlayerPrefs | Designed for Luna | Kept as base |
| BikePlayable `AudioManagerBase` | Same design | No | PlayerPrefs | try/catch | Rejected |
| Obby `AudioManager` | String-id SFX entries, min interval, pitch, start offset | Fixed array | Yes | Written without lambdas | Adopted: data model |
| Craft `Craft01AudioManager` | BGM + 3 channel SFX | Round-robin | No | Clean | Adopted: channel choice |
| GangterCity PlayableSpider `SoundManager` | Gameplay cues | Transient queue | No | Clean | Pooling idea only |
| GangterCity CT Game `SoundManager` | Enum-keyed cues | No | No | Small | Rejected |
| SatisFix / scoop / squishy / mukbang `SoundManager` | Static flags, Hashtable, `Pooling.Instance` | Yes (class not found) | Static flags | try blocks | Rejected |
| scoop / squishy `SoundConfig` | One method per cue | n/a | n/a | Project-specific | Rejected |
| `SoundEnifi` (Craft, Obby) | `Play` / `Stop` wrapper | No | No | Trivial | Rejected |

Result: `PASoundConfig` (ScriptableObject) + `PASoundManager` on top of `PAAudioManagerBase`.

## Redundancy decisions inside the kit

| Item | Decision |
|---|---|
| `PABaseUIController` vs `PAUIControllerBase` | One base. `PAUIControllerBase` became `PAGameUIController : PABaseUIController` in Flow. |
| `IOrientationSource`, `PAGameEventOrientationSource` | Removed. The base controller polls `Orientation.IsLandscape`. |
| `IStoreGateway` | Removed. Protected `GoToStore()`; `PALunaStoreGateway` stays as a plain component (it is a Button `OnClick` target in scenes). |
| `ISteerInput` | Removed (no implementers). `IMoveInput` kept; `PAJoystick` implements it. |
| `Tweens/` vs `Tweening/` | Merged into `Tweening/Components`. |
| `PALayout` vs `PAOrientationLayout` | Both kept; different authoring models. |
| `PADependencyScannerPanel` vs `PlayableAdsCheckerWindow` | Duplicate; the kit version stays. |
| `Assets/Reference/` | Deleted; superseded by `PASoundManager` and the Luna Config sample. |

## Migration tables

### 1.x kit to 2.0.0

| Old | New |
|---|---|
| `Assets/PlayableKit/**` | `Packages/com.kat.playablekit/**` (GUIDs unchanged) |
| `PlayableKit.UI.PAUIControllerBase` | `PlayableKit.Flow.PAGameUIController` (derives from `PABaseUIController`: override `OnControllerAwake`, `OnControllerUpdate`, `OnOrientationChanged`) |
| `PlayableKit.UI.PACtaClickForwarder` | `PlayableKit.Flow.PACtaClickForwarder` |
| `PlayableKit.PAGameManagerBase` | `PlayableKit.Flow.PAGameManagerBase` |
| `PlayableKit.PAAudioManagerBase` | `PlayableKit.Audio.PAAudioManagerBase` |
| `PACameraShake`, `PAFreezeSpineRigAfterWarmup`, all `PAUI*` effects, `PAFallWindFx`, `PAFlashTrailRendererDriver`, `PAScratchSpriteRenderer`, `IWindFxReceiver` | `PlayableKit.Effects.*` |
| `PlayableKit.UI.PAUITapHand`, `PABillboardToCamera` | `PlayableKit.Effects.*` |
| `PADoFadeAToB`, `PADoMoveAToB`, `PADoScaleAToB`, `PALunaTweenAnimation`, `PATweenButton`, `PATweenRotateButton`, `PAUiSlide`, `PAUiPageSlide` | `PlayableKit.Tweening.*` |
| `PlayableKit.UI.PAHoldButton`, `PADragScroll`, `PAUiDragRotate`, `IDragRotateTarget`, `IMoveInput` | `PlayableKit.Controls.*` |
| `PlayableKit.LunaAnalyticsSink` | `PlayableKit.Analytics.LunaAnalyticsSink` |
| `IOrientationSource`, `PAGameEventOrientationSource` | Removed. Override `PABaseUIController.OnOrientationChanged`. |
| `IStoreGateway`, `PABaseUIController.Store` | Removed. Use protected `GoToStore()` or `PALunaStoreGateway`. |
| `ISteerInput` | Removed |
| Menus `Tools/PA`, `Tools/Sp Unity`, `Tools/MyLunaHelper` | `Tools/PlayableKit/...` |
| `Assets/Reference/*` | Removed. Use `PASoundManager` and `Samples~/LunaConfig`. |

### Other projects' names to the kit

| Old name | Kit equivalent |
|---|---|
| `LunaManager` | `PALunaManager` |
| `ScreenOrientationChecker` | `PAScreenOrientationChecker` |
| `UiManager` | `PACtaOverlay` |
| `SoundEnifi` / `SoundManager` | `PASoundManager` |
| `LunaStoreGateway` | `PALunaStoreGateway` |
| `GameEventOrientationSource` | Removed |
| `BaseUIController` | `PABaseUIController` |
| `UIShine` | `PAUIShine` |
| `DoScaleAToB` / `DoMoveAToB` / `DoFadeAToB` | `PADo*AToB` |
| `TweenButton` / `TweenRotateButton` | `PATweenButton` / `PATweenRotateButton` |
| `KeepRatio` | `PAKeepRatio` |
| `AutoPlayVideoOnEnable` | `PAAutoPlayVideoOnEnable` |
| `VideoLoopSequenceController` | `PAVideoLoopSequenceController` |
| `PlayworksVideoBootstrap` | `PAVideoBootstrap` |
| `PlayableVideoStepController` | `PAVideoStepController` |
| `VideoPlayOrientationSizer` | `PAVideoOrientationSizer` |
| `PlayableSpiderLunaPhysicsPatch` / `CodexLunaStage4AutoPatch` | `PALunaBuildPatcher` |
| `PlayableAdsCheckerWindow` | `PADependencyScannerPanel` |
| `SimpleJoystick` | `PAJoystick` |
| `CoinMoveFX` | `PAUICoinFly` |

## Known gaps

- The mukbang and Obby script lists are partial.
- The `Pooling` class used by the scoop / squishy / SatisFix / mukbang `SoundManager` was not located.
- The origin of the Craft and Gangster confetti prefabs is unverified; `PA_ConfettiBurst` was built independently.
- It is unverified whether Luna honours Unity's WebGL audio import overrides; if Luna re-encodes audio, `PAAudioImportOptimizer` helps only through mono and sample rate.
- Duplicate detection was by file name; contents were diffed only for the items stated above.
- "PlayableKit in project X" is unconfirmed where searches timed out (SatisFix, squishy, VideoPlayer, Fake-Video).
- Scoop's Unity version conflicts between `ProjectVersion.txt` (2022.3.62f2) and `AGENTS.md` (6000.2.7f2).
- Particle counts, texture sizes and a full shader-pragma audit were not measured.
- Not evaluated: GangterCity `Editor/LunaTools`, Fake-Video editor windows, SatisFix `BaoPlayable_FindRefsInSceneWindow`, Obby `SoundManagerRP`.
