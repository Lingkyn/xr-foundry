# Unity reference system composition

This directory contains the first XFCM composition root:

- `foundry.project.json` declares fixed components, variant choices, required
  capabilities, and cross-family binding boundaries.
- `foundry.lock.json` pins the deterministic structural resolution, including
  component manifest hashes and dependency order.

The reference selects the Inventory UGUI renderer and XR UGUI surface. UI Toolkit
and XR UI Toolkit remain first-class alternatives in `component-catalog.json`; they
are not cumulative dependencies.

Check the lock with:

```bash
python scripts/compose_system.py --check --json
```

The current lock proves structural resolution only. Its three cross-family
bindings are pending consumer-owned adapters, so `runtime_ready` is `false`.
Neither this directory nor a passing resolver claims a clean Unity compile,
runtime integration, headset behavior, or named-device evidence.
