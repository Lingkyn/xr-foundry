# Contribution recognition policy

Status: **Proposed V1 policy**

This policy recognizes useful public work without turning contribution volume into
a score, a competition, or repository authority. It implements the recognition
boundary proposed by
[RFC 0002](../rfcs/0002-public-workbench.md).

## Principles

- Recognize code and non-code work when it has public, independently inspectable
  evidence.
- Credit the kind of contribution instead of converting unlike work into points.
- Credit the accountable GitHub identity; disclose material tool assistance
  separately.
- Ask for consent before publishing optional personal details or attribution that
  is not already supplied publicly by the contributor.
- Correct mistakes transparently through reviewable Git history.
- Keep contribution evidence, public thanks, and revocable GitHub permission as
  three separate ledgers.

## Contribution types

V1 accepts the following types. A single contribution may receive more than one
credit record when each type has distinct evidence; it must not be duplicated to
inflate visibility.

| Type | Examples of qualifying evidence |
| --- | --- |
| `code` | A merged implementation or bug-fix commit |
| `docs` | Merged technical, contributor, migration, or user documentation |
| `research` | An admitted source comparison or RFC input that changes a project decision |
| `review` | A substantive human review linked to the resulting change |
| `test` | Automated test design, reproducible defect isolation, or revision-bound test evidence |
| `userTesting` | A consented user/device test receipt with environment, procedure, result, and non-claims |
| `infra` | CI, release, validation, build, or repository-maintenance infrastructure |
| `design` | An accepted architecture, API, interaction, accessibility, or visual design contribution |
| `triage` | Reproduction, issue refinement, source admission, or dependency routing that makes work executable |
| `translation` | Reviewed translation of maintained project material |
| `community` | Mentoring, moderation, onboarding, or coordination with a durable public outcome |

Opening an Issue, generating output, posting a reaction, or submitting an
unreviewed patch is not sufficient by itself. The evidence must show the useful
outcome claimed by the type.

## The three ledgers

| Ledger | Purpose | Authority meaning |
| --- | --- | --- |
| Contribution evidence | One JSON record per accepted credit, validated against [`contribution-credit.schema.json`](contribution-credit.schema.json) | States what public evidence supports; grants no permission |
| Public acknowledgement | [`CONTRIBUTORS.md`](../../CONTRIBUTORS.md), grouped by contribution type with evidence links | Says thank you; is not a reviewer, maintainer, or access-control list |
| Revocable permission | GitHub repository/organization roles, rulesets, branch protection, and CODEOWNERS | The only ledger that can describe current repository authority |

No automation may infer the third ledger from either of the first two. Removing or
changing a GitHub role does not erase historically accurate contribution credit.
Likewise, receiving credit never creates or promises a role.

## Evidence record

Accepted records belong under `docs/contributing/credits/` in a future checkpoint
that also adds validator coverage. Until that checkpoint lands, this policy and the
example define the proposed contract but no ad hoc record should claim validation.

An accepted credit record must include:

- the contributor's public GitHub identity;
- one contribution type and a concise outcome statement;
- at least one public repository evidence URL;
- the immutable repository revision that contains or accepts the outcome;
- the UTC timestamp and maintainer identity that recorded the credit;
- AI/tool-assistance disclosure when applicable; and
- constants confirming that the record grants no repository authority.

Evidence links should prefer merged commits and pull requests, accepted research or
review threads, and validated test/device receipts. Mutable Issue or Discussion
links may provide context but should not be the only evidence for a code, test,
release, or device claim.

The file
[`contribution-credit.example.json`](contribution-credit.example.json) is explicitly
an `example` record. It contains no evidence and grants no credit.

## Public acknowledgement

After an evidence record is accepted, a focused pull request may add or update the
contributor's row in `CONTRIBUTORS.md`:

- list the GitHub identity once;
- list all evidence-backed contribution types without totals;
- link to the accepted evidence record or its immutable revision;
- use alphabetical ordering rather than activity ordering; and
- do not publish an email, legal name, employer, device identifier, or location
  unless the contributor explicitly requests it and publication is necessary.

A contributor may request a corrected display name, link, category, or removal from
the acknowledgement page. The evidence ledger is corrected separately when a fact
is wrong; accurate public history is not silently rewritten.

## AI and advanced-tool assistance

Material coding-agent or generative-tool assistance requires:

1. an accountable human GitHub identity who understands and can defend the entire
   submission;
2. disclosure in the pull request, including the affected scope and validation;
3. an `Assisted-by:` trailer in the commit for material coding assistance; and
4. the same accountable human and assistance metadata in the credit record.

Use the form `Assisted-by: <tool-or-agent>:<model-or-version>` when the version is
known. Do not add `Signed-off-by`, `Co-authored-by`, `Reviewed-by`, `Tested-by`, or
another human-attestation trailer on behalf of a person or tool.

AI review can produce useful findings, but it cannot satisfy a required human
review or approval. A human reviewer must inspect and validate the relevant diff,
evidence, and claims. The contributor, not the assistance tool, receives public
contribution credit and remains accountable.

## No ranking or points

V1 publishes no all-time or seasonal leaderboard, aggregate score, streak, token
count, fastest-completion prize, or “top contributor” order. These mechanisms can
overweight easy-to-count activity, obscure review and testing, and invite duplicate
or low-value work.

Project health may use aggregate, privacy-reviewed signals such as response time,
retention, ready-task completion, review latency, and breadth of contribution types.
Those signals diagnose the workbench; they do not score people.

## Roles and permission changes

A maintainer may later propose a documented responsibility ladder. Any role change
must be based on the repository's current need and a maintainer's explicit assessment
of sustained judgment, reliability, security practice, collaboration, and relevant
scope. It must be revocable and recorded in GitHub's permission ledger.

The following are never sufficient by themselves:

- number of commits, pull requests, reviews, tests, or credit records;
- a leaderboard position or badge;
- AI-generated activity volume;
- one successful contribution; or
- a Task Hall claim lease.

## Corrections, disputes, and abuse

- Open a focused public Issue or pull request for a factual correction that is not
  sensitive.
- Use the private security/reporting route for impersonation, harassment, private
  data, forged evidence, or a security concern.
- Maintainers verify that the evidence belongs to the named identity and that the
  claimed type matches the accepted outcome.
- Duplicate, fabricated, self-referential, inaccessible, or privacy-violating
  evidence is rejected.
- A corrected record and acknowledgement retain reviewable Git history. GitHub
  permission changes follow their own audit path.

## Privacy and executable artifacts

Credit records and acknowledgement must not contain credentials, private repository
data, private consumer content, account identifiers, precise personal location,
device serial numbers, or machine-local paths.

Generated executables, including APK files, do not belong in the source repository.
Device or build contributions may link to separately stored, revision-bound artifacts
and commit small text receipts, digests, and attestation references only after the
artifact-verification contract is implemented.
