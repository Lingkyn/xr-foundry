# Inventory UGUI local-consumer validation — 2026-07-15

> **Historical only:** this pre-correction run did not prove functional shipped
> prefab bindings. Current evidence is the
> [`0.1.1` corrective candidate receipt](2026-07-15-inventory-ugui-0.1.1-candidate.md).

## Historical local verdict

The local Inventory UGUI slice passes its incubating presentation boundary. The
presenter alone owns aggregate mutation, view models are read-only, every required
UI state can be replayed, pointer and keyboard submit share the same standard
EventSystem path, and the shipped role assets retain real nested prefab links.

This is not yet candidate evidence. A Git-pinned consumer with unrelated XR packages
removed remains required. No world-space, controller, headset, comfort, or Pico
claim is made.

## Composition evidence

Separate shipped prefab assets exist for `InventoryShell`, `InventoryPanel`,
`InventoryGrid`, `InventorySlot`, `ItemView`, `ItemDetails`, and `ActionMenu`.
`InventorySlotCompact` is a real prefab variant. Editor tests verify source links
through Shell -> Panel -> Grid -> Slot -> Item and independent slot replacement.

The first factory attempt correctly failed because multiple serializable
`MonoBehaviour` classes shared a non-matching script file and produced a missing
script in the saved prefab. Each view was then moved into its own same-named script;
the rebuilt prefab chain and tests passed. Compilation alone would not have caught
that defect.

## Consumer and evidence

- Unity: `6000.3.19f1`
- EditMode: 31 passed, 0 failed; UGUI editor tests: 4 passed
- PlayMode: 1 passed, 0 failed
- EditMode result/log SHA-256: `B92441956BA79605980EFE5FA2BDE7E575EA6DC5423D647962ED62335F2A52DD` / `BEF89EE607B1FA51C3C18277DC61674ADF3641664E10DC92CA7E3EED11C3B89E`
- PlayMode result/log SHA-256: `96C074C86575B8D8A871E2F31B03A1A313DDE60C6313B2857657B9A2C0E49546` / `C05A54CDD233F8F2D8642EEF52136881F5AF03ECAACBDE5F5A2D272D346B66B9`

## Claim boundary

The earliest failed UGUI gate is `non_xr_immutable_git_consumer`. XR world-space
placement and device evidence remain separate Issue #9 work.
