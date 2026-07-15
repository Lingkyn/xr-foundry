# Inventory Renderer-Neutral Architecture

Status: canonical initialization architecture for the Inventory package family.

The package family establishes two boundaries before its first complete release:

1. presentation semantics live outside renderer assemblies; and
2. every XR composition has a renderer-explicit package identity.

## Package graph

```text
com.lingkyn.inventory.core
  -> com.lingkyn.inventory.presentation
       -> com.lingkyn.inventory.ugui
            -> com.lingkyn.inventory.xr.ugui
       -> com.lingkyn.inventory.uitoolkit
            -> com.lingkyn.inventory.xr.uitoolkit
```

`com.lingkyn.inventory.unity` remains a separate authoring adapter. No shared XR
core is created until both working renderer routes demonstrate tested duplication.

## Presentation boundary

Install `com.lingkyn.inventory.presentation` at the same reviewed full commit SHA
as a renderer package. Presentation owns these renderer-neutral contracts:

```text
Lingkyn.Inventory.UGUI.InventoryPresenter
  -> Lingkyn.Inventory.Presentation.InventoryPresenter

Lingkyn.Inventory.UGUI.InventoryViewModel
  -> Lingkyn.Inventory.Presentation.InventoryViewModel
```

The same namespace owns `InventoryUiState`,
`InventorySlotViewModel`, `InventorySlotIntent`, and `IInventoryView`. Renderer
components such as `InventoryShellView`, `InventoryGridView`, and
`InventorySlotView` belong in `Lingkyn.Inventory.UGUI`.

## Renderer-explicit XR packages

XR composition is renderer-explicit in package ID, assembly, namespace,
documentation, catalog, and evidence. UGUI uses
`com.lingkyn.inventory.xr.ugui` / `Lingkyn.Inventory.XR.UGUI`; UI Toolkit uses
`com.lingkyn.inventory.xr.uitoolkit` / `Lingkyn.Inventory.XR.UIToolkit`.

Do not use the UGUI XR package as a meta-package for UI Toolkit. Install
`com.lingkyn.inventory.xr.uitoolkit` for that composition.

## Git URL consumers

XR Foundry does not publish these internal sibling versions through a public
scoped registry. A Git consumer must pin every selected
`com.lingkyn.inventory.*` dependency to the same reviewed full 40-character
commit SHA. Do not rely on a single Git URL to resolve custom sibling semver
dependencies.

## Evidence boundary

Only evidence for this exact package graph can promote it. Required evidence
includes:

- Presentation and each renderer package in a clean local consumer;
- the same packages from an immutable Git revision;
- each XR composition's Android build/install/open gate; and
- separate real-device receipts for each renderer, device/runtime profile, build
  hash, and input modality.

If any validation step fails, keep the affected package incubating and record the
first failed gate. A visible panel or a pass from another renderer is not a
substitute.
