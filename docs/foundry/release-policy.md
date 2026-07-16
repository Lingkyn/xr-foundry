# Foundry release policy

A Foundry batch may be released when its manifest and every listed package agree
with the catalogs, manifests, source boundary, license, and recorded evidence.
The release tag binds the complete repository commit because Unity Git installs
resolve package subfolders from that commit.

Release requirements:

- complete repository contract and public CI pass at the release commit;
- immutable tag and release notes list every package ID, version, path, maturity,
  install selector, verified claims, and non-claims;
- independent review has no unresolved high-risk finding;
- maintainer makes the final release decision;
- no package maturity changes merely because it appears in a batch;
- no automated result becomes a named-device claim;
- rollback keeps the prior immutable tag available and never rewrites public
  history.

External contributors and Agents may prepare evidence and release notes. They do
not receive tag, release, maturity-promotion, or merge authority by doing so.

