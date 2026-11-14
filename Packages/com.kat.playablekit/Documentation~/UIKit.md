# UI Kit

Orientation-aware building blocks for playable-ad UI. Lives in `PlayableKit.UI` (needs Core, Audio, ugui and TextMeshPro); the ready-made game overlay `PAGameUIController` lives in `PlayableKit.Flow`.

## What's here

| Type | Purpose |
|---|---|
| `Oriented<T>` / `UIOrientation` | A UI element authored once for landscape (`Hor`) and once for portrait (`Ver`). Runtime-only. |
| `OrientedGameObject`, `OrientedButton`, `OrientedText` | Serializable versions of the above (Luna cannot serialize generic `[Serializable]` types). Convert implicitly to `Oriented<T>`. |
| `OrientedExtensions` | `SetActive`, `SetText`, `GetActiveInHierarchy` on any `Oriented<T>`. |
| `PABaseUIController` | Base class for UI controllers: orientation flips and "go to store". |
| `CtaPresenter` | Show / hide / retarget a CTA panel pair (landscape + portrait, plus an optional fullscreen CTA). |
| `CountdownTimer`, `IdleTimer` | Countdown-to-zero and count-up-with-reset helpers. Kept separate because their reset semantics are opposites. |
| `PALayout`, `PAOrientationLayout` | Per-element layout presets vs a central slot list (see below). |
| `PASafeArea`, `PAAspectFitter`, `PAPortraitFrame`, `PATextOutline`, `PALoadingScreenController`, `PAMuteToggleButton`, `PACtaOverlay` | Single-purpose components. |
| `PAScreenOrientationChecker` | Optional: raises orientation events for `PASafeArea`, `PALayout` and Video components. |

## Orientation

`Orientation.IsLandscape` (Core) is the single definition of landscape. `PABaseUIController` reads it in `Awake`, polls it every frame, and calls `OnOrientationChanged(UIOrientation)` when it flips. This works with or without a `PAScreenOrientationChecker` in the scene, and needs no adapter or interface.

## Writing a controller

```csharp
using PlayableKit.UI;
using UnityEngine;

public sealed class MyController : PABaseUIController
{
    [SerializeField] private OrientedGameObject myPanel;
    [SerializeField] private OrientedGameObject ctaPanel;
    [SerializeField] private GameObject fullscreenCta;

    protected override void OnControllerAwake()
    {
        CtaPanel = new CtaPresenter(ctaPanel, fullscreenCta);
    }

    protected override void OnControllerEnable()
    {
        SetActive(myPanel, true);        // both Hor and Ver
    }

    protected override void OnControllerUpdate()
    {
        // per-frame logic
    }

    protected override void OnOrientationChanged(UIOrientation orientation)
    {
        // landscape / portrait flipped: reset held input, re-layout, ...
    }

    public void OnCtaClicked()
    {
        GoToStore();                     // null-safe call into PALunaManager
    }
}
```

| Hook | When |
|---|---|
| `OnControllerAwake` | Once, in Awake, after `CurrentOrientation` is set |
| `OnControllerEnable` / `OnControllerDisable` | OnEnable / OnDisable |
| `OnControllerUpdate` | Every frame |
| `OnOrientationChanged(UIOrientation)` | On every landscape / portrait flip |

Rules:

- Never declare your own `Awake`, `OnEnable`, `OnDisable` or `Update` on a `PABaseUIController` subclass. Override the `OnController*` hooks instead, so there is no `base.X()` to forget.
- `CurrentOrientation` holds the current value; `CtaPanel` holds a `CtaPresenter` you create in `OnControllerAwake`.
- `GoToStore()` is `protected static`. Outside a controller, call `PALunaManager.Instance.GoToStore()` or put a `PALunaStoreGateway` component on an object and point a Button `OnClick` at it.
- Unity-object null checks use `== null`, never `?.`.

## Ready-made overlay: PAGameUIController

`PlayableKit.Flow.PAGameUIController : PABaseUIController` drives tap zones, the tutorial hand, the progress bar and the CTA end card. Use it as-is (fill `Portrait Core` / `Landscape Core`) or subclass it.

| Member | Purpose |
|---|---|
| `ShowIntro()` / `HideIntro()` / `OnFirstTap()` | Intro hint, hand and pulses |
| `SetProgress(float)` | Progress fill 0..1 |
| `ShowEndCard()` | Shows the end card (tracks `endcard_shown` once) |
| `Cta()` | Plays the tap sound and calls `GoToStore()` |
| `BuildCoreRefs()` | Override to copy your own ref class into the core refs |
| `OnHudTick()` | Throttled by `hudTickInterval` |

It re-applies the layout and re-shows the intro on `OnOrientationChanged`. `PACtaClickForwarder` forwards a tap on a full-screen catcher to `Cta()`.

## PALayout vs PAOrientationLayout

| | `PALayout` | `PAOrientationLayout` |
|---|---|---|
| Model | Per-element presets, driven by orientation events | One central slot list plus the CanvasScaler reference resolution |
| Use when | A few elements need different anchors per orientation | Whole screens re-arrange between orientations |

Both read `Orientation.IsLandscape`. Pick one per element.

## Input contracts

`IMoveInput` (`PlayableKit.Controls`) exposes a 2D `Value` and `IsPressed`. `PAJoystick` implements it; implement it on your own steering or tilt source so gameplay code stays input-agnostic. `ISteerInput` was removed in 2.0.

## Migrating from 1.x

| 1.x | 2.0 |
|---|---|
| `IOrientationSource` + `PAGameEventOrientationSource` + `orientationSourceBehaviour` field | Delete. Override `OnOrientationChanged`. |
| `IStoreGateway` + `storeGatewayBehaviour` field + `Store` | Delete. Call `GoToStore()`. |
| `PAUIControllerBase` (`Awake` / `Update` overrides) | `PAGameUIController`: override `OnControllerAwake` / `OnControllerUpdate`. |
