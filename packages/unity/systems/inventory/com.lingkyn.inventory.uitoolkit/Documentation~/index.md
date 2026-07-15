# Inventory UI Toolkit adapter

## Required document contract

The root cloned by `UIDocument` must contain the names exposed by
`InventoryDocumentContract`: root, state label, message label, slot grid,
details label, and primary-action button. `InventoryDocumentView.Bind` throws
when any required part is absent so an incomplete replacement cannot pass as a
working adapter.

## Ownership

- Presentation owns immutable view state, semantic intents, and presenter policy.
- This package owns UI Toolkit element creation, state classes, and event binding.
- A consumer owns styling overrides, localization, item display data, and scene
  composition.
- The optional XR UI Toolkit package owns world-space/XRI validation.

Call `SetInteractionEnabled(false)` to leave content readable while suppressing
all semantic intent emission and disabling action controls. XR composition uses
this as one part of its fail-closed gate.
