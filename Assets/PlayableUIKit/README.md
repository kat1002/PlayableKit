# PlayableUIKit

A standalone base for playable-ad UI controllers. Copy this whole folder into a new Unity
project's `Assets/` and it compiles immediately — it depends only on `UnityEngine`,
`UnityEngine.UI`, and `TMPro` (TextMeshPro), never on any specific ad SDK or game-specific type.

## What's here

- `Oriented<T>` / `UIOrientation` — a UI element authored once for landscape, once for portrait,
  as a typed field (`Oriented<GameObject> ctaPanel;`) instead of an index-convention array.
  `OrientedExtensions` gives `.SetActive(bool)`/`.GetActiveInHierarchy()` as extension methods so
  any code — not just `BaseUIController` subclasses — can use them.
- `BaseUIController` — base class for every UI controller. Owns orientation-change reaction, "go
  to store", and driving the lifecycle of composed `IUIFeature` components — all **optional**.
- `IUIFeature` / `ITickableFeature` — the plug-in contract for optional pieces of a controller (a
  CTA sequence, a reward popup, a punch button...). See "One controller, many plug-in features"
  below — this is the intended way to compose a scene's UI, not a fixed list of hand-built fields.
- `IAction` / `UIActionButton` — a generic button-to-action wiring so a button never needs to know
  *which* action it triggers. See "Swappable button actions" below.
- `IMoveInput` / `ISteerInput` — swappable input-source contracts (a joystick vs. a steering wheel
  vs. tilt) kept as two small interfaces, not one, because the underlying controls are genuinely
  different shapes (2D value vs. 1-axis value) — forcing one contract just moves the awkwardness.
- `CtaPresenter` — show/hide/retarget a CTA panel pair, composed (not inherited) by whichever
  controller/feature actually owns a CTA moment. One instance can safely serve **multiple** CTA
  moments across an owner and its composed features, as long as only one is ever shown at a time.
- `CountdownTimer` / `IdleTimer` — countdown-to-zero and count-up-with-reset primitives, composed
  by whichever controller/feature owns a session/idle timer. Kept as two classes, not one, because
  their reset semantics are opposites, not variants of the same shape.

## Plugging into a real project

`BaseUIController` needs two things a real project has to supply, each behind a one-method-ish
interface:

```csharp
public interface IOrientationSource { UIOrientation Current { get; } void Subscribe(...); void Unsubscribe(...); }
public interface IStoreGateway { void GoToStore(); }
```

Write one small `MonoBehaviour` adapter per interface, wrapping whatever this project's actual
event bus / ad SDK is (a handful of lines each — see this repo's own
`Assets/Shared/UI/Adapters/` for a worked example wrapping a Luna/PlayWorks-based project's
`GameEventManager`/`LunaManager`). Drop one instance of each adapter into the scene (once, shared
across controllers), then assign them on each `BaseUIController` subclass's
`orientationSourceBehaviour`/`storeGatewayBehaviour` fields — both are optional, so a controller
that doesn't need one just leaves it unassigned.

This is a plain interface + `[SerializeField] MonoBehaviour` field cast once in `Awake`, no DI
container, no reflection — safe for IL2CPP/WebGL playable-ad builds. Every pattern in this kit
follows that same shape.

## One controller, many plug-in features

**A scene should have one `BaseUIController` subclass, not one controller per concern.** Secondary
concerns (a reward popup, a punch button, a hand-off sequence) become `IUIFeature` components on
the *same* GameObject, auto-discovered by the host — not sibling `MonoBehaviour`s each reinventing
orientation handling, CTA wiring, etc.

```csharp
public interface IUIFeature
{
    void Init(BaseUIController host);   // called after the host's own OnControllerAwake
    void OnFeatureEnable();
    void OnFeatureDisable();
}
public interface ITickableFeature { void Tick(float deltaTime); } // optional, only if you need a per-frame tick
```

