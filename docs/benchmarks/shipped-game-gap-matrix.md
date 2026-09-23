# Shipped-game gap matrix

A library for XR products is measured against what a shipped XR product needs,
not against its own roadmap. This page takes three shipped product shapes, lists
the systems each one had to build, and marks what XR Foundry supplies today. The
matrix drives the source-gate queue: a system that every shape needs and the
library lacks outranks one the library already has on paper.

Status words follow the rest of the tree. **verified** means the packages ran
Unity EditMode or PlayMode tests at the evidence commit named in
`reference-catalog.json`; **staged** means the Core and adapter are authored under
`staging/` with a coverage map and no Unity compiler has run over them;
**candidate** means only a roadmap row exists; **missing** means nothing exists
and nothing is queued; **product** means the system is specific to that product
and the library should never provide it.

## The three shapes

| Shape | Public example genre | Why it is a benchmark |
| --- | --- | --- |
| A. Rhythm action | A beat-matching saber game | The best-selling XR genre; minimal world, maximal input, audio, scoring, and platform-service load |
| B. Object interaction | A job-simulation or hand-puzzle game | Grab, use, combine, and place; inventory and physics-facing interaction dominate |
| C. Spatial creation | A room-scale placement and editing tool | Panels, focus, verbs, placement, undo, and persistence dominate; the closest shape to the library's own consumer |

## The matrix

| System a shipped product needs | A | B | C | XR Foundry today | Family or gap |
| --- | --- | --- | --- | --- | --- |
| XR rig, tracking, project layout, diagnostics | yes | yes | yes | **verified** | Foundations (rank 1) |
| Controller and hand input routed to semantic actions | yes | yes | yes | **verified** | Interaction (rank 2) |
| Comfort and accessibility settings (vignette, turn, height, handedness, volume) | yes | yes | yes | **verified** core, no UI adapter | Settings (rank 3); the UI adapter is LESSON-007's open disposition |
| Save, load, versioned migration (scores, progress, unlocks, documents) | yes | yes | yes | **verified** | Persistence (rank 4) |
| Inventory, containers, lists, item classification | some | yes | yes | **verified** | Inventory (rank 5) |
| Localization of every player-visible string | yes | yes | yes | **staged** | Localization (rank 6) |
| Audio events, mixing, snapshots, attachment to objects | core | yes | yes | **staged** | Audio events (rank 7) |
| Locomotion and comfort policy (teleport, turn, move, vignette) | no | yes | yes | **staged** | Locomotion (rank 8) |
| Scene flow (menu to play to results, loading, fade, recovery) | yes | yes | yes | **staged** | Scene flow (rank 9) |
| World-space panels, wrist and hand menus, pointer and gaze routing | yes | yes | yes | **staged** | XR UI shell (rank 10) |
| In-headset tuning of design values by developers | dev | dev | dev | **staged** | Live tuning (rank 11) |
| Haptic feedback per event and per controller profile | every hit | every grab | every verb | **candidate** | Haptics (rank 12): every shape needs it on the first minute |
| First-run tutorial and onboarding | yes | yes | yes | **candidate** | Tutorial (rank 17) |
| Scoring, combo, progression, objectives | core | yes | some | **candidate** | Objectives (rank 18); scoring rules are product, progression state is library |
| Analytics and consent-gated telemetry | yes | yes | yes | **candidate** | Analytics (rank 19) |
| Platform services: entitlement, achievements, leaderboards, cloud save, store sign-in | yes | yes | some | **candidate** (rank 13 after this page) | Platform services, thin adapters over the vendor SDKs behind one contract |
| Performance and quality tiers (refresh rate, render scale, foveation, per-device presets) | yes | yes | yes | **candidate** (rank 14) | Quality tiers; store certification requires stable frame rate on every headset |
| Content pipeline: data-driven loading of levels, packs, and user content | custom levels | packs | user scenes | **candidate** (rank 15) | Content packs (Addressables or equivalent behind one contract) |
| Spatial placement: ray placement with ghost preview, snapping, commit and cancel | no | some | core | **candidate** (rank 16) | Spatial placement (from the shape C benchmark) |
| Undo and recall with validated recovery | no | some | core | **missing** | Persistence clause first, own family later |
| VFX and feedback helpers (hit, spawn, dissolve) | yes | yes | some | **missing** | New: Feedback effects; small, composes on Audio and Haptics |
| Input binding display and rebinding UI | yes | yes | yes | **missing** | Interaction clause plus a Settings UI adapter |
| Beat sync, note spawning, slicing judgement | core | no | no | product | Never in the library |
| Physics-based tool use (pour, cut, throw) | no | core | some | product-adjacent | Interaction supplies the semantic layer; the physics is product |
| Dialogue and narrative | no | some | no | **candidate** | Dialogue (rank 20) |
| Networking and multiplayer | some | some | some | deferred | Networking (rank 21) |

## What the matrix says

Counting the 24 library-relevant rows (the two product rows excluded): **5
verified, 6 staged, 9 candidate, 3 missing, 1 deferred**. Before this page, four of
the nine candidates did not exist: platform services, quality tiers, a content
pipeline, and spatial placement are on the path of every shipped XR product and
were not in the queue, and haptics sat behind them only by accident of ordering.

Two consequences for the queue, applied in `ROADMAP.md`:

1. Haptics moves ahead of every other candidate, and three new candidates enter
   after it: Platform services, Quality tiers, Content packs. Spatial placement
   enters as the fourth new candidate from shape C. Tutorial keeps its place
   behind them at rank 17; Objectives, Analytics, and Dialogue follow.
2. Until the staged families have run once in a Unity Editor, seven rows sit at
   "staged" and the matrix cannot say whether they are right. One Editor run is
   the single cheapest move on this page.

## How to use this page

- Before proposing a family, add its row here and say which shapes need it.
- Before a release, re-derive the counts; a row that moves from staged to
  verified must cite the evidence commit.
- The shapes are genres, not products; the library admits only public sources
  (platform guidelines, engine documentation, maintained open source) as
  evidence, never a specific product's implementation.
