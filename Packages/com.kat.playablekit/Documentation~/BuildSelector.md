# Build Selector

Why `luna.json` must be prepared per creative and how the Build Selector does it. It is for projects that hold several playable creatives (or a full game plus creatives) under one `Assets/` folder. Setup basics are in [Setup.md](Setup.md); requirements are in the [README](../README.md).

## 1. Why it exists

Luna compiles every runtime script under `Assets/` unless `luna.json` `unity.scripts.excludes` lists it. A project with a full game and several creatives therefore fails at stage 3: full-game services, C# features Luna does not support and other creatives' scripts all get compiled. Each creative must compile only what its keep list names.

`luna.json` holds one creative at a time. These parts belong together and must change together:

- `unity.scenes` / `startupScene`
- `creativeName`
- the generated excludes block between `Assets/__generated_excludes_begin__/` and `Assets/__generated_excludes_end__/`
- `assets.rules.texture.overrides`

Changing only the scene leaves the previous creative's excludes in place. Two incidents seen in practice:

- A creative was built with another creative's excludes. Its controllers became empty script types in the browser, with no compile error.
- A creative was built with a sibling's excludes and failed at stage 3 with `CS0234: The type or namespace name 'Effects' does not exist in the namespace 'PlayableKit'`, in a script that belonged to the other creative.

The Playworks plugin caches `luna.json` in static fields until a domain reload, and writes the cache back on build and on UI changes. Hand edits and shell scripts that change the file while the Editor is open get overwritten. Prepare forces a domain reload so the plugin reads the new file.

Prepare validates everything first, writes once, verifies the result, and runs in C# inside Unity on Windows and macOS (no PowerShell or Python).

## 2. Quick start

1. Open the playable scene (an untitled scene must be saved first).
2. **Tools > PlayableKit > Luna > Build Selector** (also Control Center > Luna Compliance > **Open Build Selector**).
3. If the scene is not registered: choose **Start keep list from**, then **Register**.
4. Edit the keep list (**Open keep list**), so it names every script the creative needs.
5. **Prepare and Open Playworks**.
6. In Playworks: **Build Develop**.

## 3. Project files

| File | Purpose |
|---|---|
| `Tools/Playworks/PlayableProfiles.json` | Profile registry: one profile per creative |
| `Tools/Playworks/creatives/<Name>.keep.txt` | Keep list per creative |
| `luna.json` (project root) | Written by Prepare |

Registry and keep-list paths are fixed and relative to the project root (the folder that contains `Assets/`). Both are project data: commit them.

`luna.json` at the project root must already contain `unity.scripts.excludes` (string array), `assets.rules.texture.default` and `assets.rules.texture.overrides`. The package **Luna Config** sample has them.

## 4. Registry format

```json
{
  "profiles": [
    {
      "feature": "CreativeA",
      "displayName": "CreativeA",
      "scenePath": "Assets/Scenes/CreativeA.unity",
      "keepListPath": "Tools/Playworks/creatives/CreativeA.keep.txt",
      "creativeName": "CreativeA"
    },
    {
      "feature": "CreativeB",
      "displayName": "CreativeB",
      "scenePath": "Assets/Scenes/CreativeB.unity",
      "keepListPath": "Tools/Playworks/creatives/CreativeB.keep.txt",
      "creativeName": "CreativeB",
      "textureNoCompression": [
        "Assets/Art/Fonts/MainFont SDF.asset"
      ]
    }
  ]
}
```

| Field | Required | Meaning |
|---|---|---|
| `feature` | yes | Unique id, case-insensitive. The `-playableFeature` value. |
| `displayName` | no | Informational. Register sets it to the scene name; Prepare ignores it. |
| `scenePath` | yes | `Assets/...unity`. One buildable profile per scene, matched case-insensitively. |
| `keepListPath` | yes | Keep-list file, relative to the project root. |
| `creativeName` | yes | Written to `luna.json` `creativeName`. |
| `diagnosticsOnly` | no | Bool. Prepare writes scene, creative name and textures but leaves the excludes untouched. The window will not prepare such a profile; use batchmode. |
| `textureNoCompression` | no | Asset paths. Each gets an override cloned from `assets.rules.texture.default` with `compression: none`. Override entries without a name are dropped. |

## 5. Keep-list format

```text
# comments and blank lines are ignored
Assets/Scripts/Creative/
Assets/Plugins/DOTween/
Assets/Scripts/Shared/LunaManager.cs
```

