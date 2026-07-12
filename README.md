# Ulink

Ulink lets you **link logic into UI in the Unity Builder** by attaching components directly to UXML elements. It uses a composition model—multiple components per element—and handles property injection, lifecycle, and editor/runtime control seamlessly.

---

## Overview

Ulink lets you attach reusable logic components to UI elements defined in UXML. You compose behavior from multiple components assigned via the Inspector drawer. Ulink handles setup, attach, and detach lifecycle events automatically.

---

## Key Strengths

- **Composition over inheritance**: Attach multiple components to a single element, each handling a focused concern.
- **Inspector-driven configuration**: Add, configure, and remove components through a dedicated property drawer—no code changes required.
- **Serialized properties**: Mark fields with `[UlinkSerializable]` and configure them directly in the Inspector.
- **Lifecycle management**: Automatic calls to `Setup`, `OnAttach`, and `OnDetach` keep components properly initialized and torn down.
- **Editor awareness**: Control whether components run in the Editor, globally or per-component.

---

## Features

- **Component interface** — Implement `IUlinkComponent<T>` (or non-generic `IUlinkComponent`) to define reusable UI logic.
- **Element marking** — Add `[UlinkElement]` to a `VisualElement` class to enable component injection.
- **Inspector drawer** — Search, add, configure, and remove components through a dedicated property drawer.
- **Serialized properties** — Mark fields with `[UlinkSerializable]` to expose them for configuration in the Inspector.
- **Runtime-only components** — Add `[UlinkRuntimeOnly]` to a component class to skip it during editor-time execution.
- **Manual initialization** — Use `UlinkExtensions.Initialize<T>()` to initialize a component outside the normal Ulink lifecycle.
- **`IUlinkElement` interface** — Every `[UlinkElement]` class implements `IUlinkElement`, which exposes
  `TryGetComponent<T>`, `GetComponent<T>`, and `GetComponents<T>` directly on the interface.
  Use it to query components from any code holding a `VisualElement` reference without knowing the concrete type:
  ```csharp
  if (element is IUlinkElement uel && uel.TryGetComponent<MyComp>(out var c)) { ... }

  // LINQ traversal over a panel:
  panel.Query<VisualElement>().ToList().OfType<IUlinkElement>()
      .Select(e => e.GetComponent<MyComp>())
      .Where(c => c != null)
  ```

---

## Installation

1. Open Unity's **Package Manager** (Window → Package Manager).
2. Click the **'+'** button and choose **"Add package from Git URL…"**.
3. Enter the GitHub URL:
   ```
   https://github.com/Mercury-Leo/Ulink.git
   ```
4. Click **Add Package**. Unity will fetch and install Ulink into your project.

---

## Usage

### 1. Create a Component

Implement `IUlinkComponent<T>` where `T` is the target `VisualElement` type:

```csharp
public class ScoreComponent : IUlinkComponent<Label>
{
    public void Setup(Label element)
    {
        // Store a reference; called once when the component is assigned.
    }

    public void OnAttach()
    {
        // Register event listeners; called when the element attaches to a panel.
    }

    public void OnDetach()
    {
        // Unregister event listeners; called when the element detaches from a panel.
    }
}
```

For components that work with any `VisualElement`, implement the non-generic `IUlinkComponent`:

```csharp
public class MyComponent : IUlinkComponent
{
    public void Setup(VisualElement element) { /* ... */ }
    public void OnAttach() { /* ... */ }
    public void OnDetach() { /* ... */ }
}
```

### 2. Mark an Element

Add `[UlinkElement]` to your custom `VisualElement` class to enable component injection:

```csharp
[UxmlElement]
[UlinkElement]
public partial class ScoreDisplay : VisualElement
{
    public ScoreDisplay() { }
}
```

### 3. Add Components via the Inspector

With `[UlinkElement]` applied, a component drawer appears in the UI Builder Inspector for that element. Use it to:

- Search for and add components by type.
- Configure `[UlinkSerializable]` fields directly.
- Remove components when no longer needed.

### 4. Configure Properties with `[UlinkSerializable]`

Mark fields on a component with `[UlinkSerializable]` to expose them for configuration in the Inspector:

```csharp
public class ThresholdComponent : IUlinkComponent<VisualElement>
{
    [UlinkSerializable] int threshold = 100;
    [UlinkSerializable] string label = "Score";
    [UlinkSerializable] MyEnum mode = MyEnum.Default;

    public void Setup(VisualElement element) { /* ... */ }
    public void OnAttach() { /* ... */ }
    public void OnDetach() { /* ... */ }
}
```

#### Supported Property Types

| Type | Notes                              |
|------|------------------------------------|
| `string` |                                    |
| `int` |                                    |
| `float` |                                    |
| `double` |                                    |
| `long` |                                    |
| `bool` |                                    |
| `Vector2` |                                    |
| `Vector3` |                                    |
| `Vector4` |                                    |
| `Color` |                                    |
| Any `enum` | Parsed by name                     |
| Any `UnityEngine.Object` subclass | Resolved via `UlinkAssetRegistry` at runtime; loaded via asset path in the Editor |
| Other types | Attempted via `Convert.ChangeType()` |

### 5. Runtime-Only Components

Add `[UlinkRuntimeOnly]` to a component class to prevent it from running in the Editor:

```csharp
[UlinkRuntimeOnly]
public class ScoreComponent : IUlinkComponent<Label>
{
    // This component will only run at runtime, not in edit mode.
}
```

You can also disable editor execution globally in the Ulink project settings ("**Run In Editor**" toggle).

### 6. Manual Initialization

Use `UlinkExtensions.Initialize<T>()` to wire up a component outside the normal Ulink lifecycle (e.g., when not using the Builder):

