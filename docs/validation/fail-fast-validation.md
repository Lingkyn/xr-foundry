# Fail-fast repository validation

Use one process as the pre-mutation acceptance gate:

```text
python scripts/validate_repository.py --json --run-contract-tests
```

The command runs repository validation first. If that stage fails, contract tests
are not started and the process exits non-zero. If repository validation passes,
the same process runs the complete contract suite and exits non-zero on any test
failure. A contributor or Agent may commit or push only after this command exits
zero; a later shell command must not be used to hide or overwrite its exit status.

GitHub Actions remains the merge-time enforcement boundary. A local result is
execution evidence, not permission to bypass the required pull-request check.
The command does not grant repository authority and does not prove any headset,
controller, comfort, or other device behavior.

Independent review is separated at the executing-Agent boundary, not by forcing
different GitHub accounts. Multiple local or cloud Agents may work under one
accountable maintainer identity, but an accepted receipt requires disjoint
`assisted_by` sets, fresh review context, and `reviewed_own_output=false`. The
maintainer identity remains accountable for the final decision.
