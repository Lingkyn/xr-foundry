# Device receipts

Generic execution receipts belong here after an actual run and review. Start from
[`../device-receipt.template.json`](../device-receipt.template.json), replace every
placeholder, and keep the result bound to one public-origin-reachable full commit
SHA, materialized repository artifact path/digest, application ID, compatibility
profile, package tuple, real Unity manifest and resolved dependency-lock digests,
versions and reachable edges, build
tuple, device profile, capability plan, named input sources, posture, measured
duration, and environment.

The blank template is intentionally stored outside this directory. Do not commit a
`not_tested` file here as if it were evidence. Preserve failed, blocked, and
inconclusive verdicts; a later run gets a new receipt ID.

Validate a completed file with
`python scripts/validate_repository.py --device-lab-receipt <receipt-path> --json`.
This directory is the only active execution-receipt surface; capability-specific
requirements come from the selected test plan rather than a second receipt schema.
