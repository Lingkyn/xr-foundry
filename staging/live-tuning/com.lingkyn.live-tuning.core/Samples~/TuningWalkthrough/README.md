# Tuning Walkthrough sample

`TuningWalkthroughSample.cs` walks through the Live Tuning Core without an asset, a scene,
or any `UnityEngine` API. Call `TuningWalkthroughSample.Run()` from any script, test, or
editor menu and read the returned text.

It shows, in order:

1. A `TunableRegistry` built by explicit registration: one float, one integer, one
   enumerated, and one colour tunable, each with an explicit range or value set.
2. A second registry derived from a small inline token document through `TokenBridge`,
   with an explicit annotation-key set and range policy supplied by the caller — the Core
   holds no token value of its own.
3. A full intent sequence on an immutable `TuningState`: two `set` intents, a `snapshot`,
   another `set`, a `reset`, and `apply_snapshot`, which restores the snapshot's whole
   override set rather than undoing one change at a time.
4. `Export()` to a `TokenOverrideDocument` and its deterministic JSON text.
5. The same short intent sequence applied twice from a fresh initial state. Both results
   are equal and their fingerprints match, which is what "deterministic replay" means in
   the verification contract.

The sample is staging material like the package that carries it; it is authored and
unexecuted until the first Unity gate records a receipt.
