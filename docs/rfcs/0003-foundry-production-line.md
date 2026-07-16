# RFC 0003: Foundry V1 production line

Status: accepted for bounded implementation by the maintainer.

Public deliberation: <https://github.com/Lingkyn/xr-foundry/discussions/51>

Implementation checkpoint: <https://github.com/Lingkyn/xr-foundry/issues/52>

## Decision

XR Foundry uses one public, engine-extensible production lifecycle:

```text
proposal -> source_gate -> admitted_blueprint -> scaffold -> build
-> fast_validation -> exact_consumer_validation -> independent_review
-> maturity_promotion -> release -> observation
```

Every transition is an evidence gate. A later stage can consume evidence from an
earlier stage, but cannot silently increase its claim strength. In particular:

- a scaffold is deliberately incomplete and is not a catalog package;
- a fast validation result cannot promote or release a package;
- automated Editor evidence cannot prove headset behavior;
- one renderer, dependency, engine, input, runtime, or device tuple cannot donate
  evidence to another tuple;
- discussion, Agent output, popularity, or a passing example does not grant
  admission, maturity, release, or repository authority.

## First batch

The first batch is the repository's existing nine implemented Unity packages:

- two foundations: Project Initializer and XR Baseline;
- seven Inventory modules: Core, Unity authoring, Presentation, UGUI, UI Toolkit,
  XR UGUI, and XR UI Toolkit.

They are published at their recorded `incubating` maturity. The batch is useful
for immutable Git installation, evaluation, and raw-material generation, but it
does not claim candidate/stable maturity or unrecorded device support.

## Package admission

A Unity blueprint can be written only when it is `admitted`, names a public
implementation Issue, cites positive public sources, uses a safe canonical path,
and preserves the no-authority constants. The scaffolder is dry-run first,
deterministic, and collision-refusing. Generated staging tests fail intentionally
until real implementation replaces the scaffold marker.

The scaffolder never changes `package-catalog.json`, `reference-catalog.json`, a
batch, a compatibility profile, maturity, or a release. Those changes require a
separate reviewed checkpoint with exact validation evidence.

## Validation levels

1. `fast_structure`: schemas, paths, catalogs, package manifests, batch membership,
   and deterministic scaffold rules. It is iteration feedback only.
2. `repository_contract`: the complete Python contract suite and public-safety
   checks. It is required for integration.
3. `exact_consumer`: immutable Git resolution, dependency lock, compile, and the
   applicable Unity tests for one exact tuple. It may support only the recorded
   automated claims.
4. `named_device`: a Device Lab receipt for one exact artifact, runtime, renderer,
   input route, and device. It alone may support the corresponding device claims.

## Continuity and next work

The queue contains source-gate proposals rather than empty package directories.
Each proposal becomes an independent public checkpoint. A stopped Agent leaves a
reachable commit and continuation receipt; a replacement Agent may revise the
plan when evidence contradicts it.

## Authority and privacy

No workflow, scaffold, task claim, review, credit record, or Agent can grant
itself write, merge, release, maturity-promotion, or device-acceptance authority.
Public files contain generic reusable material only; consumer identities, private
systems, credentials, local paths, and private reasoning transcripts are excluded.

