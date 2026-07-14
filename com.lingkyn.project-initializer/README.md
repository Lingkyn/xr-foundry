# com.lingkyn.project-initializer

Consumer-neutral Unity project scaffolding for folders, scenes, baseline prefabs,
anchor documents, validation, and editor menus.

Status: **incubating**. Use a reviewed commit or local path until candidate evidence
is published.

## Quick start

1. Install the package.
2. Run `Tools > Lingkyn > Project Initializer > Initialize`.
3. Review generated files and run `Validate`.
4. Add project-specific runtime assemblies and adapters under the generated scaffold.

The operation is idempotent for existing folders/assets and never compiles against
consumer runtime assemblies.

See [Documentation~/index.md](Documentation~/index.md).