```csharp
var component = new ScoreComponent();
component.Initialize(myLabel);
```

This calls `Setup`, then hooks `OnAttach` and `OnDetach` to the element's panel events.

---

## Example

A complete component that tracks a score and updates a label:

```csharp
[UlinkRuntimeOnly]
public class ScoreComponent : IUlinkComponent<Label>
{
    [UlinkSerializable] public string prefix = "Score: ";

    Label _label;

    public void Setup(Label element)
    {
        _label = element;
    }

    public void OnAttach() { }

    public void OnDetach() { }
}
```

Mark the element in UXML:

```csharp
[UxmlElement]
[UlinkElement]
public partial class ScoreDisplay : VisualElement
{
    public ScoreDisplay() { }
}
```

Then open the element in the UI Builder, add `ScoreComponent` via the Inspector drawer, and configure `prefix` as needed.

---

## UlinkAssetRegistry

`UlinkAssetRegistry` is a `ScriptableObject` that maps asset GUIDs to `UnityEngine.Object` references for runtime resolution of `[UlinkSerializable]` fields typed as `UnityEngine.Object` subclasses.

It is **auto-generated and kept in sync** by the Ulink generator at `Assets/Generated/Ulink/Resources/`. You do not need to create or manage it manually.

This registry is required for builds that lack access to `AssetDatabase` (runtime, IL2CPP). In the Editor, asset paths are used directly and the registry is not needed.

---

## Editor Mode Control

By default, components run both at runtime and in the Editor.

- **Global**: Toggle "**Run In Editor**" in **Edit → Project Settings → Leo's Tools/Ulink**. You can also trigger generation manually via **Tools/Leo's Tools/Ulink/Generate**.
- **Per-component**: Add `[UlinkRuntimeOnly]` to the component class.

`[UlinkRuntimeOnly]` takes precedence over the global setting for that component.

---

## Performance & Benchmarks

Ulink's composition model has a cost. Where a vanilla `VisualElement` subclass **is** its own logic
(inheritance), a Ulink element **instantiates, injects, and sets up each attached component** when it
is built. This is a deliberate tradeoff: **you pay more at construction and first-time boot in
exchange for composition, Inspector-driven configuration, and cheap runtime access.**

The rule of thumb is **build cost is higher, access cost is not**. If you construct UI once and
interact with it many times — the common case — the overhead amortizes to near zero. If you rebuild
very large panels frequently, the construction cost is real; prefer pooling and reuse there.

### What was measured

Each scenario compares a Ulink element (logic in components) against a vanilla element (logic via
inheritance), across panels of 1 / 10 / 100 / 1000 elements. Ratios below are Ulink relative to
vanilla; absolute timings are hardware-dependent, so the ratio is the portable signal.

| Scenario | What it measures | Ulink vs vanilla |
|---|---|---|
| **Access** | Retrieve one component and invoke it (`GetComponent` / `TryGetComponent`) | ≈ a cached reference; **flat** as the panel grows, and far cheaper than a fresh `Q<>()` |
| **Bulk query** | Process every matching element in a panel | ~30–60% slower; the no-alloc `GetComponents(List<T>)` path matches vanilla's allocations |
| **Construction** | Clone a UXML template + instantiate/inject/Setup | ~2–3× for one component, ~5× for three — **this is where the tradeoff lives** |
| **Lifecycle** | Attach/detach on a live panel | ~1.25–1.8× (fans `OnAttach`/`OnDetach` across each element's component list) |
| **Boot** | First panel bring-up (cold) | a **one-time** init (asset registry load + type resolution + JIT), then steady state |

Access stays close to native because a component lookup is a short linear scan over an element's own
component list — not a panel-wide tree query — so it does not degrade as the panel grows.

### Why boot/construction is slower (and why that's fine)

Building a Ulink element does real work per component that vanilla never does: create the component,
inject its `[UlinkSerializable]` fields, call `Setup`, and wire lifecycle callbacks. Three components
therefore cost roughly three times the setup of one. The first Ulink panel of a session also pays a
small **one-time** cost to warm the `UlinkAssetRegistry`, resolve component types, and JIT the build
path.

To keep that cost as low as possible, the hot path is **code-generated, not reflective**:

- Components are created through generated `() => new Component()` factories — no `Activator.CreateInstance`.
- `[UlinkSerializable]` fields are assigned by generated typed setters — no reflection `SetValue`, no boxing of primitives.
- Type resolution is cached process-wide, and per-element structures (component lists, lifecycle
  delegates) are allocated lazily or cached, so a populated panel allocates far less than a naive
  composition layer would.

All of this is AOT/IL2CPP-safe (no `Reflection.Emit` or `Expression.Compile`).

### Tradeoff summary

- ✅ **Cheap steady-state access** — `GetComponent` runs at roughly cached-field speed and does not degrade with panel size.
- ✅ **Allocation-lean, reflection-free hot path** — creation and injection are generated, so the runtime cost is low and AOT-safe.
- ⚠️ **Higher construction cost** — each component is instantiated, injected, and set up when the element is built.
- ⚠️ **A one-time boot cost** — the first Ulink panel of a session warms the registry, type resolution, and JIT.

**Choose Ulink** when your UI is composed, configured in the Builder, and built-then-used. **Prefer
plain inheritance** when you churn very large panels on the hot path and every microsecond of
construction counts.

---

## Known Issues

1. Changing a component's path will result in a lose of serialization. Looking into ways to fix this.
2. Generates one big file for each assembly definition. Looking for a solution with a source generator, non trivial.

---

## License

This project is licensed under **Apache-2.0**.
