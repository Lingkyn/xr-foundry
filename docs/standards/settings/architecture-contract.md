# Player Settings and Accessibility architecture contract

## Invariants

1. A stable `SettingKey` identifies meaning; a UI label, control path, scene
   object, or localized string is never the identity.
2. Every registered definition declares one value kind, default, scope, validation
   constraint, and application policy. Stored values cannot redefine a setting.
3. A candidate state is validated as a whole before any applicator runs, so
   cross-setting constraints observe the same deterministic snapshot.
4. Apply is transactional: applicators run in stable key order; a failure rolls
   back already-applied effects in reverse order; no change event or persistence
   request is emitted until the apply completes.
5. Cancel discards staging only. Reset stages declared defaults for the selected
   scope; it does not silently erase unrelated scopes or unknown stored data.
6. Profiles are ordered override layers over definitions, not mutable global
   assets. Duplicate keys within one layer fail closed.
7. Durable storage and migration belong to a replaceable persistence seam. A
   settings snapshot contains typed meaning; a Persistence-family adapter may
   envelope, migrate, verify and store it. Core imports neither Persistence nor
   any path, file, PlayerPrefs, JSON, cloud, or platform storage policy.
8. Accessibility metadata makes settings discoverable. It never claims that a
   feature exists, meets a guideline, passes certification, or is suitable for a
   particular player or device.
9. Renderers read neutral view data and issue commands. UGUI, UI Toolkit, XR UI,
   input rebinding, localization, and device adapters are sibling consumers.
10. ScriptableObject assets author immutable definitions and presets; runtime
    player choices live in Core snapshots and persistence providers.

## Core concepts

| Concept | Responsibility |
| --- | --- |
| `SettingKey` | Validated stable string identity. |
| `SettingValue` | Closed typed value union: Boolean, Integer, Float, String, or Option ID. |
| `SettingDefinition` | Kind, default, scope, numeric/options constraint, application order, restart policy, and metadata. |
| `AccessibilityMetadata` | Optional category, feature ID, discoverability text keys, preview support, and non-claim status. |
| `SettingsRegistry` | Immutable unique definitions plus deterministic lookup/order. |
| `SettingsProfile` | Named, ordered override layer validated against the registry. |
| `SettingsSnapshot` | Immutable known values plus preserved unknown stored values and revision metadata. |
| `SettingsTransaction` | Stages set/reset/profile commands without mutating committed state. |
| `ISettingsConstraint` | Validates the complete candidate snapshot, including cross-setting rules. |
| `ISettingApplicator` | Applies and rolls back one or more setting effects without owning persistence. |
| `ISettingsSnapshotRepository` | Replaceable typed load/save seam implemented by a persistence adapter. |
| `SettingsCoordinator` | Validates, applies, rolls back, commits, emits deterministic changes, and requests persistence. |

## Value and scope model

The first Core uses explicit value kinds instead of `object` or arbitrary generic
payloads. Numeric definitions may declare inclusive min/max/step; option values
must be stable IDs from an explicit allowed set. String settings may declare a
maximum length. Validation rejects kind mismatch, non-finite floats, invalid
ranges, unknown options, duplicate definitions, and invalid defaults.

Scopes are `Global`, `User`, `Profile`, and `Session`. A consumer chooses which
scopes it persists. Reset operates on a named scope. A session override never
silently overwrites a durable user value.

## Transaction and failure flow

1. Freeze committed state and open a transaction at its revision.
2. Stage set, reset, or ordered profile-layer commands.
3. Reject stale transactions if the committed revision changed.
4. Materialize one candidate snapshot and validate definitions plus all
   cross-setting constraints.
5. Compute a stable key-sorted change set.
6. Run matching applicators in declared order while recording rollback tokens.
7. On failure, roll back in reverse order and return both the original failure and
   any rollback diagnostics; committed state and notifications remain unchanged.
8. On success, atomically replace the in-memory snapshot, emit the change set,
   then call the optional persistence seam. A persistence failure is reported as
   `applied_not_persisted`; it does not pretend the already-applied effects were
   rolled back.

## Accessibility metadata categories

The vocabulary includes `text`, `contrast`, `audio`, `captions`, `input`,
`navigation`, `motion`, `comfort`, `photosensitivity`, `difficulty`, and
`discoverability`. Definitions opt in only when relevant. Metadata may expose a
localization title/description key, preview support, whether the feature should be
available before gameplay, and a public documentation key.

The vocabulary is extensible through stable string feature IDs. It does not embed
Microsoft, platform-store, legal, medical, or certification labels as universal
truth. A product maps and verifies platform-specific claims separately.

`Comfort` is a discoverability category for product-defined motion or immersive
preferences. Apple visionOS and Microsoft guidance establish that motion choices
and alternatives can matter; they do not establish a universal XR comfort value,
locomotion policy, medical outcome, or support for any named headset.

## Persistence and interaction boundaries

The snapshot repository is a port, not a second save system. It accepts and
returns typed `SettingsSnapshot` data. A separate adapter may translate that data
to the Persistence family's codec/envelope/store contracts. Persistence owns byte
format, migration, integrity, commit, backup and recovery. Settings owns key
meaning, defaults, scopes and validation. Neither family depends on the other at
Core level.

Semantic Interaction owns action identity, physical bindings and rebinding. A
Settings definition may expose a policy such as hold/toggle preference or input
assistance metadata, but it cannot contain a control path or mutate an action map.

## Unity adapter

The Unity package may provide definition, catalog, profile, numeric-constraint,
option-list, and accessibility-metadata ScriptableObjects or serializable records;
deterministic Core conversion; actionable Editor validation; and a factory that
accepts consumer-provided applicators and persistence seams.

It must not store runtime player choices in authored assets, search scenes for
applicators, call `PlayerPrefs`, hard-code QualitySettings/AudioMixer/Input System
behavior, depend on a renderer, or infer device support.
