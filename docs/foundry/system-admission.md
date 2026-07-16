# Cross-project system admission gate

XR Foundry is a standards-oriented, cross-project reference and package library.
It does not accept a feature merely because one game uses it or because its code
can be moved into a package.

This gate runs before the positive-source gate and before package identities or
directories are reserved. A proposal must first show that it represents a common
or standard system family whose reusable kernel can be derived from positive,
public, professional evidence.

## Admission question

> Is this a recurring cross-project capability with a source-supported common
> kernel, or is it one product's content, taxonomy, workflow, or gameplay rule?

An admitted family must satisfy all of the following:

1. **Recurring problem.** Name at least two materially different consumer
   contexts. A second scene in the same game is not a second consumer context.
2. **Positive evidence.** Include at least one official engine, platform, open
   standard, or other primary industry source and at least one independent
   professional, shipped, maintained, or widely adopted implementation.
3. **Common kernel.** Every mandatory Core capability is supported by multiple
   independent positive sources, or by one normative standard plus independent
   implementation evidence. Popularity is a signal, not authority by itself.
4. **Variation boundary.** Differences between games become configuration,
   policies, ports, adapters, renderer modules, samples, or extension seams.
5. **Project exclusion.** Product names, private assemblies, story/content data,
   scene hierarchy, art direction, economy tuning, one game's commands, and
   one-off rules stay outside the shared Core.
6. **Clean-consumer proof.** The evidence plan includes installation and tests in
   a consumer that does not contain the originating game.
7. **Provenance.** Sources, licenses, admitted claims, rejected alternatives, and
   non-claims are recorded before implementation.

Alignment with
[`game-system-reference-standards`](https://github.com/Lingkyn/game-system-reference-standards)
is useful evidence: `Common` or `Standard` strengthens admission; `Candidate`
requires the Foundry's own complete evidence; `project-specific` remains local.
That repository is a navigation and classification layer, not automatic package
or release authority.

## Outcomes

| Outcome | Meaning | Next route |
| --- | --- | --- |
| `admitted_cross_project_standard_candidate` | Generality and evidence threshold passed | Positive-source gate, then blueprint |
| `reference_only` | Useful comparison material, but not yet an installable standard | Reference catalog or discussion |
| `project_extension` | Valid for a specific game but not the shared kernel | Consumer repository |
| `rejected` | Duplicative, unsupported, unsafe, or too product-specific | Record rationale; do not scaffold |

Passing this gate does not make a package mature, stable, compatible, released,
accessible, comfortable, secure, or device-verified. It only admits the system
family to the next Foundry stage.

## Public source basis

- [Unity: Creating custom packages](https://docs.unity3d.com/Manual/CustomPackages.html)
  defines UPM as a way to discover and share reusable components, prescribes
  package layout and tests, and recommends a new project to reduce coupling errors.
- [Game System Reference Standards](https://github.com/Lingkyn/game-system-reference-standards)
  separates cross-project systems, modules, patterns, principles, and anti-patterns
  from consumer-owned GDD/TDD, IDs, delivery, and evidence.
- [Open Source Guides: How to Contribute](https://opensource.guide/how-to-contribute/)
  supports making contribution scope and process explicit in repository-level
  README and CONTRIBUTING surfaces and discussing substantial contributions before
  investing in implementation.

## Required durable record

Every admitted family has one file under `docs/foundry/admissions/` validated by
[`system-admission.schema.json`](system-admission.schema.json). Every admitted
package blueprint must point to that record. A pull request that changes the
common kernel, mandatory boundaries, or evidence basis must update and re-review
the family record rather than silently expanding the package.
