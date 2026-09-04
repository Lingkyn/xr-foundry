# Reference-system binding consumer

This directory is the source template for the consumer-owned runtime bindings in
`../foundry.project.json`. It is deliberately not opened by Unity in place.
Materialize it into a clean temporary project before compiling or running tests so
generated `Library`, `Logs`, and `ProjectSettings` state cannot contaminate the
repository validator.

```bash
python scripts/materialize_reference_consumer.py --output /private/tmp/xr-foundry-reference-consumer
```

The first consumer slice exercises only the packages needed by the three
cross-family binding endpoints. It does not prove the complete 13-component XR
composition or any headset behavior. The exact editor tuple for this experiment is
Unity `6000.3.19f1` on macOS Editor with the Null graphics device.

The harness contains three production adapter paths:

- semantic interaction events to Inventory selection intents;
- Inventory snapshots to the persistence contract and codec; and
- scoped Settings changes to interaction policy.

Its tests use synthetic semantic input and a recording presentation view. Unless a
test explicitly selects `LocalFileSaveStore`, storage is in memory. A passing suite
includes one real temporary-filesystem backup-recovery path, but does not imply
general filesystem durability, controller, renderer, player, or device proof.
The composition root must load a Settings snapshot before initializing the policy
adapter; automatic load-after-initialize resynchronization is not implemented.

Unity Test Framework can return success when no tests ran. Every EditMode and
PlayMode result must therefore pass `scripts/verify_unity_test_results.py` in
addition to the Unity process exit code. The caller must use a result path that did
not exist before the run, record the Unix epoch immediately before launching
Unity, and supply that boundary together with the source-audited exact test count
and Assembly name. For this consumer, those totals are 23 EditMode tests and 3
PlayMode tests; changing the test inventory requires changing the expected count,
not learning it from the result under test.
