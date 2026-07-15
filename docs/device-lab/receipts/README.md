# Device receipts

Generic execution receipts belong here after an actual run and review. Start from
[`../device-receipt.template.json`](../device-receipt.template.json), replace every
placeholder, and keep the result bound to one full commit SHA, artifact digest/ref,
application ID, package tuple, device profile, capability plan, and environment.

The blank template is intentionally stored outside this directory. Do not commit a
`not_tested` file here as if it were evidence. Preserve failed, blocked, and
inconclusive verdicts; a later run gets a new receipt ID.

Historical package-specific receipts remain in their original validation location
and schema. Do not copy them here or rewrite them as generic V1 evidence.
