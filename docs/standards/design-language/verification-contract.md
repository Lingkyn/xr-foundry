# UI design language verification contract

This contract is written the way [`locomotion/verification-contract.md`](../locomotion/verification-contract.md)
is: every clause names something that can actually be checked against a concrete
artifact, not an aesthetic judgment. This family carries neutral tokens, a state
model, and content/interaction principles — it carries no runtime code — so the
gates below are structural and reference-hierarchy checks against
[`ui-design-language-standard.json`](ui-design-language-standard.json) and
[`README.md`](README.md), not test results.

## Source gate

- Every derivation input is positive, public, and role-bounded in
  [`source-manifest.json`](source-manifest.json).
- No consumer project, product screenshot, prior product skin, or private brand
  asset is a derivation input; `context_only_not_derivation_inputs` in the manifest
  names the one downstream lesson that is context only, not a source.
- A person confirms the source URLs, exact page paths, and the reviewed-on dates of
  every platform guidance page before the admission record is signed, because the
  authoring environment could not fetch them.

## Reference hierarchy gate

- The reference hierarchy is explicit and unambiguous: `README.md`'s
  "Reference hierarchy" table and `ui-design-language-standard.json`'s
  `reference_hierarchy` object name exactly one **primary visual reference**
  (Apple visionOS) and exactly one **primary interaction reference pairing**
  (PICO and Meta Horizon OS together, as one interaction reference), for exactly
  two concerns: visual (surfaces, depth, layout, typography) and interaction
  (pointer, selection, sizing, states).
- A clause never cites a reference for a concern it does not own: no visual clause
  cites PICO or Meta Horizon OS as primary, and no interaction clause cites Apple
  visionOS as primary, matching the split rationale recorded in
  `source-manifest.json`.
- Every reference named in `reference_hierarchy.visual_primary.sources` or
  `reference_hierarchy.interaction_primary.sources` has a matching source `id` in
  `source-manifest.json`; a reference id present in one file and absent from the
  other is a drift defect.

## Token inventory gate

- Bijection between the standard's prose and its data: every token category the
  standard names in `README.md`'s "What the language defines" section (surface,
  accent, text, per-state slot colors, corner-radius scale, elevation, spacing,
  hit-target minimums) has a matching key under `design_tokens` in
  `ui-design-language-standard.json`, and every top-level key present under
  `design_tokens` in the JSON (`surface`, `text`, `slot_states`, `shape`,
  `elevation`, `spacing`, `hit_target_dp`, `motion`) is named somewhere in the
  README. A token named in prose with no JSON key, or a JSON key the README never
  names, is a drift defect, not a style choice.
- The same bijection applies to the state model (`state_model.states` in the JSON
  must equal the `normal / hover / selected / disabled` set the README states) and
  to the interaction sizing figures (the `hit_target_dp.minimum` and
  `hit_target_dp.primary_controls` values in the JSON must equal the `48 dp` /
  `60 dp` figures the README states).
- A canonical token value changes in one file only when it changes in the other in
  the same revision; the JSON is the machine-readable source of truth and the
  README is its human-readable description, never a second, independently
  editable copy of the values.

## Renderer adapter seam gate

- Every renderer adapter that claims to follow this standard maps the shared
  tokens through exactly one injectable skin/theme seam (for a UGUI adapter, a
  single `ScriptableObject` asset reference; for a UI Toolkit adapter, a single
  injectable style-source reference) and through no second, parallel place where a
  token value is set.
- The adapter ships a default skin instance populated with the canonical values
  recorded in `design_tokens`, so an un-themed install already matches the shared
  library look; a default skin that omits a token or substitutes a different value
  than the JSON records fails this gate.
- A consumer can replace the injected skin to restyle the adapter without forking a
  prefab, a stylesheet, or adapter code; the renderer-neutral presentation contract
  the adapter sits on carries no visual vocabulary of its own for the seam to
  duplicate.
- This standard defines no component, prefab, scene, or renderer-specific asset. It
  is expressed entirely as data (the token JSON), the state-model and
  content/interaction principles in prose, and the seam contract text above; any
  component, prefab, or scene that renders these tokens belongs to the family that
  consumes this standard (for example the XR UI shell renderer adapters or an
  Inventory presentation adapter), never to this standard itself.

## Claim ceiling

No clause in this standard, on its own, proves legibility, contrast, comfort, glare,
GPU or fill-rate cost, motion comfort, or any other perceived-quality outcome for a
person looking at or pointing at a rendered surface. Every claim of that kind
requires a Device Lab receipt that binds the observation to a full commit SHA, the
exact renderer, runtime, and device tuple, the posture, the measured duration, and
the tester identity for the exact composition that was looked at; `not_tested` is
never evidence, and a passing repository-contract run is never a legibility,
contrast, or comfort result.

This family ships no package and no test assembly: there is no `Runtime/`,
`Editor/`, or `Tests/` folder under `packages/unity` for it, and no compatibility
profile records an `editmode_tests` claim for it. Its manifest-and-contract
pairing is checked by repository validation
(`validate_family_source_manifests`); its Reference hierarchy, Token inventory,
and Renderer adapter seam clauses above are checked by human review of this
contract against `ui-design-language-standard.json`, `README.md`, and the
consuming renderer adapter's own code review — no Unity EditMode or PlayMode run
can verify a token value, a reference hierarchy, or a skin seam, and none is
offered as evidence for one.
