# Changelog

## Unreleased

- Added `foundry.tunables.json` (WI-022): declares the `InventoryUiToolkitSkin`
  seam (`ApplySkin`) and its nine surface/text/slot-state colour members as
  data for the live-tuning developer scaffold, with defaults equal to the
  canonical design-language token values and a `token` reference for each;
  the package now lists `xr-foundry.tuning.surface` 1.0.0 in
  `foundry.component.json` `provides`. See
  `docs/standards/live-tuning/tunable-surface.md`.
- Added the injectable design-language skin seam: `InventoryUiToolkitSkin`
  (ScriptableObject with the canonical shared tokens and a `CreateDefault()`
  factory) and `InventoryDocumentView.ApplySkin`, which writes the tokens as
  inline styles on the bound root, grid, message, primary action, and every
  current or later slot. With no skin injected the shipped USS drives the look
  unchanged.
- Slot buttons now track pointer enter/leave so the hover token applies when a
  skin is present; the disabled token's RGB colors a disabled slot and its alpha
  dims the whole slot.
- Added EditMode coverage for skinned enabled/disabled/selected/re-rendered
  slots, skin removal, the un-skinned stylesheet path, and the canonical default
  values. These changes have not executed in an Editor gate; the package stays
  at the catalogued version until a receipt exists (LESSON-008).

## 0.1.0 - 2026-07-15

- Added a renderer-only UI Toolkit adapter for neutral Inventory presentation
  state and semantic slot intents.
- Added functional UXML/USS assets, bounded scrolling, runtime slot binding,
  interaction gating, and replaceable named-element contracts.
- Added a replayable State Gallery sample plus EditMode and PlayMode coverage.
