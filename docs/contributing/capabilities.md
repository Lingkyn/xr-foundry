# What do you bring? Declare it before you take work

Every arriving contributor, person or coding Agent, answers one question first:
what do you bring? An AI budget, a Unity Editor, a headset, or rights on this
repository. The answer is not paperwork. It decides which work you can finish and,
more importantly, which claims you must never make. The declarations live in
[`capability-profiles.json`](capability-profiles.json) and repository validation
keeps them honest (`validate_capability_profiles`).

Run this, answer for yourself, and take the work your answer reaches:

```text
python scripts/open_work.py --list-capabilities
python scripts/open_work.py --capability <id> --markdown
```

The board narrows to the items that declaration can finish and says how many need
a different one. Nothing is hidden: work you cannot reach is work for another
declaration, not a secret.

## The four declarations

| Declaration | You bring | You reach | You may never claim |
| --- | --- | --- | --- |
| `ai_tokens_only` | A clone, Python, and an AI budget (or your own hands) | Work that waits on nothing, and the outside-contributor items | That anything compiled or ran; a version bump, maturity, profile, release, or tag; any headset behaviour |
| `unity_editor` | The above plus a machine with the pinned Editor | The same, plus every item waiting on an Editor run | Device behaviour; a maturity promotion; that one Editor result transfers to another tuple |
| `xr_headset` | A named device and a way to install a build of an exact commit | The same as the first, plus every item waiting on a headset | Anything outside the exact tuple your receipt records; `not_tested` is never evidence |
| `maintainer` | Rights: settings, secrets, branch protection, tags, releases, signatures | Work that waits on a maintainer or a review window | An Editor or device result without its receipt; that a signature replaces the gate it signs |

## Why the repository asks

Three reasons, all of them things this repository already learned the hard way:

1. **Most of the library's remaining work is capability-bound.** A contributor with
   only tokens cannot produce Unity or device evidence, no matter how good the
   model is. Sending them at an evidence gate wastes their budget and produces a
   plausible-looking claim with nothing behind it.
2. **The process must catch a claim no matter who wrote it.** A declaration is a
   promise the validator can check against what actually lands: authored C# with
   no receipt stays "authored, unexecuted"; a device claim without a receipt fails;
   a version bump without evidence fails. The declaration tells you that in
   advance instead of at review time.
3. **A capability is not a permission.** Declaring `maintainer` does not grant
   rights, and declaring a headset does not make an observation true. The
   profile routes work; the gates, the verdict, and the receipts decide what is
   accepted.

## If you bring more than one

Declare the strongest thing you can actually do today, and re-declare when that
changes. A person with an Editor this week and a headset next week is two
declarations, and the work item's `needs` field, not a memory of last week, is
what the board matches against.

## If you bring something these four do not cover

A reviewer with no Editor, a translator, a technical writer, a designer: all of
that is routine work that waits on nothing, so `ai_tokens_only` reaches it. If a
whole new kind of capability appears (a second engine, a CI runner pool, a test
farm), it is a routine change to `capability-profiles.json` plus the work items
addressed to it; the validator then requires that every `needs` value some work
item uses is reachable by at least one declaration.
