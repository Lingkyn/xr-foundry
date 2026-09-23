# Shell Walkthrough sample

`ShellWalkthroughSample.cs` walks through the XR UI Shell Core without an asset, a scene, or
any `UnityEngine` API. Call `ShellWalkthroughSample.Run()` from any script, test, or editor
menu and read the returned text.

It shows, in order:

1. A `ShellLayout` built by explicit declaration: two panels, a wrist menu, and a hand menu,
   and two registered input sources (a ray and a gaze source).
2. A placement intent sequence on an immutable `ShellState`: `open`, `focus`, a second `open`,
   and `dock`.
3. Typed pointer and gaze routing: a ray `hover` that resolves against the focused panel, and a
   gaze `select` that is rejected with `source.kind.unsupported` for lacking a registered commit
   source before it resolves once one is supplied — the head ray is never the primary pointer.
4. The canonical skin mapping resolving the `panel.background` slot to its shared
   design-language token.
5. The same intent sequence replayed from a fresh initial state, whose fingerprint equals the
   original, which is what "deterministic replay" means in the verification contract.

The sample is staging material like the package that carries it; it is authored and unexecuted
until the first Unity gate records a receipt.
