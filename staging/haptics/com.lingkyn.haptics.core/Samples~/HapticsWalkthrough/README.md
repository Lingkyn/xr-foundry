# Haptics Walkthrough sample

`HapticsWalkthroughSample.cs` walks through the Haptics Core without an asset, a scene, or
any `UnityEngine` API. Call `HapticsWalkthroughSample.Run()` from any script, test, or editor
menu and read the returned text.

It shows, in order:

1. A `HapticEventRegistry` built by explicit registration: one transient, one continuous, and
   one envelope event, each with an explicit amplitude/duration/frequency guard rail and default.
2. Two `HapticProfile`s in one `HapticProfileSet`: a default profile that scales one named
   device down, and a boosted profile that scales the left target up.
3. A full intent sequence on an immutable `HapticState`: two `play` intents, a `set_profile`,
   a `stop`, and `stop_all`.
4. A `HapticBindingTable` resolving one bound source intent to a haptic `play` intent, and the
   named `binding.unbound` result for a source with no binding — never silence.
5. The same short intent sequence applied twice from a fresh initial state. Both results have
   equal fingerprints, which is what "deterministic replay" means in the verification contract.

The sample is staging material like the package that carries it; it is authored and unexecuted
until the first Unity gate records a receipt.
