# Work packets: how anyone, with any tool, picks up the next piece

The milestones in [`../milestones.md`](../milestones.md) are reached by many
hands: the maintainer, a steward Agent, a person with a headset, a contributor
using a different coding assistant in an editor or a terminal, or someone who has
never spoken to any of them. None of them shares a session, a chat history, or a
plan that lives in one tool. So every unit of work is a **packet** in
[`work-packets.json`](work-packets.json): a self-contained record that says what
to read, where it may write, what to do, and which commands and artifacts prove it
is done. Repository validation checks the packet file
(`validate_work_packets`), so a packet cannot point at a missing document, an
unknown batch, a circular dependency, or a private script.

## The protocol, tool by tool

1. **Read the guide your tool reads.** `AGENTS.md` at the root is the entry point
   every coding assistant loads; `CLAUDE.md` only includes it. A person starts at
   [`start-here.md`](start-here.md). Nothing else is required.
2. **Pick a packet.** Run `python scripts/open_work.py --markdown` for the whole
   board, or read `work-packets.json` and take the first packet whose `needs` you
   satisfy (`none` means a clone is enough) and whose `depends_on` packets are
   `done`. A packet is not a claim: two contributors may take the same `none`
   packet; the merge-readiness verdict decides between the results, and the second
   one rebases. Only a non-routine packet needs a Task Hall claim
   ([`task-hall.md`](task-hall.md)).
3. **Stay inside `allowed_paths`.** Packets with disjoint paths run in parallel,
   in one session or across many tools. A change outside the paths is a separate
   packet or a separate pull request.
4. **Do the `steps`, then run the `acceptance.commands`** in a clean virtual
   environment. Every command is a script in `scripts/` or `python -m unittest`;
   nothing depends on the tool that produced the change.
5. **Push and open the five-line pull request** as [`start-here.md`](start-here.md)
   describes. The verdict, not a person, says whether a routine packet merges. A
   non-routine packet waits for the maintainer, who reads the same verdict.
6. **Mark the packet.** In the same pull request, set `status` to `done` and
   `done_proof` to the path that proves it (the artifact, the receipt, the record).
   Validation refuses `done` without an existing proof path. If you stop early,
   set `status` to `in_progress` and publish a continuation receipt
   ([`work-continuation.schema.json`](work-continuation.schema.json)) so the next
   hand, in any tool, starts from your exact revision and next safe action.

## What a packet never does

- It grants no permission: not write, review, merge, release, promotion, or device
  status. Those come from the verdict, the maintainer, and the evidence gates.
- It assumes nothing about who does it. The acceptance commands are the same for
  a maintainer, a steward Agent, or a stranger's assistant, because the process
  must catch bad work regardless of who authored it.
- It is never marked `done` by the tool that did the work on its own say-so; the
  proof path is what the next reader checks.

## How packets are added

A packet is a routine change to `work-packets.json`. It needs an `id` in the
`WP-nnn` series, a batch that exists in `milestones.md`, `read_first` paths that
exist, and acceptance commands from the accepted script list. A packet for a new
family follows the queue: source gate first, staged implementation second, never a
package directory before admission. When a milestone cell moves to `done`, the
packet that did it is the proof path for that cell.
