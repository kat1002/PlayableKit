# PlayableKit

Luna / Playworks-safe toolkit for Unity playable ads, shipped as the UPM package **`com.kat.playablekit`** (v2.0.1).

> [!IMPORTANT]
> **Luna only compiles `Assets/`. The kit is a package, so Luna builds it only when `luna.json` lists its folder.**
>
> If it isn't listed, the Luna build still succeeds, but every `PA*` component is silently dropped: tweens don't play, buttons do nothing, and nothing shows in the Console.
>
> The kit lists itself. On every Editor load (so after each install and update) it writes its current folder into `luna.json` > `unity.scripts.externalSources`, for example `.\Library\PackageCache\com.kat.playablekit@<hash>`. The hash changes with every update, so **don't type that path by hand**. What you do:
> 1. Keep `luna.json` in the project root and **commit it** after the Console shows `[PlayableKit] luna.json externalSources now lists the kit ...`.
> 2. Before each build, run **Tools > PlayableKit > Luna Compliance**. It reports an error when the entry is missing or stale.
> 3. To fix it right away: **Tools > PlayableKit > Luna > Sync luna.json External Sources**.

It gives every playable the same base:
- Luna lifecycle, store / CTA handling and analytics.
- A built-in tween engine (`TweenLite`, no DOTween needed).
- A sound manager with a ScriptableObject config.
- Orientation-aware UI, effects, shaders, video steps and input controls.
- `PALog` logging, preconfigured through `PAConfig`.
- Editor tools that check a project against Luna's rules, and automatic Luna build patches.

All types use the `PA` prefix (`PALunaManager`, `PASoundManager`, `PALog`, `PAConfig`, ...).

---

## Contents

