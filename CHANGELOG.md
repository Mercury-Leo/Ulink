# Changelog

All notable changes to Ulink will be documented here.

---

## [0.8.7]

### Changed
- `IUlinkElement` now exposes the four component query methods as interface members:
  `TryGetComponent<T>`, `GetComponent<T>`, `GetComponents<T>()`, and `GetComponents<T>(List<T>)`.
  Callers holding an `IUlinkElement` reference can query components without casting to the concrete type.

---

## [0.8.5]

### Added
- Generated `TryGetComponents<TComponent>()` and `TryGetComponents<TComponent>(List<TComponent>)`
  with Unity-style equivalents on the generated `ComponentsType`:
  - `GetComponent<TComponent>()` — returns first match or `null`
  - `GetComponents<TComponent>()` — returns all matches as `TComponent[]`
  - `GetComponents<TComponent>(List<TComponent> results)` — fill-in-place, no allocation

---

## [0.8.6]

### Added
- `LocalizedString` support for `[UlinkSerializable]` fields (requires `com.unity.localization`).
  - Inspector drawer uses Unity's built-in `PropertyField` for table/entry selection.
  - Runtime converter automatically reconstructs the `LocalizedString` from stored data.
  - Both the editor and runtime bridges are compiled only when the `ULINK_LOCALIZATION`
    scripting define is active, so the Localization package remains an optional dependency.

---

## [0.8.3]

### Added
- `Vector2`, `Vector3`, `Vector4`, and `Color` as supported `[UlinkSerializable]` field types (injector + Inspector drawer).
- `UlinkAssetRegistry` ScriptableObject for runtime GUID-to-asset resolution; auto-synced by the generator from UXML references.

### Changed
- Renamed `[UlinkRuntime]` attribute to `[UlinkRuntimeOnly]`.

---

## [0.8.0]

### Added
- `IUlinkComponent<T>` generic interface (and non-generic `IUlinkComponent`) — replaces `IUlinkController`.
- `[UlinkElement]` attribute — marks a `VisualElement` class for component injection.
- `[UlinkSerializable]` attribute — marks fields for serialized configuration in the Inspector drawer.
- `[UlinkRuntime]` attribute — marks a component as runtime-only; replaces the `RuntimeOnly` property.
- Inspector property drawer for component management: search, add, configure, and remove components.
- Multiple components per element — compose behavior from independent, focused components.
- New lifecycle: `Setup(T element)`, `OnAttach()`, `OnDetach()`.
- `UlinkExtensions.Initialize<T>()` — manually initialize a component outside the Ulink lifecycle.

### Removed
- `IUlinkController` interface.
- `UlinkFactory` base class.
- `[UlinkController]` and `[UlinkFactory]` attributes.
- `OnSerialize`, `Bind`, and `Unbind` lifecycle methods.
- `RuntimeOnly` property (replaced by `[UlinkRuntime]` attribute).
