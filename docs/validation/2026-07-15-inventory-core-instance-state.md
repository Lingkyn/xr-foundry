# Inventory Core typed instance-state validation — 2026-07-15

## Verdict

The typed mutable instance-state slice passes its current Core boundary. Unique
items can carry validated schema-versioned fragments, update or remove them through
the aggregate's atomic mutation path, move them between containers, persist and
restore them, and normalize supported older fragment schemas.

The design deliberately rejects a universal dictionary of arbitrary objects.
Consumers register `ItemStateFragmentCodec<T>` implementations; Core stores only an
immutable type ID, schema version, and codec-owned payload after validation.

This receipt does not promote the package to candidate. Public API compatibility
review and upgrade/rollback evidence remain pending. It makes no Unity authoring,
UGUI, XR, controller, headset, or Pico claim.

## Consumer and toolchain

- Unity: `6000.3.19f1`
- Test platform: EditMode, batch mode
- Consumer: the existing persistent clean Unity smoke project outside this repository
- Package source: local file dependency for this implementation slice; immutable Git
  URL evidence must be refreshed after the reviewed revision is merged

## Evidence

- Repository validator: pass
- Repository Python tests: 9 passed
- Unity test run: 23 passed, 0 failed
- Inventory Core Unity tests: 21 passed, 0 failed
- Result SHA-256:
  `1EB4A6F6C506A60ECDA30143D3C31426C31D9CE25F3867894D72EEFDEF10F1D5`
- Log SHA-256:
  `8E887F04DC1FC33C3FAB5E5A970E5C19BD5FE68A8EABC4BCA252B32F5849E8A4`

Covered behavior includes typed codec creation/read, atomic state replacement and
removal, transfer preservation, persistence round trip, invalid-payload rollback,
unregistered-codec rejection, fungible-state rejection, and fragment schema
normalization from version 1 to version 2.

## Claim boundary

Passing this receipt satisfies `typed_instance_state`. The earliest failed Core
gate advances to `public_api_compatibility_review`; no later package-family gate is
inferred from this result.
