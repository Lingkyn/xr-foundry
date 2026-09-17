# Comfort Policy sample

`ComfortPolicySample.cs` walks through the Locomotion Core without a scene, a rig, an
XR device, or any `UnityEngine` API. Call `ComfortPolicySample.Run()` from any script,
test, or editor menu and read the returned text.

It shows, in order:

1. The default comfort policy (snap turn at 45 degrees, 1.5 m/s movement speed,
   standing posture, vignette off) and two registered anchor ids. Every id is a
   validated identity with one canonical form; `ModeId` is a closed set of four modes,
   `AnchorId` is an open vocabulary sharing the same canonicalization rules.
2. An intent sequence: register two anchors, teleport to one, turn, move for one
   tick, and one intent that is deliberately rejected — a smooth-turn request while
   the policy still selects snap turn, which fails with `mode.disabled` and leaves the
   state it found intact — followed by a comfort-policy change (raising movement
   speed) and a second teleport.
3. The same sequence applied twice from the same initial state. Both results are
   equal, which is what "deterministic state" means in the verification contract.
4. The final state read back: the active mode set, the comfort policy, the current
   anchor, the accumulated heading, and the accumulated planar offset, each an
   immutable read.

The sample is staging material like the package that carries it; it is authored and
unexecuted until the first Unity gate records a receipt.
