# Changelog

All notable changes to Ulink will be documented here.

---

## [0.8.0]

### Added
- `IUlinkComponent<T>` generic interface (and non-generic `IUlinkComponent`) — replaces `IUlinkController`.
- `[UlinkElement]` attribute — marks a `VisualElement` class for component injection.
- `[UlinkProperty]` attribute — marks fields for serialized configuration in the Inspector drawer.
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
