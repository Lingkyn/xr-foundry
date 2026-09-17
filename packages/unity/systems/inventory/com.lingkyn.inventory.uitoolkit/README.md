# Lingkyn Inventory UI Toolkit

Status: **incubating**. This package is a renderer adapter for
`com.lingkyn.inventory.presentation`; it does not own Inventory domain or
presentation policy.

`InventoryDocumentView` binds an actual `UIDocument`/`VisualElement` tree to
immutable `InventoryViewModel` state. UI Toolkit buttons emit
`InventorySlotIntent` values containing both the stable Core `SlotAddress` and
the transient display index. The adapter does not mutate an Inventory aggregate.

The shipped `InventoryDocument.uxml` and `InventoryDocument.uss` provide a
functional empty/partial/full/rejected/selected/disabled/loading/error surface,
a bounded `ScrollView`, runtime slot buttons, details, and a primary action. A
consumer can replace the document while preserving the named-element contract
defined by `InventoryDocumentContract`.

## Public surface

- `InventoryDocumentView` (`MonoBehaviour`, `IInventoryView`): `Bind`, `Render`,
  `SetInteractionEnabled`, `TrySelect`, `TryActivate`, `ApplySkin`, and the
  `ActivationRequested` / `SelectionRequested` intent events.
- `InventoryDocumentContract`: the named-element contract a replacement document
  must satisfy.
- `InventoryUiToolkitSkin` (`ScriptableObject`): the single injectable skin seam
  that maps the shared UI design language tokens
  (`docs/standards/design-language/`). `CreateDefault()` and the
  `XR Foundry/Inventory/UI Toolkit Skin` asset menu both produce the canonical
  values, so a skin with no edits already matches the shared library look.

## Injecting a skin

Assign a skin asset to the view's `Skin` field in the Inspector, or inject one
explicitly from consumer code:

```csharp
var skin = InventoryUiToolkitSkin.CreateDefault(); // or a saved asset
skin.Accent = new Color(0.8f, 0.4f, 0.1f);
view.ApplySkin(skin);   // restyles bound elements and every slot rendered later
view.ApplySkin(null);   // clears the seam and returns to the stylesheet look
```

`ApplySkin` writes the token values as inline styles on the bound root, grid,
message label, primary action, and each slot button, so the shipped
`InventoryDocument.uss` selectors keep working underneath and a replacement
document only has to honor the named-element contract. When no skin is
injected the seam writes nothing and the stylesheet alone drives the look,
exactly as before the seam existed.

| Token | Element | Style written |
| --- | --- | --- |
| `surface.panel` | `inventory-root` | `background-color` |
| `surface.section` | `inventory-grid` | `background-color` |
| `surface.accent` | `inventory-primary-action` | `background-color` |
| `text.primary` | `inventory-root`, occupied slots, `inventory-primary-action` | `color` |
| `text.muted` | `inventory-message`, empty slots | `color` |
| `slot_states.normal` / `hover` / `selected` | slot buttons by state | `background-color` |
| `slot_states.disabled` | disabled slots | RGB as `background-color`, alpha as `opacity` |

Inline values outrank USS `:hover` and modifier selectors, so while a skin is
present the view resolves the slot state itself (disabled, then selected, then
hover, then normal) from the interaction gate, the selected class, and pointer
enter/leave. Borders, radii, sizes, and the state badge stay stylesheet-owned.
The skin carries no font: the design language leaves the font slot to the
consumer, and the shipped stylesheet declares none.

This package has no XR Interaction Toolkit dependency and makes no world-space,
headset, controller, comfort, or device claim. Use
`com.lingkyn.inventory.xr.uitoolkit` for the optional XRI world-space
composition.

The State Gallery sample creates a screen-space `UIDocument` explicitly from a
menu command and replays all neutral states. It does not install scenes or
global input objects automatically.

## Git installation

For Git evaluation, explicitly pin Core, Presentation, and UI Toolkit to the same
full repository commit SHA. Package manifest dependency versions express
compatibility; they cannot fetch sibling Git packages from this monorepo
automatically.
