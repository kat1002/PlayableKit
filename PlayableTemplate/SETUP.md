# Playable Ad Template — setup

A copy-paste starting point for a new Luna Playworks playable, extracted from BikePlayable.
Everything here is genre-agnostic: a state-machine `GameManagerBase`, an overlay
`UIControllerBase` (intro / progress / end card / CTA), an `AudioManagerBase` (loop crossfade +
one-shots + mute), `CameraShake`, `LunaManager`, and a mute button / loading screen. No external
art assets are required beyond DOTween (for `DoScaleAToB`, used by intro tap-hint pulses).

## Getting started

1. Copy `Assets/_Template/` into your new project (or, in this project, just keep using it).
2. In the Unity Editor, run **Tools > Playable > Create Template Base Scene**. This generates
   `Assets/_Template/Scenes/PlayableTemplateBase.unity` via code (not a hand-edited file), so it's
   always correctly wired against the current base classes. Duplicate that scene as your new
   playable's scene and rename it.
3. Build out your game-specific gameplay under the empty `World` / `Player` GameObjects.

## Hierarchy (what the scene builder creates)

```
Main Camera
Directional Light
___________
  LunaManager   → LunaManager
  GameManager   → GameManagerBase           (usable as-is; subclass when you need hooks)
  AudioManager  → AudioManagerBase
  UI            → UIControllerBase
  World         → (empty — attach your world/level driver script here)
  Player        → (empty — attach your player-control script here)
  EventSystem
Canvas
  PortraitUI / LandscapeUI (mirrors)
    Header → MuteButton (+ room for game-specific HUD)
    Start → TapLeft, TapRight, Start → TutorialHand, Hint
    EndCard → Dim, CTA (CtaButton prefab instance), Title
  LoadingScreen   (topmost Canvas sibling, active by default)
  FullscreenCTA
```

Create each manager as an **empty GameObject** and add the one component listed, same convention
as BikePlayable's own SETUP.md.

## Wiring table (generic fields)

| Component | Field | Notes |
|---|---|---|
| GameManagerBase | Ui | drag the `UI` object |
| | Camera Shake | leave empty — auto-found |
| UIControllerBase | Portrait Core / Landscape Core | root/hand/hintText/startObj/progressFill/endCard per orientation — the scene builder wires these already |
| | Loading Screen | leave empty — auto-found |
| LunaManager | Safety Timeout | hard cap before a forced end card |

## Extension points for a new game

Subclass `GameManagerBase`, `UIControllerBase`, `AudioManagerBase` and override:

| Hook | Fires |
|---|---|
| `GameManagerBase.OnAwake()` | end of `Awake()` — wire your own scene refs |
| `OnBeginPlay()` | first tap — start your spawners/loops |
| `OnPlayingTick()` | every frame while playing, after session tick + progress |
| `OnMidpointReached()` | once, when `PlayElapsed` crosses `sessionDuration` |
| `OnEndGameBegin(reason)` | immediately on any end (stop loops/effects) |
| `OnBeforeCrashSequence(reason)` | once, for non-timeout ends, before the crash slow-mo |
| `OnEndGameAfter(reason)` | once the end card is showing |
| `UIControllerBase.BuildCoreRefs()` | if you need extra HUD fields beyond the generic set, keep your own richer ref class (see BikePlayable's `UIController.UIRefs`) and copy into `portraitCore`/`landscapeCore` here |
| `UIControllerBase.OnHudTick()` | throttled (`hudTickInterval`) HUD formatting |
| `AudioManagerBase.OnAwake()` | call `CreateLoopSources(clipA, clipB)` with your own clips |

See BikePlayable's `GameManager`/`UIController`/`AudioManager` in `Assets/Scripts/` for a full
worked example of this pattern.

## Tunables

`sessionDuration`, `introInactivityStoreDelay`, `endCardDelay`, `crashShakeAmplitude` /
`crashShakeDuration` (GameManagerBase); `safetyTimeout` (LunaManager); `hudTickInterval`
(UIControllerBase); `loopVolume` / `loopPitchRange`, `sfxVolume` (AudioManagerBase).

## Export checklist

1. `Tools > Playable > Prepare For Luna (shader + build settings)` — pins the configured
   always-included shader (default `Unlit/Color`) and registers the active/open scene in Build
   Settings. No scene path is hardcoded: it uses the scene you have open, or an override in the
   auto-created `PlayableExportSettings` asset.
2. `Window > Unity Playworks` → add your scene to the Playworks scene list → Export.
3. In the Luna preview: text renders (builtin font), tap zones cover full height in both
   orientations, CTA redirect fires, mute button silences audio.

## Known limitations / prerequisites

- `Packages/manifest.json`'s `com.unity.playworks.upp` entry is a machine-local relative path —
  it must be re-pointed per machine/project; this can't be fixed generically.
- DOTween must exist at `Assets/Plugins/Demigiant/DOTween/` for `DoScaleAToB` (used by the intro
  tap-hint pulses) to compile.
- No asmdef is used on purpose: this template is distributed by copying the folder, not by
  referencing a shared package, so folder discipline + review is enough to keep it decoupled from
  game-specific code. Revisit only if the template sees real multi-project reuse and either
  compile times or an accidental-coupling incident justify the added compilation boundaries.
- Keep runtime scripts under `Scripts/Core/` free of LINQ/heavy reflection/generics beyond
  List/Dictionary — Luna's C#→JS transpiler expects simple C#. Editor-only code (under
  `Scripts/Editor/`) has no such restriction (`luna.json` excludes `**/Editor/`).
