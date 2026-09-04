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

Unity Test Framework can return success when no tests ran. Every EditMode and
PlayMode result must therefore pass `scripts/verify_unity_test_results.py` in
addition to the Unity process exit code.