1. [Requirements](#requirements)
2. [Install](#install)
3. [Set up a scene](#set-up-a-scene)
4. [Configure: PAConfig and PALog](#configure-paconfig-and-palog)
5. [Sound](#sound)
6. [Build with Luna](#build-with-luna)
7. [Migrate an existing project](#migrate-an-existing-project)
8. [Update the package](#update-the-package)
9. [Troubleshooting](#troubleshooting)
10. [Docs](#docs)
11. [Develop this repo](#develop-this-repo)
12. [Contact](#contact)

---

## Requirements

| What | Notes |
|---|---|
| Unity 2021.3 or later | Compile-tested in empty Unity 2022.3 and Unity 6 projects. |
| Luna 6.4 or Playworks 7.2 (optional) | Needed only for a real ad build. Without it, the kit compiles: lifecycle and analytics calls do nothing, and `InstallFullGame` only logs. |
| TextMeshPro | Installed automatically as a dependency. Run **Window > TextMeshPro > Import TMP Essential Resources** once if the project has no TMP font yet. |

uGUI and the audio, particle system and video modules are also pulled in automatically.

## Install

Pick one.

**A. Git URL (recommended).** Window > Package Manager > **+** > *Add package from git URL*:

```
https://github.com/kat1002/PlayableKit.git?path=Packages/com.kat.playablekit
```

To pin a release, add `#v2.0.0` to the end of the URL.

**B. manifest.json.** Add this to `Packages/manifest.json` > `dependencies`:

```json
"com.kat.playablekit": "https://github.com/kat1002/PlayableKit.git?path=Packages/com.kat.playablekit#v2.0.0"
```

**C. .unitypackage (offline).** Download `PlayableKit.unitypackage` from this repo and use *Assets > Import Package > Custom Package*. The files land in `Packages/com.kat.playablekit`.

**D. Local clone (for working on the kit).** Window > Package Manager > **+** > *Add package from disk*, and pick `Packages/com.kat.playablekit/package.json` in your clone.

When it's done, the Console should show no errors, and the **Tools > PlayableKit** menu should appear.

> If the project already has an old copy of the kit, for example `Assets/PlayableKit`, read [Migrate an existing project](#migrate-an-existing-project) **before** installing.

## Set up a scene

### Fast way: the sample

1. Package Manager > PlayableKit > **Samples** > *Template Scene* > **Import**.
2. Open `Assets/Samples/PlayableKit/<version>/Template Scene/PATemplateScene.unity`.
3. Optional: import the **Luna Config** sample. Copy its `luna.json` to the project root and fill in `projectId`, `applicationID`, `environmentID` and `creativeName`.
4. Add the scene to **File > Build Settings**.

### From an empty scene

Give each manager its own empty GameObject:

| GameObject | Component | Required | Notes |
|---|---|---|---|
| `PALunaRoot` | Drop in `Prefabs/PALunaRoot.prefab` | Yes, one per scene | `PALunaManager`: Luna lifecycle, store opening, mute, safety timeout. The prefab also holds the CTA UI. |
| `PAConfig` | `PAConfig` | Recommended | Log settings. See below. |
| `GameManager` | Your subclass of `PAGameManagerBase` | Yes | Session flow: first tap, play, end card. Assign **Ui**. |
| `UI` (on the Canvas) | Your subclass of `PAGameUIController` | Yes | Intro, hand hint, progress, end card for portrait and landscape. |
| `AudioManager` | `PASoundManager` | If there is sound | Assign a `PASoundConfig`. |
| `EventSystem` | `EventSystem` + `StandaloneInputModule` | Yes | Needed for any UI input. |

Canvas: Screen Space - Overlay is fine for UI. Effects that use **ParticleSystem** need *Screen Space - Camera* or world space. Luna does not draw particles on Overlay canvases.

### Write your game

```csharp
using PlayableKit;
using PlayableKit.Flow;
using PlayableKit.UI;

public sealed class MyGame : PAGameManagerBase
{
    protected override void OnBeginPlay()               { PALog.Log("MyGame", "first tap"); }
    protected override void OnPlayingTick()             { /* every frame while playing */ }
    protected override void OnEndGameBegin(EndReason r) { PALog.Log("MyGame", "end", r.ToString()); }
}

public sealed class MyUI : PAGameUIController
{
    protected override void OnOrientationChanged(UIOrientation o) { /* move things for portrait / landscape */ }
}
```

- Wire your CTA button's `OnClick` to `PAGameUIController.Cta()`, or add `PACtaClickForwarder` to it. Either one plays the tap sound and opens the store.
- Do not override `Awake` or `Update` in UI controllers. Use the `OnController*` hooks.
- All hooks and fields are listed in [Setup.md](Packages/com.kat.playablekit/Documentation~/Setup.md) and [UIKit.md](Packages/com.kat.playablekit/Documentation~/UIKit.md).

### Check before you build

Run these from **Tools > PlayableKit**:
1. **Sanitize UI For Luna**: replaces ScrollRect, LayoutGroup, Mask and the other UI Luna cannot run.
2. **Luna Compliance**: checks the Luna rules, and that the scene is saved and in Build Settings.
3. **Playable Ads Checker**: finds missing scripts and asset or code issues.
4. **Audio Import Optimizer**: applies WebGL presets to the selected clips.
5. **Video > Video Optimizer** (video playables): encodes each video under a size budget, e.g. ≤ 2.4 MB, as browser-safe H.264 with a separate MP3. **Video > Backdrop Capture** bakes a blurred still to show behind the video. Both need [FFmpeg](https://ffmpeg.org/download.html) (`winget install Gyan.FFmpeg` / `brew install ffmpeg`).

## Configure: PAConfig and PALog

`PALog` is the one logger for the kit and your game. Each line looks like this:

```
[PA][MyGame] 3.20s | first tap
[PA][MyGame] pre 0.42s | loading        <- before the first tap
[PA][MyGame] 12.04s | end | Timeout     <- with a state
```

The time is the Luna play time. The lines also show in the browser console of a Luna build.

```csharp
PALog.Log("MyGame", "picked item", "score=3");   // game-state line (same call as the old per-project PALog)
PALog.Info("MyGame", "level loaded");
PALog.Verbose("MyGame", "per-step detail");
PALog.Warn("MyGame", "no target", this);          // `this`: click the log to select the object
PALog.Error("MyGame", "missing ref", this);
PALog.Click("PlayButton");  PALog.Cta("EndCard");  PALog.Tap(Input.mousePosition);
```

**Preconfigured defaults.** These apply with no setup:

| Setting | Default |
|---|---|
| `PALog.Enabled` | `true` |
| `PALog.MinLevel` | `Verbose` in the Editor, `Info` in player and Luna builds |
| `PALog.ShowTime` | `true` |

**Per scene.** Add a `PAConfig` component and set *Enable Game Log*, *Min Log Level* and *Show Log Time*. It applies them on Awake, before your scripts run. In Play mode, inspector edits take effect right away.

**Compile-time off.** Add `PA_LOG_OFF` to *Player Settings > Scripting Define Symbols* to silence every log.

## Sound

1. *Create > PlayableKit > Sound Config*.
2. Set the BGM and its volume, the number of SFX channels, and the SFX entries. Each entry has an id, clip, volume, min interval, pitch jitter and start offset.
3. Add `PASoundManager` to `AudioManager` and assign the config.
4. Play a sound with `PASoundManager.Play("coin")`, or from a Button with `PlaySfx(string)`.
5. `PAMuteToggleButton` on a button toggles mute. Mute is shared with `PALunaManager`, so the ad's own mute works too.

## Build with Luna

1. Install Luna 6.4 or Playworks 7.2 into the project. The kit detects it and switches on the real Luna calls (define `PLAYABLEKIT_LUNA`).
2. Put `luna.json` in the project root (from the *Luna Config* sample). The kit adds itself to `unity.scripts.externalSources` on the next Editor load; see the note at the top of this README.
3. Run the checks from [Check before you build](#check-before-you-build). Luna Compliance fails if `luna.json` does not list the kit's current folder.
4. Build from the Luna window as usual.

`PALunaBuildPatcher` runs on its own and fixes two bugs in Luna's generated JavaScript:
- a mesh-collider inertia crash in `physics3d-0.js`;
- `VideoPlayer.isLooping` being ignored in `UnityEngine.js`.

It is safe to run repeatedly, and it skips files that don't match. You can also force it from **Tools > PlayableKit > Luna > Apply Build Patches Now**.

Luna rules to keep in mind:
- no `PlayerPrefs`;
- no generic `[Serializable]` fields;
- no lambdas or LINQ in per-frame code;
- check Unity objects with `== null`, never `?.`;
- built-in render pipeline shaders only;
- open the store directly inside the click handler.

The full list is in the [package README](Packages/com.kat.playablekit/README.md#luna-gotchas).

## Migrate an existing project

The package uses the same namespaces, class names and (for 1.x copies) script GUIDs as the old copies kept inside projects. **The two cannot live side by side**: you would get duplicate types, CS0433 "ambiguous reference" errors and GUID clashes.

1. **Commit or back up first.**
2. **Delete the old copy.** Known locations.
3. **Install the package** ([Install](#install)). A 1.x copy that lived in `Assets/PlayableKit` keeps its scene and prefab references, because the GUIDs are unchanged.
4. **Fix the code:**
   - Add the `using PlayableKit.<Module>;` lines (`Flow`, `UI`, `Audio`, `Effects`, `Tweening`, `Controls`, `Video`, `Analytics`).
   - Rename old names to the `PA*` types:

     | Old | New |
     |---|---|
     | `PAUIControllerBase` | `PAGameUIController` |
     | `PlayableLog` | `PALog` |
     | `LunaManager` | `PALunaManager` |
     | `UIShine` | `PAUIShine` |
     | `DoScaleAToB` | `PADoScaleAToB` |

     The full table is in the [CHANGELOG](Packages/com.kat.playablekit/CHANGELOG.md#breaking-changes) and the [scan report](Docs/ProjectScanReport.md).
   - Per-project `PALog.Log(cls, msg[, state])` calls work unchanged with `PlayableKit.PALog`. Add `using PlayableKit;`, then delete the old `PALog.cs`.
   - A per-project `PAConfig` with `m_EnableGameLog` becomes the kit's `PAConfig` component, which has the same field plus a log level and a time toggle. Re-tick the setting on the scene object after swapping.
   - UI controllers: move `Awake`/`Update` code into `OnControllerAwake`/`OnControllerUpdate`, and the old `Cta` property is now `CtaPanel`.
5. **Check:** no Console errors, and *Tools > PlayableKit > Playable Ads Checker* reports no missing scripts. Then play the scene once.

## Update the package

- **Git URL:** change the `#vX.Y.Z` tag in `manifest.json`. If you used no tag, use Package Manager > PlayableKit > **Update** (or delete the `com.kat.playablekit` entry from `packages-lock.json`).
- After any update, Unity reinstalls the kit in a new `Library/PackageCache/com.kat.playablekit@<hash>` folder. The kit rewrites its `luna.json` entry on the next Editor load. Commit the changed `luna.json`.
- **.unitypackage:** delete `Packages/com.kat.playablekit`, then import the new `.unitypackage`.
- Read the [CHANGELOG](Packages/com.kat.playablekit/CHANGELOG.md) for breaking changes.

## Troubleshooting

| Problem | Fix |
|---|---|
| `CS0433` / "type exists in both" | An old copy of the kit is still in `Assets/`. See [Migrate](#migrate-an-existing-project). |
| TMP text is pink or invisible | *Window > TextMeshPro > Import TMP Essential Resources*. |
| CTA does nothing in the Editor | Expected without Luna. It logs `InstallFullGame (Luna not installed)`. |
| In the Luna build, `PA*` components do nothing (no tween, no click) and no error shows | Luna built without the kit: `luna.json` > `unity.scripts.externalSources` is missing the kit or points at an old `@hash` folder. Run **Tools > PlayableKit > Luna > Sync luna.json External Sources**, then rebuild. |
| No logs | Check `PAConfig` (*Enable Game Log*, *Min Log Level*) and the `PA_LOG_OFF` define. |
| Particles invisible in the Luna build | Use a Screen Space - Camera canvas or world space, not Overlay. |
| Missing scripts after migrating | The old copy came from a different layout (not 1.x `Assets/PlayableKit`), so re-add the components by hand. |

## Docs

| Doc | What |
|---|---|
| [Package README](Packages/com.kat.playablekit/README.md) | Modules and assemblies, editor tools, assets, Luna gotchas |
| [Setup](Packages/com.kat.playablekit/Documentation~/Setup.md) | Scene objects, hooks and tunables in detail |
| [UI Kit](Packages/com.kat.playablekit/Documentation~/UIKit.md) | Controllers, orientation, CTA |
| [CHANGELOG](Packages/com.kat.playablekit/CHANGELOG.md) | Release notes and the 1.x to 2.0 rename table |
| [Project scan report](Docs/ProjectScanReport.md) | What was found in other playable projects, and what was adopted |

## Develop this repo

1. Open this folder in Unity 2022.3. Luna/Playworks is referenced from `Packages/manifest.json`, so re-point its `file:` path on your machine.
2. The package is embedded in `Packages/com.kat.playablekit`; edit it there.
3. Samples import into the git-ignored `Assets/Samples`.
4. To release:
   - bump `version` in `package.json` and add a CHANGELOG entry;
   - run **Tools > PlayableKit > Export PlayableKit.unitypackage**;
   - commit, then tag `vX.Y.Z`.

## Contact

Made by **kat1002**.

- GitHub: [github.com/kat1002](https://github.com/kat1002)
- Email: [nguyenphuc10022004@gmail.com](mailto:nguyenphuc10022004@gmail.com)
- Issues and feature requests: [github.com/kat1002/PlayableKit/issues](https://github.com/kat1002/PlayableKit/issues)
