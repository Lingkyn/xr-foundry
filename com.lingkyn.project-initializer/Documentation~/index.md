# Lingkyn Project Initializer

`com.lingkyn.project-initializer` creates an opinionated but consumer-neutral
Unity project scaffold. It does not provide gameplay systems or depend on a
consumer project's runtime assemblies.

## Evaluate locally

```json
"com.lingkyn.project-initializer": "file:../../xr-foundry/com.lingkyn.project-initializer"
```

Run:

```text
Tools/Lingkyn/Project Initializer/Initialize
Tools/Lingkyn/Project Initializer/Validate
```

Initialization creates the `Assets/_Project` folder contract, four baseline scenes,
empty system/UI prefabs, project anchor documents, Input Actions seed, and an
activation marker for build validation. Existing assets are preserved where possible.

## Boundaries

- Generated scene roots are structure, not product content.
- Empty marker objects and prefabs are extension points, not hidden runtime services.
- Consumer-specific assemblies, gameplay types, settings, prefabs, and vendor
  adapters remain in the consumer project.
- Remove `Assets/_Project/Settings/LingkynProjectInitializer.marker` to disable the
  package build preprocessor while retaining generated assets.
