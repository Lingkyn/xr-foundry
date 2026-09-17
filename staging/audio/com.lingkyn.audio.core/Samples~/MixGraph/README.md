# Mix Graph sample

`MixGraphSample.cs` walks through the Audio Core without a scene, an asset, a clip, a
mixer, or any `UnityEngine` API. Call `MixGraphSample.Run()` from any script, test, or
editor menu and read the returned text.

It shows, in order:

1. A `MixGraph` built with `MixGraphBuilder`: a required root bus, two child buses,
   a float parameter with a declared range, an enumerated parameter with a declared
   value set, two snapshots that reference in-graph buses, and two events routed to
   buses. Every id is a validated identity with one canonical form.
2. An intent sequence: register an anchor id, post two events, attach one event to the
   anchor, set two parameters, and transition to a snapshot with an explicit duration.
   One intent is deliberately out of range and is rejected with
   `parameter.out_of_range`; the state it found stays intact.
3. The same sequence applied twice from the same initial state. Both results are equal,
   which is what "deterministic state" means in the verification contract.
4. The final state read back: active events, the current snapshot, every bus parameter
   value, and every attachment, each as an immutable enumeration.

The sample is staging material like the package that carries it; it is authored and
unexecuted until the first Unity gate records a receipt.
