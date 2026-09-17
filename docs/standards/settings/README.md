# Player Settings and Accessibility family standard

This standard defines renderer-neutral player-setting semantics and a thin Unity
authoring/application adapter. It is deliberately not a settings screen, storage
provider, input rebinding implementation, localization catalog, platform SDK, or
accessibility certification system.

## Package boundary

| Planned layer | Owns | Does not own |
| --- | --- | --- |
| Engine-light Core | Typed keys and values, definitions, defaults, scopes, constraints, profiles, staged transactions, deterministic change sets, rollback orchestration, snapshots, persistence seam, accessibility discoverability metadata | File I/O, Unity objects, UI rendering, input bindings, localized strings, platform settings |
| Unity adapter | ScriptableObject definition/profile authoring, deterministic Core conversion, catalog validation, and replaceable Unity applicator registration | UGUI, UI Toolkit, concrete game graphics/audio/input policy, mutable player state in assets |

Accessibility categories describe discoverability and intended player-facing
effect. A category or tag never proves that a feature is implemented, usable,
legally compliant, platform-certified, or verified on a device.

`AccessibilityMetadata.Category` and `FeatureId` are open metadata: free strings
with no admitted set, so two consumers may spell the same category differently and
no consumer should group or filter on them as if they were a closed taxonomy.
Whether an admitted category set belongs in Core as a validated identity or in a
profile document is an open decision recorded as `LESSON-001` in the
[consumer lessons register](../lessons/README.md).

No package identifier is reserved until the source gate receives an independent
PASS and an admitted blueprint is committed. Read the [architecture contract](architecture-contract.md),
[verification contract](verification-contract.md), and
[positive-source manifest](source-manifest.json) before implementation or reuse.

See also:

- [`api-surface.md`](api-surface.md) (public API inventory for the compatibility review)

## Coverage record

[`coverage-map.json`](coverage-map.json) maps every clause of the Core and Unity
adapter gates in the verification contract to named tests and lists the tests still
missing. An unmapped or partial clause is an open gap, not implied coverage, and the
map is not execution evidence.
