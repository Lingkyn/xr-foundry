# Input System routing sample

1. Create an Input Action Asset and one or more actions/bindings.
2. Create Lingkyn Interaction Intent, Route, Context, and Registry assets.
3. Assign each Route an explicit `InputActionReference`; do not use action-name or
   scene lookup.
4. Convert the Registry once and retain the returned `InteractionUnityRegistry`.
5. In each Input Action callback, select the ordered route candidates for that
   action and call `InputSystemSignalAdapter.CaptureCallback` with an explicit
   observation/ingress stamp, timestamp, source, modality, and capabilities.
6. Submit the resulting Core frame to an `InteractionCoordinator`.

The sample intentionally uses no XRI, OpenXR, world-space UI, scene search,
vendor SDK, or device assumption. Add those only in a separately evidenced
consumer or adapter.
