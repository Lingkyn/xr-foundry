# Inventory Evidence Coverage Matrix

This matrix records convergence. A pattern becomes a core requirement only when it
is supported by multiple positive sources or by one normative engine contract plus
a professional implementation benchmark.

| Capability | Positive sources | Standard deduction |
| --- | --- | --- |
| Definition and runtime instance identities | `epic-lyra-inventory`, `unity-economy-player-inventory`, `opsive-ultimate-inventory` | Separate stable definition IDs from unique instance IDs |
| Inventory and equipment separation | `epic-lyra-inventory`, `more-mountains-inventory-engine`, `opsive-ultimate-inventory` | Equipment is an optional neighboring lifecycle, not the Inventory core |
| Multiple inventories and containers | `more-mountains-inventory-engine`, `opsive-ultimate-inventory` | Model owned aggregates with one or more policy-bearing containers |
| Stacks, unique items, and mutable state | `epic-lyra-inventory`, `opsive-ultimate-inventory`, `unity-economy-player-inventory` | Stack compatibility and mutable instance state are explicit |
| Extensible item capabilities | `epic-lyra-inventory`, `game-creator-inventory`, `opsive-ultimate-inventory` | Use typed optional capabilities/fragments and bounded integrations |
| Restrictions and placement | `opsive-ultimate-inventory`, `game-creator-inventory`, `more-mountains-inventory-engine` | Capacity, category/tag, slot, shape, and custom rules are composable policies |
| Structured mutation and concurrency | `unity-economy-player-inventory`, `opsive-ultimate-inventory` | Mutations return structured results; authority adapters surface conflicts |
| Save/load integration | `opsive-ultimate-inventory`, `more-mountains-inventory-engine`, `unity-economy-player-inventory` | Core owns versioned state contracts while storage remains an adapter |
| Domain and presentation separation | `more-mountains-inventory-engine`, `unity-scriptableobject-architecture`, `epic-lyra-inventory` | UI consumes snapshots/view models and sends commands |
| Nested reusable presentation parts | `unity-nested-prefabs`, `unity-scriptableobject-architecture`, `more-mountains-inventory-engine` | Panel, grid, slot, item, details, and action views are independent nested prefabs |
| Package shape and tests | `unity-custom-packages`, `unity-package-tests` | Every package has conventional layout, assemblies, tests, samples, docs, and changelog |
| Optional XR world-space presentation and device acceptance | `unity-xri`, `unity-xri-examples`, `pico-unity-project-validation`, `pico-unity-controller-ray-canvas`, `pico-unity-build-run`, `pico-unity-input-mapping`, `pico-unity-openxr-release-notes` | XR belongs in a separate adapter using supported tracked-device UI paths; configuration and automated routing are prerequisites, while named-device installation, interaction, readability, anchoring, and comfort remain a separate evidence gate |

## Deliberate synthesis

The standard does not copy one product's taxonomy. It takes the common contracts
above and chooses the smallest neutral model that preserves them. Features that
appear in only one benchmark remain optional extensions until additional positive
evidence or real consumer demand justifies promotion.

The source set contains no consumer project, course project, internal prototype,
or screened-out repository. Those materials cannot change this matrix.
