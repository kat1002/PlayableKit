# Setup

How to start a new playable with PlayableKit. Requirements are in the [README](../README.md). Luna 6.4 / Playworks 7.2 is needed for a real ad build; the kit compiles without it.

## 1. Start from the sample

1. Package Manager > PlayableKit > Samples > **Template Scene** > Import.
2. Open the imported `PATemplateScene`. It contains `PALunaRoot`, a `PAGameManagerBase` and a `PAAudioManagerBase`.
3. Optional: import the **Luna Config** sample, copy `luna.json` to the project root and fill `projectId`, `applicationID`, `environmentID` and `creativeName`.

To start from an empty scene instead, drop `Prefabs/PALunaRoot.prefab` in and add the components in the table below.

## 2. Scene objects

Create each manager on its own **empty GameObject** with the one component listed.

| Object | Component | Notes |
|---|---|---|
| `PALunaRoot` (prefab) | `PALunaManager` + CTA UI | Required, one per scene. Owns the Luna lifecycle, store opening and mute sync. |
| `GameManager` | `PAGameManagerBase` (or your subclass) | Session state machine. Assign **Ui**. |
| `UI` (on the Canvas) | `PAGameUIController` (or your subclass) | Intro, progress bar and end card. Fill `Portrait Core` / `Landscape Core`. |
| `AudioManager` | `PASoundManager` | Assign a `PASoundConfig`. Or `PAAudioManagerBase` for loop crossfade only. |

Wiring:

| Component | Field | Notes |
|---|---|---|
| `PAGameManagerBase` | Ui | The `PAGameUIController` object |
| | Camera Shake | Optional (`PACameraShake`, in Effects) |
| `PAGameUIController` | Portrait Core / Landscape Core | root, hand, hint text, start object, progress fill, end card, intro pulses per orientation |
| | Loading Screen | Optional `PALoadingScreenController` |
| `PALunaManager` | Safety Timeout | Hard cap before a forced end card |

## 3. Subclass the flow

```csharp
using PlayableKit;
using PlayableKit.Flow;

public sealed class MyGame : PAGameManagerBase
{
    protected override void OnBeginPlay() { /* first tap: start spawners */ }
    protected override void OnPlayingTick() { /* every frame while playing */ }
    protected override void OnEndGameBegin(EndReason reason) { /* stop loops */ }
}
```

| `PAGameManagerBase` hook | Fires |
|---|---|
| `OnAwake()` | End of `Awake()`; wire your scene references |
| `OnBeginPlay()` | First tap |
| `OnPlayingTick()` | Every frame while playing |
| `OnMidpointReached()` | Once, when `PlayElapsed` crosses `sessionDuration` |
| `OnEndGameBegin(reason)` | Immediately on any end |
| `OnBeforeCrashSequence(reason)` | Once, for non-timeout ends, before the crash slow-mo |
| `OnEndGameAfter(reason)` | Once the end card is showing |

`PAGameUIController` derives from `PABaseUIController`, so override the `OnController*` hooks, never `Awake` / `Update`:

| `PAGameUIController` hook | Fires |
|---|---|
| `BuildCoreRefs()` | Awake; copy a richer ref class into `portraitCore` / `landscapeCore` |
| `OnHudTick()` | Every `hudTickInterval` seconds |
| `OnOrientationChanged(UIOrientation)` | Landscape / portrait flip (already re-applies the layout and the intro) |

`Cta()` plays the tap sound and opens the store; wire it (or `PACtaClickForwarder`) to your CTA button. Details in [UIKit.md](UIKit.md).

## 4. Sound

1. Create a config: Project > Create > **PlayableKit > Sound Config**.
2. Set the BGM clip and volume, the number of SFX channels, and add SFX entries (id, clip, volume, min interval, pitch jitter, start offset).
3. Put `PASoundManager` on the AudioManager object and assign the config.
4. Play from code with `PASoundManager.Play("id")`, or from a Button `OnClick` with `PlaySfx(string)`.

Mute is shared with `PALunaManager`; add `PAMuteToggleButton` to a button to toggle it.

## 5. Tunables

`sessionDuration`, `introInactivityStoreDelay`, `endCardDelay`, `crashShakeAmplitude`, `crashShakeDuration` (`PAGameManagerBase`); `safetyTimeout` (`PALunaManager`); `hudTickInterval` (`PAGameUIController`); `loopVolume`, `loopPitchRange`, `sfxVolume` (`PAAudioManagerBase`).

## 6. Before you build

1. **Tools > PlayableKit > Sanitize UI For Luna** on the scene.
2. **Tools > PlayableKit > Luna Compliance**. Save the scene and add it to Build Settings first, or it reports an error / warning.
3. **Tools > PlayableKit > Playable Ads Checker** for missing scripts and heavy assets.
4. Optional: **Tools > PlayableKit > Audio Import Optimizer** to shrink clips for WebGL.
5. Window > Unity Playworks: add the scene and export. After the build, `PALunaBuildPatcher` patches the generated `LunaTemp` JavaScript on its own (see the [README](../README.md#palunabuildpatcher)).
6. In the Luna preview check: text renders, tap zones cover the full screen in both orientations, the CTA opens the store, mute silences audio.

## 7. Notes

- `com.unity.playworks.upp` in `Packages/manifest.json` is often a machine-local `file:` path. Re-point it per machine.
- Particles (e.g. `PA_ConfettiBurst`): use world space or a Screen Space - Camera canvas. Trigger with `ParticleSystem.Play()`.
- Keep runtime code simple: no LINQ, no generic `[Serializable]` types, no `PlayerPrefs`. See *Luna gotchas* in the README.
