# Persistence verification contract

## Source gate

- Every derivation input is positive, public, traceable and role-bounded in the
  source manifest.
- Consumer code and context-only materials are absent from derivation inputs.
- License/terms, maintenance evidence, admitted claims and excluded uses are
  explicit.
- Independent review confirms the sources support the proposed boundary before a
  package ID or directory is admitted.

## Core gate

Deterministic tests must cover:

- same-version round trip and byte-level envelope bounds;
- ordered multi-step migration;
- missing, duplicate, ambiguous, cyclic and non-monotonic migration edges;
- rejection of future schema versions;
- corrupted payload, incorrect length and incorrect digest;
- codec, integrity, read, write, flush and commit failures as different results;
- cancellation before commit;
- failed-save preservation of the prior committed record;
- load candidate immutability until consumer validation succeeds;
- recovery selection between primary and backup without silently accepting staging
  files; and
- deterministic post-commit event ordering.

## Unity adapter gate

- EditMode tests validate ScriptableObject configuration, schema IDs, migration
  graphs, slot/path safety and supported DTO shapes.
- File-provider tests use a temporary directory and inject failures at stage,
  flush, backup and replace boundaries.
- Capability tests prove `atomic_replace` is reported only for the exact provider
  path that used the supported replacement operation.
- JsonUtility tests record its concrete supported DTO tuple; another codec receives
  separate evidence.
- No test writes to a real user's production save directory.

## Exact-consumer gate

- Install packages from an immutable full commit SHA and exact subfolder path.
- Record Unity version, OS, scripting/API profile, package lock digest, package
  versions, testable assemblies and test results.
- Compile both Core and Unity adapter assemblies in a clean project.
- Run all applicable package tests and preserve machine-readable results.
- Record upgrade/rollback behavior only after a previous immutable release exists.

## Claim ceiling

The first Windows Editor consumer can prove only that tuple. It cannot establish
Android, iOS, WebGL, tvOS, console, cloud, security, crash-durability or XR device
behavior. A checksum is not authentication; a backup is not cloud sync; a successful
write is not automatically an atomic commit.
