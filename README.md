# PlayableKit (Unified Best-of-Breed Repository)

Consolidated reusable toolkit for Unity Playable Ads (Unity Playworks / Luna), combining the best versions of every playable kit, template, UI sanitizer, video controller, and editor compliance checker across all workspace projects (`PlayableStarterKit`, `GangterCity`, `CraftPlayable`, `Obby-Playable-Ads`, `squishy_pa`, `scoop_pa`, `mukbang_pa`, `Home-Clean-Playable`, `Playable-Ads-Fake-Video`, `VideoPlayer`, `BikePlayable`, and `Package`).

## Key Highlights

- **Zero External Tween Dependency (100% Luna-Safe)**: Bundles `TweenLite` (`Playable.Tweening`) with a familiar `DOTween`-style fluent API (`DOScale`, `DOMove`, `DOAnchorPos`, `DOFade`, `DOColor`, `DOFillAmount`, `DOShakePosition`, `DOVirtual.DelayedCall`, `DOTween.Sequence`, `WaitForCompletion`).
- **Update-Driven UI FX**: `UIShine` and `UIStarTwinkle` run on zero-allocation `Update` loops for reliable behavior in Luna / WebGL.
- **Full Analytics & Diagnostics**: Includes `PlayableAnalytics`, `IPlayableAnalyticsSink`, `LunaAnalyticsSink`, and `PlayableLog`.

---

## Installation

### Via Unity Package Manager (Git URL)

1. In Unity Editor, open **Window** > **Package Manager**.
2. Click the **+** button in the top left corner of the window.
3. Select **Add package from git URL...**.
4. Paste the Git URL:
   ```text
   https://github.com/kat1002/PlayableKit.git
   ```
   *(Optional)* You can also specify a specific branch, tag, or commit hash (e.g. `#master`):
   ```text
   https://github.com/kat1002/PlayableKit.git#master
   ```

### Via `Packages/manifest.json`

Add the package entry directly to your project's `Packages/manifest.json` dependencies:

```json
{
  "dependencies": {
    "com.kat.playablekit": "https://github.com/kat1002/PlayableKit.git"
  }
}
```

---

## Repository Structure

```text
PlayableKit/
├── PlayableKit/                        # Core Tweening, UI FX, Analytics & Shaders
│   ├── Editor/
│   │   ├── PlayableKitMenu.cs
│   │   └── PlayableUIKitMenu.cs
│   ├── Materials/
│   │   └── UI_ProgressFill_M.mat
│   ├── Runtime/
│   │   ├── Analytics/                  # PlayableAnalytics, IPlayableAnalyticsSink
│   │   ├── Diagnostics/                # PlayableLog
│   │   ├── Effects/                    # UIFloatingText, UIFlyToTarget, UIIdleMotion, UIPathTwinkle,
│   │   │                               # UIPixelRingPop, UIPixelSmokeBurst, UIPraiseFly, UIProgressFillFX,
│   │   │                               # UIShine, UIStarBurst, UIStarTwinkle, UITapHand, UITapRipple, UITapRing
│   │   ├── Tweening/                   # TweenLite.cs (namespace Playable.Tweening)
│   │   ├── Tweens/                     # DoMoveAToB, DoRotateLoop, DoScaleAToB, DoShakeLoop,
│   │   │                               # LunaTweenAnimation, TweenButton, TweenRotateButton
│   │   └── UI/                         # UIButtonFX, UIPopup, UIProgressBar
│   ├── Shaders/
│   │   └── UI_ProgressFill.shader
│   └── Sprites/                        # Default particles (circle, glow, ring, smoke, sparkle, star, tap hand)
│
├── PlayableUIKit/                      # Modular Playable UI & Luna-Safe Layout Components
│   ├── Adapters/                       # DefaultStoreGateway, ScreenRatioOrientationSource
│   ├── Core/                           # PlayableClock, PlayableStore, ResponsiveCanvas
│   ├── FX/                             # ConfettiBurst, FloatingFeedback, IdleFloat,IdlePulse,
│   │                                   # RadialBurst,RedAlertOverlay, ShineSweep
│   ├── LunaSafeUI/                     # PAAspectFitter, PADragScroll, PALayout, PASafeArea
│   │   └── Editor/                     # PALunaUiSanitizer (Tools > PA > Sanitize UI For Luna)
│   ├── Tweens/                         # UIFadeIn, UIMoveBetween, UIPopIn, UIRotateLoop, UIScaleBetween, UIShakeLoop
│   └── Widgets/                        #AnimatedCounter, CountdownTimer, CtaButton, EndcardController,
│                                       # SegmentedProgressBar,SimpleProgressBar, StepCheckProgress, TutorialHand
│
├── Core/                               # Lifecycle, Orientation, Event Bus & Video Playable Helpers
│   ├── Adapters/                       # GameEventOrientationSource, LunaAnalyticsSink, LunaStoreGateway
│   ├── Editor/                         # PlayableSpiderLunaPhysicsPatch
│   ├── Events/                         # GameEventManager (Zero-GC reverse iteration)
│   ├── Luna/                           # Unified LunaManager (+ EditMode Session tests), CtaTapCounter,
│   │   │                               # PlayableOrientationLayout, PlayableStoreReturn, PlayableTextOutline,
│   │   │                               # PlayableCtaOverlay, PlayablePortraitFrame, FreezeSpineRigAfterWarmup,
│   │   │                               # PlayworksVideoBootstrap, VideoPlayOrientationSizer, ChromaKey/Outline Shaders
│   │   └── Editor/                     # LunaManagerSessionTests
│   ├── UI/                             # ScreenOrientationChecker, SoundEnifi, UiManager
│   └── Video/                          # PlayableVideoStepController, AutoPlayVideoOnEnable,
│                                       # VideoLoopSequenceController, VideoPlayerLoopUtility, KeepRatio
│
├── MyLunaUtils/                        # Luna Compliance Checkers, Cleaners, Config Sync & Scratch Card
│   ├── ScratchSpriteRenderer.cs        # Runtime Texture2D Color32 scratch/reveal effect
│   └── Editor/
│       ├── PlayableAdsCheckerWindow.cs # Tools > Sp Unity > Playable Ads Checker (includes C# 8+ syntax checks)
│       ├── MyLunaComplianceWindow.cs   # Tools > MyLunaHelper > Compliance Tool
│       ├── MyLunaComplianceScanner.cs
│       ├── LunaSpineSceneSkelScanner.cs# Tools > MyLunaHelper > Spine Binary Inspector
│       ├── CleanerTool.cs              # Tools > MyLunaHelper > Force Remove All AssetBundles
│       ├── FindAssetByID.cs            # Tools > MyLunaHelper > Find Asset By ID
│       └── LunaTools/                  # Tools > Luna > Refresh From Disk / Apply To luna.json
│
├── PlayableTemplate/                   # Genre-Agnostic Base Scene & Lifecycle Template (PlayableTemplate.Core)
│   ├── SETUP.md
│   ├── Prefabs/                        # CtaButton.prefab
│   ├── Scripts/
│   │   ├── Core/                       # GameManagerBase, GameSessionState, UIControllerBase,
│   │   │                               # AudioManagerBase, MuteToggleButton, CameraShake, LoadingScreenController
│   │   └── Editor/                     # LunaPreflightChecks, PlayableExportTools, PlayableTemplateSceneBuilder
│   └── Tests/Editor/                   # GameSessionStateTests
│
└── Reference/
    ├── luna.json
    └── SoundManager.cs.txt
```
