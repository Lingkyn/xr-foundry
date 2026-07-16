# Lingkyn Persistence Core

`com.lingkyn.persistence.core` provides a Unity-engine-light save orchestration kernel:

- deterministic binary save envelope encoding/decoding with strict bounds;
- validated slot identifiers and stable stage/error results;
- integrity abstraction with SHA-256 implementation;
- deterministic migration pipeline with explicit rejection paths; and
- capability-declared opaque store contract with fail-closed coordinator behavior.

The package intentionally excludes storage backends, Unity adapter code, UI, cloud, encryption, and project-specific configuration.
