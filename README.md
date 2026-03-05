# Ulink

Ulink lets you **link logic into UI in the Unity Builder** by attaching components directly to UXML elements. It uses a composition model—multiple components per element—and handles property injection, lifecycle, and editor/runtime control seamlessly.

---

## Overview

Ulink lets you attach reusable logic components to UI elements defined in UXML. You compose behavior from multiple components assigned via the Inspector drawer. Ulink handles setup, attach, and detach lifecycle events automatically.

---

## Key Strengths

- **Composition over inheritance**: Attach multiple components to a single element, each handling a focused concern.
- **Inspector-driven configuration**: Add, configure, and remove components through a dedicated property drawer—no code changes required.
- **Serialized properties**: Mark fields with `[UlinkProperty]` and configure them directly in the Inspector.
- **Lifecycle management**: Automatic calls to `Setup`, `OnAttach`, and `OnDetach` keep components properly initialized and torn down.
- **Editor awareness**: Control whether components run in the Editor, globally or per-component.

---

## Features

- **Component interface** — Implement `IUlinkComponent<T>` (or non-generic `IUlinkComponent`) to define reusable UI logic.
- **Element marking** — Add `[UlinkElement]` to a `VisualElement` class to enable component injection.
- **Inspector drawer** — Search, add, configure, and remove components through a dedicated property drawer.
- **Serialized properties** — Mark fields with `[UlinkProperty]` to expose them for configuration in the Inspector.
- **Runtime-only components** — Add `[UlinkRuntime]` to a component class to skip it during editor-time execution.
- **Manual initialization** — Use `UlinkExtensions.Initialize<T>()` to initialize a component outside the normal Ulink lifecycle.

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
- Configure `[UlinkProperty]` fields directly.
- Remove components when no longer needed.

### 4. Configure Properties with `[UlinkProperty]`

Mark fields on a component with `[UlinkProperty]` to expose them for configuration in the Inspector:

```csharp
public class ThresholdComponent : IUlinkComponent<VisualElement>
{
    [UlinkProperty] int threshold = 100;
    [UlinkProperty] string label = "Score";
    [UlinkProperty] MyEnum mode = MyEnum.Default;

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
| Any `enum` | Parsed by name                     |
| Any `UnityEngine.Object` subclass | Loaded via asset path;             |
| Other types | Attempted via `Convert.ChangeType()` |

### 5. Runtime-Only Components

Add `[UlinkRuntime]` to a component class to prevent it from running in the Editor:

```csharp
[UlinkRuntime]
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
[UlinkRuntime]
public class ScoreComponent : IUlinkComponent<Label>
{
    [UlinkProperty] public string prefix = "Score: ";

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

## Editor Mode Control

By default, components run both at runtime and in the Editor.

- **Global**: Toggle "**Run In Editor**" in the Ulink project settings.
- **Per-component**: Add `[UlinkRuntime]` to the component class.

`[UlinkRuntime]` takes precedence over the global setting for that component.

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

## License

This project is licensed under **Apache-2.0**.
