# Player Settings and Accessibility source coverage matrix

The matrix binds each load-bearing decision to positive public sources. Original
transaction safeguards are admitted only as testable Foundry design decisions;
they are not misattributed to a source that does not implement them.

| Concern | Primary positive sources | Admitted decision | Explicit non-claim |
| --- | --- | --- | --- |
| Accessibility coverage | `microsoft-xag-v3.2` | Metadata categories cover applicable player barriers without requiring every product to implement every feature. | Categories are not compliance or certification. |
| Stable identity and registry | `epic-lyra-game-settings`, `osu-framework-configuration` | Definitions have stable unique identities, typed values, defaults, descriptions and constraints. | No Unreal or osu! API is copied. |
| Typed bounds and notifications | `osu-framework-configuration` | Boolean/integer/float/string/option values use explicit validation and old/new change data. | Generic arbitrary objects and source bindable graphs are excluded. |
| Conditional/cross-setting policy | `epic-lyra-game-settings` | Complete candidate state can drive dependencies and explain unavailable combinations. | Lyra edit conditions do not prove Foundry rollback. |
| Transaction and rollback | Registry/data-source separation above plus Foundry failure analysis | Stage the complete candidate, apply in stable order, roll back partial effects, and test every failure boundary. | This is a Foundry safeguard requiring direct tests, not a borrowed industry claim. |
| Persistence separation | `epic-lyra-game-settings`, `osu-framework-configuration`, existing Persistence family contract | Settings owns typed meaning; a replaceable adapter owns storage/envelope behavior. | No built-in file, INI, PlayerPrefs, cloud or migration provider. |
| Input boundary | `microsoft-xag-input-107`, `epic-lyra-game-settings` | Describe input-related preferences without binding semantic meaning to one physical control. | No rebinding, action map, controller or modality support claim. |
| Motion and immersive comfort discoverability | `microsoft-xag-motion-117`, `apple-hig-motion-visionos` | Products can expose motion/comfort preferences and alternatives before harmful exposure. | No universal locomotion policy, comfort threshold, medical outcome or device claim. |
| Feature discoverability | `microsoft-accessibility-feature-tags-2.0.1`, `microsoft-xag-v3.2` | Metadata includes player-facing description/documentation keys and preview availability. | Metadata never creates a platform feature tag. |
| Unity authoring | `unity-6000.3-scriptableobject` | Unity assets author shared immutable definitions and presets converted to Core. | Assets do not hold mutable player choices or select a renderer. |

## Explicitly uncovered by this source gate

- concrete graphics, audio-mixer, caption, localization or input applicators;
- settings-screen information architecture and renderer acceptance;
- platform account sync, cloud storage, parental controls or certification;
- legal accessibility compliance;
- medical or individualized accessibility recommendations; and
- named-device XR comfort or interaction behavior.

