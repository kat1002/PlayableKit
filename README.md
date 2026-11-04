# PlayableKit (Unified Starter & Effects Kit)

Consolidated reusable toolkit for Unity Playworks / Luna playable ads.
Combines the best versions of `PlayableKit`, `PlayableUIKit`, `Core`, `Reference`, and `TweenLite` (`Playable.Tweening`) from across all playable projects.

Copy the folders you want into a new project's `Assets/`. All `.meta` files are included so script and asset GUIDs are preserved across projects.

## Requirements in the Target Project
- **UGUI** (`UnityEngine.UI`) — built-in.
- **No external tween library needed** — `PlayableKit/Runtime/Tweening/TweenLite.cs` (`Playable.Tweening`) is bundled and Luna-safe out of the box.
- **Luna / Playworks package** (`com.unity.playworks.upp`) — only needed if copying `Core/Luna/LunaManager.cs` and `Core/Adapters/LunaAnalyticsSink.cs`.
- **TextMeshPro** — only needed if copying `PlayableUIKit/`. After installing, run `Window > TextMeshPro > Import TMP Essential Resources`.

## Contents

| Folder | What | Needs |
|--------|------|-------|
| `PlayableKit/` | Standalone UI effects (`UIShine`, `UIStarTwinkle`, `UIPathTwinkle`, `UIIdleMotion`, `UIProgressFillFX`, `UIPraiseFly`, `UIStarBurst`, `UITapRing`, `UIPixelRingPop`, `UIPixelSmokeBurst`, `FallWindFx`, `FlashTrailRendererDriver`), tweens (`DoScaleAToB`, `DoMoveAToB`, `DoFadeAToB`, `UiSlide`, `UiPageSlide`, `ButtonRotate`), UI helpers (`UITapHand`, `HoldButton`, `UiDragRotate`, `BillboardToCamera`), `PlayableAnalytics`, `PlayableLog`, `TweenLite`, shaders, materials, and sprites. | UGUI |
| `PlayableUIKit/` | `BaseUIController`, `Oriented<T>` + `OrientedFields`, `CtaPresenter`, `CountdownTimer`, `IdleTimer`, input/store/orientation contracts. | UGUI, TMPro |
| `Core/Luna/LunaManager.cs` | Single lifecycle owner: `StartGame()`, `SetEndTime(float)`, `GoToStore()` (`GameEnded` once + `InstallFullGame`), registers `LunaAnalyticsSink`. | Luna, `PlayableKit` |
| `Core/Adapters/` | `LunaStoreGateway` (`IStoreGateway` -> `LunaManager`), `GameEventOrientationSource` (`IOrientationSource` -> `GameEventManager`), `LunaAnalyticsSink` (`IAnalyticsSink` -> `Luna.Unity.Analytics`). | `PlayableUIKit`, `PlayableKit`, `LunaManager`, `GameEventManager` |
| `Core/Events/GameEventManager.cs` | Static event bus (`EventId`: GameWin/Lose, Landscape/VerticalScreen, OnActive/DeactiveUI, OnActive/DeactiveCTA). | — |
| `Core/UI/` | `ScreenOrientationChecker.cs`, `UiManager.cs`, `SoundEnifi.cs`. | `GameEventManager` |
| `Core/Editor/PlayableSpiderLunaPhysicsPatch.cs` | Auto-patches `LunaTemp/**/physics3d-0.js` so MeshColliders don't crash in 3D physics builds. Editor-only. | — |
| `Reference/luna.json` | Working Luna build config template. Copy to project root and update `projectId`/`applicationID`/`creativeName`. | — |
| `Reference/SoundManager.cs.txt` | Reference sound manager template. Copy and rename sound slots per game. | `SoundEnifi` |

## Setup in a New Scene
1. Copy `PlayableKit/`, `PlayableUIKit/`, and `Core/` into `Assets/`.
2. Put `LunaManager` on an empty GameObject; set `currentEndTime`; call `LunaManager.instance.StartGame()` on first input/intro.
3. Add `ScreenOrientationChecker` (+ fill `obj_Hori` / `obj_Verti`) and `UiManager` on the canvas (`ctaButton`).
4. Subclass `BaseUIController` for the scene's UI; add `LunaStoreGateway` + `GameEventOrientationSource` and assign them.
5. Raise CTA with `GameEventManager.RaisedEvent(EventId.OnActiveCTA)`.

## Gotchas (Luna 7.2)
- No generic `[Serializable]` types in shipped code — fields silently null in the bundle. `OrientedFields.cs` holds the non-generic wrappers; use those for serialized fields, not `Oriented<T>` directly.
- A Luna "Recompile" doesn't re-export scene data; after renaming/retyping serialized fields run a full build.
- Keep textures at 512x512 or smaller.
