# XR Foundry production line

Foundry V1 turns positive public sources into reusable package families without
confusing a scaffold, one passing test, or one device with a released standard.
The machine contract is [`foundry-manifest.json`](foundry-manifest.json), and the
decision record is [RFC 0003](../rfcs/0003-foundry-production-line.md).

## Start here

1. Select one proposal from [`queue/next-batch.json`](queue/next-batch.json).
2. Complete the [cross-project system admission gate](system-admission.md).
   A project feature, taxonomy, scene, or one-off gameplay rule stays in its
   consumer repository even when it can technically be packaged.
3. Complete its positive-source gate. Do not use a consumer project, course
   project, internal prototype, or rejected candidate as derivation material.
   Answer every lesson in the
   [consumer lessons register](../standards/lessons/README.md) for the new family;
   repository validation requires a disposition per live family once the family's
   first component manifest exists.
4. Create a blueprint from
   [`unity-package-blueprint.example.json`](unity-package-blueprint.example.json).
5. Validate and preview it:

   ```text
   python scripts/scaffold_unity_package.py blueprint.json --output-root . --json
   ```

6. Author the family under `staging/<family>/` while its Unity evidence does not
   yet exist: sources, tests, samples, and a `package.staging.json` manifest that
   repository validation does not treat as a live `com.lingkyn.*` package. Staged
   material is in no catalog, batch, compatibility profile, or release. It moves
   into `packages/` only after admission and a verified compatibility profile.
   The queue's current staged example is
   [`staging/localization`](../../staging/localization/README.md).
7. Only an admitted blueprint may add `--write`. The target must not exist.
8. Replace the deliberate failing scaffold test with real implementation and
   focused tests (or the reviewed staged sources) before proposing catalog
   admission.
9. Run fast structure checks during iteration, then the complete repository and
   exact-consumer gates before promotion or release.
10. Use Device Lab separately for every claimed headset/input/renderer tuple.

The scaffolder never edits catalogs, batches, compatibility profiles, releases,
GitHub state, or maturity. Its output is a scaffold, not a finished package.

## Package batches

[`batches/batch-registry.v1.json`](batches/batch-registry.v1.json) is the current
batch index. It keeps the released
[`unity-first-batch`](batches/unity-first-batch.v1.json) and independently
validated Persistence, Settings, and Interaction
[`unity-next-systems`](batches/unity-next-systems.v1.json) immutable. Repository
validation requires the registered batch union to cover every live package
exactly once and rejects cross-batch duplication. A future building batch is not
a release.

The landing page groups packages by capability family; machine catalogs and batch
files keep each installable package separate because dependencies, versions,
evidence, and device boundaries differ.

## Validation levels

| Level | Purpose | Can promote or release? |
| --- | --- | --- |
| `fast_structure` | Schema, path, manifest, catalog, batch, and scaffold feedback | No |
| `repository_contract` | Full fail-closed repository integration | Required but not sufficient |
| `exact_consumer` | Immutable Git resolution, compile, and applicable Unity tests for one tuple | Candidate input only |
| `named_device` | Exact Device Lab artifact/runtime/input/device evidence | Only for matching device claims |

## Release boundary

Read [`release-policy.md`](release-policy.md). A batch release is an immutable
discovery surface; it does not raise the maturity of its packages. Package
maturity remains an independently reviewed, evidence-backed decision.
