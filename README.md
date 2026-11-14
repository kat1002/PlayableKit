# PlayableKit

Luna / Playworks-safe toolkit for Unity playable ads, shipped as the UPM package **`com.kat.playablekit`** (v2.0.0).

## Install

Package Manager > Add package from git URL:

```
https://github.com/kat1002/PlayableKit.git?path=Packages/com.kat.playablekit
```

Pin a release with `#v2.0.0`. You can also import `PlayableKit.unitypackage` from this repo root. Requires Luna 6.4 / Playworks 7.2 and TextMeshPro in the target project.

## Docs

| Doc | What |
|---|---|
| [Package README](Packages/com.kat.playablekit/README.md) | Requirements, modules, quick start, editor tools, Luna gotchas |
| [Setup](Packages/com.kat.playablekit/Documentation~/Setup.md) | Starting a new playable |
| [UI Kit](Packages/com.kat.playablekit/Documentation~/UIKit.md) | Controllers, orientation, CTA |
| [CHANGELOG](Packages/com.kat.playablekit/CHANGELOG.md) | Release notes and the 1.x to 2.0 migration table |
| [Project scan report](Docs/ProjectScanReport.md) | What was found in other playable projects, and what was adopted |

## Develop

1. Open this folder in Unity 2022.3 (Luna/Playworks is referenced from `Packages/manifest.json`; re-point the `file:` path on your machine).
2. The package is embedded in `Packages/com.kat.playablekit`; edit it in place.
3. Import samples through Package Manager (they import into the git-ignored `Assets/Samples`).
4. Release: run **Tools > PlayableKit > Export PlayableKit.unitypackage** to refresh `PlayableKit.unitypackage`, and bump `package.json` plus the CHANGELOG.
