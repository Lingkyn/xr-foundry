# Changelog

## 0.1.0 - 2026-07-15

- Added a renderer-specific Inventory UI Toolkit world-space composition for
  Unity 6.3 and XR Interaction Toolkit 3.5.1.
- Added a profile, shipped world-space PanelSettings, non-head-locked default
  placement, and fail-closed surface/scene validation.
- Added same-GameObject document/view/collider enforcement, exact trigger
  collider geometry checks, and explicit panel collider policy checks.
- Added a distinct maximum-interaction-distance profile value and validated
  PanelInputConfiguration layer/range reach when an EventSystem exists.
- Added a provider-neutral setup sample plus negative EditMode gates and a
  PlayMode gate using a real UI-enabled XRRayInteractor.
- Recorded explicit non-claims: automated validation does not establish any
  XR hover/activation, real-device, controller, hand, poke, readability, or
  comfort result.
