# Localization verification contract

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, private string table, or prior product localization is a
  derivation input.
- A person confirms the source URLs and versions before the admission record is
  signed, because the authoring environment could not fetch them.

## Core gate

Deterministic tests must cover:

- locale tag parsing, canonical casing, rejection of unsupported subtag shapes, and
  the parent chain down to root;
- message identity validation;
- template parsing of placeholders, plural, select, exact selectors, the number
  sign, and apostrophe quoting, with a stable failure for every malformed shape and
  for conflicting argument kinds;
- formatting of text and number arguments;
- plural selection for the declared language set, including a one-and-other
  language, a zero-as-one language, a one-few-many-other language, a six-category
  language, and an other-only language;
- select branches with fallback to other;
- missing-argument, argument-type, and unknown-plural-rules failures as distinct
  results, with the other-only exemption;
- table immutability and rejection of duplicate or malformed messages;
- catalog construction requiring the default locale and unique locales;
- the fallback chain: requested chain, then default chain, then root, de-duplicated;
- resolution that reports the resolved locale, whether fallback happened, and the
  chain tried, and a missing message that names the chain;
- formatting through the catalog with the plural rules of the resolved locale; and
- validation codes for missing and extra messages, placeholder mismatch, missing and
  unused plural categories, unknown plural rules, and a missing source table, plus a
  consistent catalog passing clean.

## Unity adapter gate

EditMode tests must cover:

- conversion of a table asset to a domain table without mutating the asset;
- table authoring validation with stable codes, field paths, and the source asset
  for an invalid locale, an invalid id, a duplicate id, and a malformed template,
  and conversion that throws with the same report;
- catalog authoring validation for a missing table reference, a duplicate locale,
  an invalid default locale, and a default locale without a table;
- conversion of a valid catalog asset that resolves with fallback and formats with
  the resolved locale's plural rules;
- an explicit `SystemLanguage` map that fails closed for `Unknown`;
- a runtime that raises `LocaleChanged` once per actual change, formats in the new
  locale, and reports direct-table and fallback state; and
- when a bridge to the Unity Localization package exists, an explicit diagnostic
  for the package's absence rather than silence (LESSON-004).

No test claims rendering, font, or right-to-left behavior.

## Claim ceiling

An EditMode run proves parsing, formatting, fallback, and validation for the tuple
recorded in the compatibility profile. It does not prove script rendering, layout,
font coverage, CLDR completeness beyond the declared languages, or the engine
package's runtime behavior.
