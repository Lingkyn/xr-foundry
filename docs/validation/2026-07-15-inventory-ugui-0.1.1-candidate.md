# Inventory UGUI 0.1.1 candidate validation - 2026-07-15

## Verdict

`com.lingkyn.inventory.ugui@0.1.1` passes its non-XR candidate gate. The
functional shipped prefab graph, state replay, standard UGUI input, sample import,
and consumer-owned prefab-variant migration were exercised from a full immutable
Git revision in a persistent clean Unity consumer.

This verdict does not cover world-space Canvas configuration, tracked controllers,
headset readability, comfort, occlusion, or Pico behavior. Those claims belong to
the optional XR package and require separate real-device evidence.

## Immutable revision

- Corrective candidate commit: `ea7207e94aa6fa9c129e5bf381d77f932bf35911`
- UGUI package tree: `cd914a12a7a3f48ece5186a78147760c83f15b9f`
- Released baseline/rollback commit: `38731dd1cb549b9963f5c100251da1836cfdf3eb`
- Unity: `6000.3.19f1`
- Consumer input profile: Input System `1.19.0`, active Input Handling set to
  Input System only
- Consumer manifest contained Inventory Core, Unity authoring, UGUI, Input System,
  and Unity Test Framework; it contained no XR package

## Functional evidence

- EditMode: 34 passed, 0 failed, including 9 UGUI prefab/presenter tests
- PlayMode: 3 passed, 0 failed, all using shipped prefabs and real
  `GraphicRaycaster`/EventSystem paths
- State Gallery sample imported through the Unity Package Manager sample API; its
  runtime and editor assemblies compiled in a fresh invocation
- Sample setup created exactly one `EventSystem`, exactly one
  `InputSystemUIInputModule`, and a non-null Input System actions asset
- Repository validator passed; Python contract suite passed 11/11

Final result/log SHA-256:

- EditMode XML: `12CBEED266265EB537FB16E8E767BB1447E5F918B928869BE3E6D2D2378A0B24`
- EditMode log: `33479FFF556452A581D22577F2288ED6701A84BCA7DA14122999B5F6F5D475FA`
- PlayMode XML: `40D31D97412DF9FDC1AE4548E2FC7E4D061049B0725D2D03C12EA8D55B1E87A5`
- PlayMode log: `F8EDE62CF80E4FC1695C2A95C044027CC728ADE2346D6CA56D39F513BC858DCE`
- Sample import log: `4ADE466A7F9F26E5FCEA0078E8A2AE50DC22AE5272039B8CD21AF7225FA8BE3B`
- Sample setup log: `A8BFB8B0B122EC86967C6759191DC72C26E58013BB1A5A921DD62261A2D97992`

## Upgrade, rollback, and consumer ownership

The same consumer-owned `InventorySlot` prefab variant and its harmless root-name
override survived this complete sequence:

1. install `0.1.0` from commit `38731dd...`, create the variant, and validate its
   source link, override, and zero missing scripts;
2. upgrade to `0.1.1` at `ea7207e...`, then validate the same source/override plus
   non-null `ItemView`, `Background`, and `SelectionControl` bindings;
3. roll back to `0.1.0`, then revalidate source, override, and zero missing scripts;
4. upgrade again to `0.1.1`, revalidate functional bindings, import the sample, and
   rerun the complete package test suites.

Sequence-log SHA-256:

- Baseline variant: `32A79B2EFCF11BE509854EDD65DE1F4981DC0BBD33BA0036EE3FDD1B7B977F24`
- First upgrade: `D6274F23840ACC2FEC52A5C02CCC42E02BEB17BAE963F9FB94AD1651F8FE5E55`
- Rollback: `BFB34F71F5332CB79D2D8018D520915B9FF72CB853FF9BE6A7587CA940931E9B`
- Final upgrade: `ECAD7D0F11C53FF2000842BBCAF1513CB4B8FA3896922DB0612A21E471D5C7FC`

## Historical correction

The `0.1.0` release and tag remain intact, but their earlier functional-candidate
claim is withdrawn. That version retained nested prefab source links while shipping
null view bindings and no usable visual/input controls. Its validation receipt and
GitHub Release are marked as superseded instead of rewriting public history.

## Claim boundary

This receipt closes the UGUI portion only. It is evidence neither for the complete
Inventory family nor for the optional XR layer.
