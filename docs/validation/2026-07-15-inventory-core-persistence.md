# Inventory Core persistence validation — 2026-07-15

## Verdict

The provider-neutral persistence slice passes its current Core boundary. A complete
Inventory state can round-trip through a versioned envelope, a prior schema can be
migrated deterministically, and every failed restore leaves the live aggregate and
revision unchanged.

This receipt does not promote the package to candidate. Typed mutable instance
state, public API compatibility, and upgrade/rollback evidence across a released
revision remain pending. It makes no Unity authoring, UGUI, XR, controller, headset,
or Pico claim.

## Consumer and toolchain

- Unity: `6000.3.19f1`
- Test platform: EditMode, batch mode
- Consumer: a persistent clean Unity smoke project outside this repository
- Package source: local file dependency to this checkout for the implementation
  slice; immutable Git URL evidence remains the earlier baseline receipt and must
  be refreshed after a reviewed revision exists

The persistent smoke consumer was reused. No additional copy of a product project
or consumer project was created.

## Evidence

- Repository validator: pass
- Repository Python tests: 9 passed
- Unity test run: 17 passed, 0 failed
- Inventory Core Unity tests: 15 passed, 0 failed
- Result SHA-256:
  `1809980EDCDA206A26B0F652E1B11585DBEDA9EA41546C86D84374D585C9F02C`
- Log SHA-256:
  `32074F249344FEE41AA74954A109B8897E0699CCAFA86D042FC66081C3230A72`

Covered behavior includes equivalent-state round trip, atomic failure, duplicate
unique-instance rejection, deterministic schema `1 -> 2` migration, missing
migration diagnostics, and post-commit restore notification with observer-fault
isolation.

## Claim boundary

Passing this receipt satisfies `persistence_round_trip_and_migration` only. The
earliest failed Core gate advances to
`typed_instance_state_and_api_compatibility`; no later package-family gate is
inferred from this result.