- One entry per line. `#` comments and blank lines are ignored.
- `\` is normalized to `/`.
- A trailing `/` is a folder prefix: every script below it is kept. Otherwise the entry is one exact file.
- Matching is case-sensitive (ordinal). A listed file that does not exist is an error.

Scripts considered: every `*.cs` under `Assets/`, skipping folders named `Editor`, folders ending in `~`, and symlinks/junctions. Packages are not scanned; PlayableKit itself is compiled through `externalSources` (see the [README](../README.md)).

How excludes are generated:

- Each excluded script collapses to its shallowest folder below `Assets/` that holds no kept script; otherwise the file itself is listed.
- The list is sorted ordinal and written between the two markers.
- Entries in `luna.json` outside the markers are kept. Entries after the end marker move in front of the regenerated block (kept for output compatibility with the legacy Python script).

## 6. Window states

The window works on the active scene. It reloads on focus, scene open/switch/save and **Refresh**.

| State | Meaning | Action |
|---|---|---|
| Untitled scene | Scene has no file | Save the scene |
| Registry error | Registry is malformed or invalid | Fix `PlayableProfiles.json` |
| Not registered | No profile for this scene (also when there is no registry yet; Register creates it) | **Register** |
| Diagnostics-only | Profile has `diagnosticsOnly` | Use batchmode |
| Duplicate | More than one profile uses this scene | Remove the duplicate |
| Keep list missing | The profile's keep-list file does not exist | Create the file |
| Keep list names missing scripts | A listed file or folder does not exist | Fix the keep list |
| Not prepared | `luna.json` uses another scene or creative | **Prepare** |
| Stale | "Keep list or scripts changed since the last Prepare" | **Prepare** again |
| Prepared, up to date | `luna.json` matches the profile | Build |

The stale check compares the excludes `luna.json` has with the excludes the keep list and the current scripts produce, plus `unity.scenes`/`startupScene` and `creativeName`.

## 7. Register flow

**Register this scene** appends a profile for an unregistered scene:

- `feature`, `displayName` and `creativeName` are the scene file name.
- The keep list is `Tools/Playworks/creatives/<Scene>.keep.txt`.
- **Start keep list from** copies another profile's keep list and `textureNoCompression` under a header naming the scene and source, or writes an empty list with format comments.
- An existing keep-list file is reused, never overwritten.
- Register is refused when a profile already uses the scene or the name.
- The first Register creates the registry file.
- The profile is appended by text insertion, so the rest of the file stays unchanged.

Then edit the keep list and Prepare.

## 8. Prepare steps

1. Save prompt for unsaved scene changes (Cancel aborts).
2. Re-check the state, that the Editor is not compiling and not in Play Mode.
3. Close open Playworks windows.
4. Validate registry, scene, keep list and `luna.json` in memory.
5. Write `luna.json` once (4-space indent, LF, UTF-8 without BOM).
6. Re-read it and verify scene, `startupScene`, creative name and generated excludes.
7. `AssetDatabase.Refresh`, then a forced domain reload.
8. Reopen **Tools > Unity Playworks Plugin** when requested (**Prepare and Open Playworks**).

A failed Prepare leaves `luna.json` unchanged. Do not edit `luna.json` by hand or toggle scripts in the Luna UI for a registered profile; the keep list decides what compiles. Use the Luna UI only for assets and settings outside the generated block.

## 9. Batchmode

For CI or recovery. The project must not be open in another Editor. Do not pass `-quit`: the method exits with code 0 on success and 1 on failure, with the reason in the log.

Select the profile with `-playableFeature <Feature>` or `-playableScene Assets/...unity`. Diagnostics-only profiles are selected with `-playableFeature`.

Windows PowerShell:

```powershell
& "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -batchmode -nographics -projectPath . -executeMethod PlayableKit.Editor.PALunaProfileWriter.PrepareFromCommandLine -playableFeature <Feature> -logFile -
$LASTEXITCODE
```

macOS:

```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath . -executeMethod PlayableKit.Editor.PALunaProfileWriter.PrepareFromCommandLine -playableFeature <Feature> -logFile -
echo $?
```

## 10. Migrating from the PowerShell/Python scripts

For projects using `Select-PlayableProfile.ps1` and `Update-LunaScriptExcludes.py`, or a project-local copy of the window. The registry fields, keep-list format and marker strings are the same, and the output is formatted like `json.dump(indent=4)`.

1. Put the registry and keep lists at the paths in section 3.
2. Delete project copies of the window and writer (old menu `Tools/Playable Ads/Build Selector`).
3. Switch CI to the `-executeMethod` above.
4. Parity check per profile, Unity closed, clean `luna.json`: run the legacy script and copy `luna.json` aside, `git checkout -- luna.json`, run Prepare (batchmode or window) and copy again, then compare both files as JSON including key order.
5. Delete the legacy scripts.

## 11. Limits

- Keep lists are hand-maintained.
- Dependencies reached only from code (types used by kept scripts but not listed) are not detected. They surface as Luna stage-3 errors or empty script types.
- Asset references are not inspected.
- One creative per `luna.json`.
- Playworks UI changes between Prepare and build can still overwrite the file.
- Registry and keep-list folder paths are fixed constants.
