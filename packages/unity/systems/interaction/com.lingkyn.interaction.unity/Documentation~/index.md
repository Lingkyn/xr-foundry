# Lingkyn Interaction Unity

## Install

Pin Core and this package to the same reviewed full commit SHA. The package has a
concrete dependency on Input System `1.14.2` and Core `0.1.0`; another tuple must
earn its own compatibility evidence.

## Convert authored assets

```csharp
InteractionAuthoringResult result =
    InteractionAuthoringConverter.Convert(registryAsset);

if (!result.Succeeded)
{
    foreach (InteractionAssetIssue issue in result.Validation.Issues)
        Debug.LogError($"{issue.AssetName} {issue.FieldPath}: {issue.Message}");
}
```

The conversion reads but never mutates the authored assets. Runtime player
choices belong in override/policy snapshots supplied by the consumer.

## Capture one observation

```csharp
InteractionResult<InteractionFrame> frame =
    InputSystemSignalAdapter.CaptureRawObservation(
        orderedRouteCandidates,
        "player.primary",
        InteractionModality.Simulated,
        InteractionCapability.Digital,
        InteractionPhase.Performed,
        true,
        timestampTicks,
        new InputObservationStamp(observationSequence, firstIngressSequence));
```

For a live callback use `CaptureCallback` with the same explicit facts. Multiple
route candidates derived from one callback must share the action GUID, source,
modality, capabilities, and value kind. The adapter preserves the supplied order.

## Binding display and overrides

`InputBindingDisplayService.GetEntries` enumerates every binding and returns its
stable binding GUID plus Input System display metadata. Display strings remain
presentation data, not semantic identities.

`InputBindingOverrideService` captures only runtime action overrides, serializes
stable action/binding GUID records, validates the whole snapshot, applies by
binding GUID, and can package the serialized token behind Core's opaque
`BindingOverride` seam. The package provides no storage backend or rebinding UI.