The host finds every `IUIFeature` on its GameObject via `GetComponents<IUIFeature>()` once, in
`Awake`, and drives `Init`/`OnFeatureEnable`/`OnFeatureDisable`/`Tick` automatically. **Adding or
removing a feature never touches the host's source** — drop the component on, or delete it.

```csharp
public sealed class RewardPopupFeature : MonoBehaviour, IUIFeature
{
    private BaseUIController _host;
    void IUIFeature.Init(BaseUIController host) => _host = host;
    void IUIFeature.OnFeatureEnable() { }
    void IUIFeature.OnFeatureDisable() { }
    // ... owns its own coroutines directly (it's a MonoBehaviour), reads _host.Cta / _host.Store
    // for the shared CTA panel and store call if it needs them ...
}
```

A feature with nothing to do every frame (e.g. a simple action-driven one) implements only
`IUIFeature`; one with a countdown/sequence to drive also implements `ITickableFeature`. Keep
`CtaPresenter`/`CountdownTimer`/`IdleTimer` as internal helpers *within* a feature, not as the
top-level composition unit — the feature component is the unit of add/remove, not the helper class.

If a feature needs to reach the host for something specific (a hand-off gate, in particular), a
direct `GetComponent<TheOtherFeature>()` lookup from `Init` is fine and matches this codebase's
house style — you don't need a generic interface for a one-off, project-specific coupling.

## Swappable button actions

Buttons decouple through a shared contract instead of the host owning one method per button:

```csharp
public interface IAction { void Perform(); bool IsBusy { get; } }
```

`UIActionButton` is generic — it wires a button's `onClick` to `IAction.Perform()` without knowing
what the action *is*:

```csharp
[SerializeField] private MonoBehaviour actionBehaviour; // any component implementing IAction
```

This is what answers "this character's punch is different from that one's": the button handler
never contains the punch logic, so it never needs to know there are multiple kinds. Different
characters/scenes just point their `UIActionButton` at a different `IAction` component
(`MeleeSwingPunch`, `ComboFsmPunch`, `ShootAction`, ...) — nothing shared needs editing, and adding
a new kind of action never touches `UIActionButton` itself.

## Writing a new controller

```csharp
public sealed class MyController : BaseUIController
{
    [SerializeField] private Oriented<GameObject> myPanel;

    protected override void OnControllerAwake()
    {
        Cta = new CtaPresenter(ctaPanel, fullscreenCTA); // if this controller owns a CTA moment
    }

    protected override void OnControllerEnable()
    {
        myPanel.SetActive(true); // Oriented<T> extension method, broadcasts to both Hor and Ver
    }

    protected override void OnControllerUpdate()
    {
        // per-frame logic — override this, not Update(), so composed features still get ticked first
    }

    protected override void OnOrientationChanged(UIOrientation orientation)
    {
        // react to a landscape/portrait flip, if you need to
    }
}
```

Never declare your own `Awake`/`OnEnable`/`OnDisable`/`Update` on a `BaseUIController` subclass —
override the `OnController*` hooks instead, so there's no "remember to call base.X()" foot-gun and
composed features are always driven correctly.

## Extending the same pattern to movement/input

`IMoveInput`/`ISteerInput` are implemented today by this project's joystick/steering-wheel
components — purely additive (the properties already existed with matching shapes, so adding the
interface declaration was a one-line, zero-behavior-change change).

```csharp
public interface IMovement { void Move(Vector2 axis); void Stop(); }
```

`IMovement` is a documented contract for a **future new mover**, not a retrofit target for an
existing monolithic character/vehicle controller. Don't attempt that retrofit blind: in this
project, the existing movers (a physics-driven player vehicle vs. a transform-teleporting AI
traffic mover) don't share a coherent movement contract in the first place — forcing one onto them
would just be a lowest-common-denominator abstraction that fits neither well — and a monolithic
character/vehicle controller is typically reused across several scenes/prefabs, so a behavior
regression there is a multi-playable incident for whatever this quarter's refactor was chasing.
If you're building a *new* mover, implement `IMovement` on it from day one; if you're looking at
retrofitting an old one, that's its own carefully-scoped investigation, not a drive-by change.
