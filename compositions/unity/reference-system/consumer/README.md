# Reference-system binding consumer

This directory is the source template for the consumer-owned runtime bindings in
`../foundry.project.json`. It is deliberately not opened by Unity in place.
Materialize it into a clean temporary project before compiling or running tests so
generated `Library`, `Logs`, and `ProjectSettings` state cannot contaminate the
repository validator.

```bash
python scripts/materialize_reference_consumer.py --output /private/tmp/xr-foundry-reference-consumer
```

The consumer exercises the packages needed by six typed composition paths. It
still does not prove the complete 13-component XR composition or any headset
behavior. The exact editor tuple for this experiment is
Unity `6000.3.19f1` on macOS Editor with the Null graphics device.

The harness contains six production adapter paths:

- Unity Input System observations to semantic interaction routing;
- semantic interaction events to Inventory selection intents;
- Inventory snapshots to the persistence contract and codec; and
- scoped Settings changes to interaction policy;
- Inventory presentation to the selected UGUI world-space XR surface; and
- Settings snapshots through the persistence contract and back into the
  Settings coordinator.

Its tests use deterministic raw Input System observations, synthetic semantic
input, recording views, and the shipped UGUI/XR world-space prefab. Unless a test
explicitly selects `LocalFileSaveStore`, storage is in memory. A passing suite
includes one real temporary-filesystem backup-recovery path and a real selected
renderer surface, but does not imply general filesystem durability, controller,
player, headset, or device proof.
The composition root must load a Settings snapshot before initializing the policy
adapter; automatic load-after-initialize resynchronization is not implemented.

Unity Test Framework can return success when no tests ran. Every EditMode and
PlayMode result must therefore pass `scripts/verify_unity_test_results.py` in
addition to the Unity process exit code. The caller must use a result path that did
not exist before the run, record the Unix epoch immediately before launching
Unity, and supply that boundary together with the source-audited exact test count
and Assembly name. Each assembly is verified separately; the current inventory
is:

| Assembly | Mode | Exact cases |
| --- | --- | ---: |
| `Lingkyn.Interaction.Core.Editor.Tests` | EditMode | 16 |
| `Lingkyn.Interaction.Unity.Editor.Tests` | EditMode | 17 |
| `Lingkyn.Inventory.Presentation.Editor.Tests` | EditMode | 5 |
| `Lingkyn.Settings.Core.Editor.Tests` | EditMode | 29 |
| `Lingkyn.Settings.Unity.Editor.Tests` | EditMode | 8 |
| `XRFoundry.ReferenceSystem.EditMode.Tests` | EditMode | 71 |
| `XRFoundry.ReferenceSystem.PlayMode.Tests` | PlayMode | 4 |

That is 146 EditMode cases plus 4 PlayMode cases, or 150 total. Changing the
test inventory requires changing the expected count from a source audit, not
learning it from the result under test. A passing Editor harness is not a player,
controller or headset claim.
