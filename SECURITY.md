# Security Policy

## Supported versions

No package is currently marked stable. Security fixes target the latest reviewed
commit and any release explicitly listed as supported in its release notes.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting for this repository when available.
Do not open a public issue containing exploit details, credentials, private project
data, or machine-local paths. Include affected package/version, impact, reproduction,
and any suggested mitigation.

The maintainer will acknowledge a valid report and coordinate disclosure after a fix
or mitigation is available.

## Contribution and automation boundary

Issue bodies, comments, pull-request patches, dependency content, logs, links, and
uploaded artifacts are untrusted input. The repository does not execute `/claim` or
other comment commands. Task claims grant no GitHub permission. Validation workflows
use read-only permissions, pin third-party Actions to reviewed full commit SHAs, and
must not expose secrets to forked pull requests.

Changes that add comment-trigger execution, broaden workflow permissions, consume
secrets, install unpinned code, or alter review/merge authority require an explicit
security review and maintainer decision.
