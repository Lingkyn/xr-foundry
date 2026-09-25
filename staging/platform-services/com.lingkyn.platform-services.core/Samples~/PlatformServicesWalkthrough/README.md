# Platform Services Walkthrough sample

`PlatformServicesWalkthroughSample.cs` walks through the Platform Services Core without a vendor
SDK, an asset, or a scene. Call `PlatformServicesWalkthroughSample.Run()` from any script, test, or
editor menu and read the returned text.

It shows, in order:

1. A `ProviderRegistry` built by explicit registration: one provider declaring entitlement,
   achievement, leaderboard, and cloud_save, with a positive cloud_save payload-size guard rail.
2. A composed initial `PlatformServicesState`: the active provider must declare every capability
   the walkthrough requires, checked once before any intent is issued.
3. A full intent sequence: `check_entitlement` resolving identity, `unlock` and `report_score`
   accepted immediately, `read_leaderboard`, and a `cloud_write` issued once while the provider is
   unreachable (queued offline, returning `offline` with its idempotency key) and resolved on a
   second submission with the same idempotency key.
4. The same short intent sequence applied twice from a fresh composed state. Both results have
   equal fingerprints, which is what "deterministic replay" means in the verification contract.

The sample is staging material like the package that carries it; it is authored and unexecuted
until the first Unity gate records a receipt.
