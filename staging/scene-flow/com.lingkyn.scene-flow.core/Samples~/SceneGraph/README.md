# Scene Graph sample

`SceneGraphSample.cs` walks through the Scene Flow Core without a scene, an asset, a
build index, or any `UnityEngine` API. Call `SceneGraphSample.Run()` from any script,
test, or editor menu and read the returned text.

It shows, in order:

1. A `SceneGraph` built with `SceneGraphBuilder`: a menu set, two content sets (one
   scene, `level1.audio`, is shared between them, which the graph allows), and a
   fallback set, each with a declared active member. `TransitionOptions` declares
   non-zero fade-out, hold, and fade-in durations so every phase is a distinct,
   explicit step instead of collapsing on the tick it is entered.
2. Loading the menu set through the full phase machine — `idle` to `fading_out` to
   `loading` to `holding` to `fading_in` and back to `idle` — driven only by explicit
   `ElapsedTimeIntent` and `SceneLoadCompletedIntent` values, never a clock.
3. Loading `level1` additively (the menu stays loaded) and then one of its scenes
   failing to load. The failure is recorded as `load.failed` and the flow recovers to
   the declared fallback set from the same phase (`loading`), never a fresh
   `fading_out`.
4. The final state read back: the loaded sets, the union of their loaded scenes, and
   the active scene, each an immutable enumeration.
5. The same short intent sequence applied twice from a fresh initial state. Both
   results are equal and their fingerprints match, which is what "deterministic
   replay" means in the verification contract.

The sample is staging material like the package that carries it; it is authored and
unexecuted until the first Unity gate records a receipt.
