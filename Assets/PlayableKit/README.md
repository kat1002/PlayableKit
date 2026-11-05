# PlayableKit

A self-contained, conflict-free Unity Playable Ads toolkit optimized for Unity Playworks (Luna / LunaX).

Consolidates all reusable runtime building blocks (Core orchestration, UI controls, video controllers, tweening, procedural effects, analytics) and Editor utilities into a single root directory (`Assets/PlayableKit/`) under isolated namespaces: `PlayableKit`, `PlayableKit.UI`, `PlayableKit.Video`, `PlayableKit.Tweening`, `PlayableKit.Analytics`, `PlayableKit.Editor`, and `PlayableKit.Tests`.

No external tween engine or TextMeshPro package dependency is required. All attachable MonoBehaviour scripts and kit assets are prefixed with `PA` (e.g. `PALayout`, `PALunaManager`, `PACtaButton`, `PA_UI_Shine_M.mat`).

---

## Directory Structure

```text
Assets/PlayableKit/
├── Docs/               # Kit documentation & setup guides (SETUP.md, UIKit.md)
├── Editor/             # Unified PAPlayableKitWindow, PALunaUiSanitizer, Exporter, Panels
├── Materials/          # PA_UI_ProgressFill_M.mat, PA_UI_Shine_M.mat
├── Prefabs/            # PACtaButton.prefab, PALunaRoot.prefab
├── Runtime/
│   ├── Analytics/      # PlayableAnalytics, IAnalyticsSink, PAAnalyticsButton
│   ├── Core/           # PALunaManager, PAGameManagerBase, PAAudioManagerBase, GameEventManager, etc.
│   ├── Effects/        # PAUIShine, PAUITapRing, PAUIIdleMotion, PAUIProgressFillFX, etc.
│   ├── Tweening/       # TweenLite (pure C# DOTween-compatible runner in PlayableKit.Tweening)
│   ├── Tweens/         # PADoScaleAToB, PADoMoveAToB, PADoFadeAToB, PAUiSlide, PATweenButton, etc.
│   ├── UI/             # PALayout, PASafeArea, PAAspectFitter, PADragScroll, PABaseUIController, etc.
│   └── Video/          # PAVideoStepController, PAVideoBootstrap, PAAutoPlayVideoOnEnable, etc.
├── Scenes/             # PATemplateScene.unity
├── Shaders/            # PA_UI_ProgressFill, PA_UI_Shine, PA_ChromaKeyGreenVideo, etc.
├── Sprites/            # PA_Light, PA_Ring, PA_Star, PA_Star2, PA_TapHand_1, PA_TapHand_2
└── Tests/Editor/       # LunaManagerSessionTests, GameSessionStateTests
```

---

## Core Features & Sub-Namespaces

### 1. `PlayableKit` (Core & State Machine)
- **`PALunaManager`**: Unified playable lifecycle manager handling Luna SDK (`LifeCycle`, `Playable`, `Analytics`), install repeat guard, auto-store timer, and no-interaction idle timeout.
- **`PAGameManagerBase`**: State machine (`Intro`, `Playing`, `Ended`), survival timer, midpoint triggers, crash flourish slow-mo, and CTA hand-off.
- **`PAAudioManagerBase`**: Audio manager with dual-source crossfade (engine/speed), SFX one-shots, and mute toggle.
- **`GameEventManager`**: Zero-allocation static event bus with reverse-iteration dispatch.
- **`GameSessionState`**: Decoupled pure C# session state machine for EditMode testability.
- **`PlayableLog`**: Lightweight console and browser logger for playable analytics and UI click debugging.

### 2. `PlayableKit.UI` (Luna-Safe UI & Adapters)
- **`PALayout`**: Direct `RectTransform` orientation presets (Landscape / Vertical) responding to orientation events without layout rebuilder overhead.
- **`PASafeArea`**: Normalized anchor fitting for device notches and camera cutouts.
- **`PAAspectFitter`**: Replacement for `AspectRatioFitter` without `DrivenRectTransformTracker`.
- **`PADragScroll`**: Horizontal inertial drag scroll without `ScrollRect` warnings.
- **`PABaseUIController` / `PAUIControllerBase`**: Base UI controller with landscape/portrait layout switching, timer hooks, and CTA forwarders.
- **`CountdownTimer` / `IdleTimer` / `CtaPresenter`**: Reusable pure C# UI helper classes.
- **`PABillboardToCamera`**, **`PAHoldButton`**, **`PAUiDragRotate`**, **`PAUITapHand`**.

### 3. `PlayableKit.Video` (Playworks Video Controllers)
- **`PAVideoStepController`**: Multi-click interactive video ads (play segment -> pause at gate -> show UI hook -> open store or resume on tap).
- **`PAVideoLoopSequenceController`**: Multi-button branching interactive video player with looping end segment.
- **`PAVideoBootstrap`**: Standalone video player startup with muted autoplay guard and soundtrack sync.
- **`PAAutoPlayVideoOnEnable`**, **`PAKeepRatio`**, **`PAVideoOrientationSizer`**, **`VideoPlayerLoopUtility`**.

### 4. `PlayableKit.Tweening` & `PlayableKit` (Tweens & Effects)
- **`TweenLite`**: Standalone tween runner supporting `DOScale`, `DOMove`, `DOLocalMove`, `DORotate`, `DOAnchorPos`, `DOFade`, `DOColor`, `DOFillAmount`, `DOJump`, `DOShakePosition`, `DOVirtual`, and sequences.
- **Tweens**: `PADoScaleAToB`, `PADoMoveAToB`, `PADoFadeAToB`, `PAUiSlide`, `PAUiPageSlide`, `PATweenButton`, `PATweenRotateButton`, `PALunaTweenAnimation`.
- **Procedural Effects**: `PAUIShine`, `PAUIProgressFillFX`, `PAUIStarTwinkle`, `PAUIPathTwinkle`, `PAUIIdleMotion`, `PAUITapRing`, `PAUIPraiseFly`, `PAUIStarBurst`, `PAUIPixelRingPop`, `PAUIPixelSmokeBurst`, `PAFallWindFx`.

### 5. `PlayableKit.Analytics`
- **`PlayableAnalytics`**: SDK-agnostic analytics hub broadcasting to registered `IAnalyticsSink` backends (`LunaAnalyticsSink`).
- **`PAAnalyticsButton`**: Component firing standard button click analytics.

### 6. `PlayableKit.Editor` (Unified Control Center)
Access via menu **`Tools -> PlayableKit -> PlayableKit Control Center`** (`Ctrl+Shift+K` / `Cmd+Shift+K`):
1. **Dependencies & Scanner**: Full project & active scene dependency scanner and forbidden API/code checker.
2. **Luna Compliance**: Scans for Spine binary `.skel.bytes`, unsupported shaders, DOTween DLL presence, and banned APIs.
3. **UI Sanitizer**: One-click active scene UI sanitizer replacing layout groups and scroll rects with Luna-safe equivalents.
4. **Utilities & Package Exporter**: One-click AssetBundle tag removal, Instance ID asset ping, and standalone `PlayableKit.unitypackage` exporter (which exports exclusively `Assets/PlayableKit`).
