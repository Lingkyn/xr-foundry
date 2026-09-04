# Changelog

## [Unreleased]

- Replace scaffold marker with engine-light persistence core implementation.
- Add deterministic save envelope codec, migration pipeline, integrity abstraction, and save coordinator orchestration.
- Add focused editor tests for slot IDs, malformed/future envelopes, checksum corruption, migration rejection paths, fail-closed stage order, and capability mismatch.
- Admit zero-byte raw read candidates so the recovery selector classifies them as malformed envelopes and preserves the primary corruption diagnostic during backup recovery.
