# Localization package-family standard

Status: proposal in the source-gate queue (`NEXT-LOCALIZATION`); Core implementation
staged in [`staging/localization/`](../../../staging/localization/README.md)

This standard defines reusable localization mechanics: stable message identity,
locale identity and fallback, message formatting with plural and select branches,
and content validation. It does not define any product's strings, tone, fonts, or
translation workflow. It is derived only from the positive public sources in
[`source-manifest.json`](source-manifest.json).

## Capability boundary

The family separates:

- immutable message identity (`MessageId`) from translated content;
- BCP 47 locale identity (`LocaleId`, restricted to language, script, region) from
  the runtime's current-locale selection;
- RFC 4647 lookup fallback (requested locale chain, then default locale chain, then
  root) from any product's fallback preference;
- an ICU MessageFormat subset (placeholders, `plural`, `select`, `#`, apostrophe
  quoting) from a full ICU implementation;
- CLDR cardinal plural categories for a declared set of languages from a claim to
  cover every language; an undeclared language is reported, never guessed; and
- content validation with stable codes (`message.missing`, `message.extra`,
  `placeholder.mismatch`, `plural.category.missing`, `plural.category.unused`,
  `plural.rules.unknown`) from any editor or pipeline UI.

The family does not own fonts, text rendering, bidirectional layout, date, number,
or currency formatting with locale digits, ordinal rules, gender inflection beyond
`select`, machine translation, or asset loading.

## Planned package boundary

| Layer | Owns | Must not own |
| --- | --- | --- |
| Engine-light Core (`com.lingkyn.localization.core`, staged) | identity, fallback chains, template parsing and formatting, plural rules, tables, catalog resolution, validation, structured results | Unity types, asset loading, UI, culture-specific number rendering, product content |
| Unity adapter (`com.lingkyn.localization.unity`, not started) | ScriptableObject string tables, locale selection from `SystemLanguage` or settings, a bridge to the Unity Localization package's tables when present | domain message identity, fallback policy, validation rules |

## Evidence boundary

An EditMode test can prove parsing, formatting, fallback, and validation for the
declared language set. It cannot prove correct rendering of any script, right-to-left
layout, font coverage, or the behavior of Unity Localization's runtime, and it does
not make the plural rules complete: CLDR rules for languages outside the declared
set, and the newer `many` category for large numbers in French, Spanish, Italian, and
Portuguese, are not implemented.

## Lessons register dispositions (to be added to the register when the family goes live)

| Lesson | Disposition | Rationale |
| --- | --- | --- |
| LESSON-001 closed classification vs open tags | adopted | Plural categories and argument kinds are closed enums; message ids are open identity |
| LESSON-002 migration path | deferred | No release exists yet; the first release records its compatibility policy |
| LESSON-003 single-workstation gate | adopted | Tests run in `run_unity_gates.py` and the consumer workflow like every family |
| LESSON-004 by-name resolution fails closed | adopted | The Core resolves nothing by reflection; the Unity adapter must report a missing Unity Localization package explicitly |
| LESSON-005 clause coverage | adopted | `coverage-map.json` maps every Core clause to named tests |
| LESSON-006 sibling pinning | deferred | Applies at the first Git-consumer validation |
| LESSON-007 skin seam | not_applicable | The family renders nothing |
| LESSON-008 version bump is a verification claim | adopted | The staged package stays at 0.1.0 until its first verified profile |

See also:

- [`verification-contract.md`](verification-contract.md)
- [`coverage-map.json`](coverage-map.json)
- [`admission.draft.json`](admission.draft.json) and [`blueprint.draft.json`](blueprint.draft.json)
