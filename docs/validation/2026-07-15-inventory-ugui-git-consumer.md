# Inventory UGUI immutable non-XR consumer validation — 2026-07-15

## Verdict

`com.lingkyn.inventory.ugui@0.1.0` passes the UGUI candidate gate. Core, authoring,
and UGUI resolved from one immutable Git revision in a consumer manifest containing
no XR package. Shipped prefabs were validated read-only from the package cache.

This verdict does not cover world-space Canvas configuration, tracked controllers,
headset readability, comfort, occlusion, or Pico behavior.

## Immutable revision

- Candidate commit: `b50834418c3573fb1cf341400bf5a85337e43d3b`
- UGUI package tree: `ee4a2f0096580d52aa65f12e8ad5ae71c98899b1`
- Final evidence-only repository changes do not modify this package tree.

## Consumer and evidence

- Unity: `6000.3.19f1`
- Consumer dependencies: Inventory Core, Inventory Unity authoring, Inventory UGUI,
  and Unity Test Framework; no XR package entry
- EditMode: 29 passed, 0 failed; UGUI editor tests: 4 passed
- PlayMode: 1 passed, 0 failed
- EditMode result/log SHA-256: `B4252FF702CEF8F83619E14F57B60A74C243EF1DA6EC0B4DD2688F915230A2DE` / `7B59D3C3E4AFBC2DE3F625D08BC5B3792D05B0BC883D2F491EF19AF3F1A988FE`
- PlayMode result/log SHA-256: `31CED33A2D0D298D849205D063A3BEF96596A25724726A059CC9721F5C1AC30B` / `D0A415E560C6E76CA61C62DF7B0AA855CC9254B1416FE9A5EF13A01FA441AF24`

## Failure-driven correction

Two false-positive-prone boundaries were corrected before promotion. Serializable
view components were split into same-named scripts after prefab saving detected a
missing-script defect. Immutable-package tests were changed from rebuilding package
assets to validating the shipped read-only prefabs after Git package cache semantics
rejected writes. Both corrections are now enforced by passing tests.

## Claim boundary

This receipt closes Issue #8 only. World-space/XRI configuration and real Pico
evidence remain Issue #9 work.
