# Ulink

Ulink lets you **link logic into UI in the Unity Builder** by injecting controllers directly into UXML UI elements. It supports two flexible injection methods—via inheritance or factory pattern—and handles serialization, binding, and lifecycle seamlessly.

---

## Overview

Ulink empowers developers to bind custom logic to UI elements defined in UXML. It does so by injecting controllers that manage UI element behavior at runtime (and optionally during edit-time). This decouples UI definition from logic, leading to cleaner, more modular code.

---

## Key Strengths

- **Flexible Injection**: Choose between classic inheritance (via `IUlinkController`) or a factory pattern (`UlinkFactory`).
- **Lifecycle Management**: Automatic calls to `OnSerialize`, `Bind`, and `Unbind` ensure controllers are properly initialized and torn down.
- **Editor Awareness**: You can configure controllers to run in editor mode, either globally or on a per-controller basis.
- **Extensible**: Factories can inject sprites or other objects into controllers, supporting reusable, dynamic UI logic.

---

## Features

- **Dual injection approaches**  
  1. Extend `IUlinkController` and implement its interface.  
  2. Extend `UlinkFactory` and override its logic-constructing function.

- **UXML attribute-based wiring**  
  Assign a `UlinkController` attribute inside UXML, and Ulink will generate the necessary bindings at compile- or runtime.

- **Lifecycle hooks**  
  - `OnSerialize`: Called when the controller is created, capturing the root element.  
  - `Bind` / `Unbind`: Called during attach and detach phases for proper lifecycle management.

- **Factory injection**  
  Use a `UlinkFactory` to create sprites or other objects and inject them into UI elements tagged with a `UlinkFactoryAttribute`.

- **Editor-mode control**  
  Control whether controllers run in the Unity Editor—configurable globally or per-controller.

---

## Installation

1. Open Unity’s **Package Manager** (Window → Package Manager).  
2. Click the **‘+’** button and choose **“Add package from Git URL…”**.  
3. Enter the GitHub URL:
   ```
   https://github.com/Mercury-Leo/Ulink.git
   ```
4. Click **Add Package**. Unity will fetch and install Ulink into your project.

---

## Usage

### 1. Controller via Interface

Create a controller by extending `IUlinkController`:

```csharp
public class MyController : IUlinkController
{
    public bool RuntimeOnly { get; } 
    public void OnSerialize(VisualElement root) { /* ... */ }
    public void Bind() { /* ... */ }
    public void Unbind() { /* ... */ }
}
```

In your custom UXML element:

```csharp
    [UxmlElement]
    [UlinkController]
    public partial class ExampleElement : VisualElement
    {
        public ExampleElement() { }
    }
```

Ulink will generate the necessary wiring so that `OnSerialize`, `Bind`, and `Unbind` are invoked appropriately.

### 2. Controller via Factory

Define a factory by extending `UlinkFactory`:

```csharp
public class MyFactory : UlinkFactory
{
    public override IUlinkController CreateController() 
    {
        var controller = new MyControllerWithSprite();
        controller.Sprite = LoadMySprite();
        return controller;
    }
}
```

In your custom UXML element:

```csharp
    [UxmlElement]
    [UlinkFactory]
    public partial class ExampleElement : VisualElement
    {
        public ExampleElement() { }
    }
```

Ulink will create and inject the controller with any required dependencies.

It is possible to use both at once.
```csharp
    [UxmlElement]
    [UlinkController]
    [UlinkFactory]
    public partial class ExampleElement : VisualElement
    {
        public ExampleElement() { }
    }
```

### 3. Editor Mode Control

By default, controllers run at runtime and Editor. To disable editor-time execution:

- Configure globally in project settings "**Run in Editor**".  
- Or override per controller - Set the ```RuntimeOnly``` to ```true```.

---

## Example

```csharp
public class ScoreController : IUlinkController
{
    public bool RuntimeOnly => true;

    Label scoreLabel;

    public void OnSerialize(VisualElement root)
    {
        scoreLabel = root.Q<Label>("score");
    }

    public void Bind()
    {
        ScoreManager.OnScoreChanged += UpdateScore;
    }

    public void Unbind()
    {
        ScoreManager.OnScoreChanged -= UpdateScore;
    }

    void UpdateScore(int newScore)
    {
        scoreLabel.text = $"Score: {newScore}";
    }
}
```

## Summary

Ulink streamlines how UI and logic interact in Unity’s UXML-based UI. Whether you prefer interface-based controllers or factories, it ensures clean separation, lifecycle handling, and optional editor integration—making your UI code more maintainable and scalable.

---

## License

This project is licensed under **Apache‑2.0**.
