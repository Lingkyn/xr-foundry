# Inventory Core 0.1.0 candidate validation — 2026-07-15

## Verdict

`com.lingkyn.inventory.core@0.1.0` passes the Core candidate gate. This verdict is
bounded to the engine-light Core package. Unity authoring, UGUI, XR, controller,
headset, comfort, and Pico behavior remain unimplemented or unproven.

## Immutable revisions

- Prerelease baseline tag: `com.lingkyn.inventory.core@0.1.0-pre.1`
- Prerelease commit: `c88ce8ca404504efb0aa11b9cf51310393078612`
- Candidate commit tested: `4810c07abde55848dbca7fa3eb128414a33d7977`
- Candidate package tree: `fe032e83288f9feaea756b3f059ee1531a36ffbf`

The final evidence-only repository changes do not modify that candidate package
tree. Both package revisions were consumed through a pinned Git URL with the
package subdirectory selector.

## Consumer and sequence

- Unity: `6000.3.19f1`
- Test platform: EditMode, batch mode
- Consumer: the existing persistent clean Unity smoke project outside this repository
- Sequence:
  1. install immutable `0.1.0-pre.1`;
  2. upgrade the same consumer to immutable `0.1.0` candidate;
  3. roll the same consumer back to immutable `0.1.0-pre.1`.

Every boundary resolved from Git and produced 23 passed tests, 0 failed, including
21 Inventory Core tests. The package lock recorded the expected commit after each
transition.

## Evidence hashes

| Boundary | Result SHA-256 | Log SHA-256 |
| --- | --- | --- |
| Prerelease install | `2D3702A2AD38758F6D34787DAF7A30DB28B262775E82B8A3F75415444535DE77` | `02A0131E2ACC0B1CB3490A4AA2C5B5C97E803891B1543BEA27EE8ED2AD2E2724` |
| Final candidate upgrade | `91CF8B4C6B42AEFCD4E950F85B571A671E35D6B3B699B455F0AE6526E2E97836` | `9D902B6B08237492D1F81683F2BFFE0ED11A3DA73FA047E6E4DE76492D92846D` |
| Final prerelease rollback | `8E10C3FE5249B8B1E08754E758AC1288DA738ED570F35C2BEECA65996C70B396` | `9EC92F16CF9BCA7DE965C7ACD156B36656E93B970A1C64948DFD70897309670E` |

## API and persistence compatibility

The candidate public Runtime type set matches the prerelease baseline. No public
type was removed and aggregate schema version 2 remains current. Existing
definition, inventory, container, instance, and fragment IDs retain their meaning.
Supported aggregate and fragment migrations remain deterministic and transactional.

## Claim boundary

This receipt closes Issue #6's Core candidate gates only. It cannot be used as
evidence for ScriptableObject authoring, nested prefabs, presentation states,
world-space UI, tracked-device interaction, or real-device usability.
