# Lingkyn Inventory Presentation

Renderer-neutral Inventory presentation contracts for Unity consumers. The runtime
assembly references Inventory Core but has `noEngineReferences: true`: it does not
depend on UnityEngine, UGUI, UI Toolkit, XR Interaction Toolkit, or a device SDK.

The package owns:

- immutable `InventoryViewModel` and `InventorySlotViewModel` snapshots;
- semantic `InventoryUiState` values;
- stable-address `InventorySlotIntent` values;
- the write-only `IInventoryView.Render` port; and
- `InventoryPresenter`, which is the only presentation-layer object allowed to send
  mutations to an `InventoryAggregate`.

Renderer packages implement `IInventoryView` and translate semantic view models and
intents into their own controls. The dependency direction is:

```text
Inventory Core <- Inventory Presentation <- renderer adapter
```

## Minimal use

```csharp
using Lingkyn.Inventory.Core;
using Lingkyn.Inventory.Presentation;

IInventoryView view = CreateRendererSpecificView();
using var presenter = new InventoryPresenter(inventoryAggregate, view);
presenter.Select(new SlotAddress(new ContainerId("backpack"), 0));
```

Install a renderer adapter such as `com.lingkyn.inventory.ugui` for a concrete view.
This package deliberately ships no visual assets, input routing, Canvas, panel, scene,
or device behavior.

## Git installation

For Git evaluation, explicitly pin Core and Presentation to the same full
repository commit SHA. Package manifest dependency versions express compatibility;
they cannot fetch sibling Git packages from this monorepo automatically.

Import **Basic Inventory Presentation** from Package Manager for a domain-only
presenter example that records a view model without choosing a renderer.
