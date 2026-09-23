using System.Collections.Generic;

namespace Lingkyn.XrUiShell.Core
{
    // The closed set of placement intents (open, close, focus, dock, follow, fold, unfold)
    // applied to an immutable ShellState, from any control, panel, or input that raises them.
    // Each intent names exactly one declared surface, plus a target surface for dock and a
    // target anchor kind for follow, exactly as the layout-model clause of the verification
    // contract states. Fold and unfold change only SurfaceRuntimeState.IsFolded (visibility);
    // they never touch the open flag, the docked target, or the follow flag, so a folded surface
    // keeps its full state, and they always reject the shell ornament (ShellOrnament.cs).

    /// <summary>The closed set of sources that may issue a shell or verb intent: a person through
    /// a panel or dock control, an agent adapter, a replay of a recorded sequence, or an import.
    /// Carried on every intent for attribution and replay only; it never selects a different
    /// validation rule, so a player-issued and an agent-issued copy of the same intent take the
    /// same path through <see cref="ShellState.Apply"/> and get the same result, and a dock verb
    /// pressed by a person and the same verb issued by an agent adapter resolve through the same
    /// <see cref="FocusSubject"/> and the same affordance (LESSON-011).</summary>
    public enum IntentActor
    {
        Player,
        Agent,
        Replay,
        Import,
    }

    /// <summary>Closed set of placement intents applied to a <see cref="ShellState"/>. Every
    /// intent carries an <see cref="Actor"/> (defaulting to <see cref="IntentActor.Player"/> when
    /// a caller uses the short constructor) and an optional <see cref="ExpectedRevision"/>: a
    /// non-null value that does not match the state's current revision is rejected with
    /// <see cref="ShellFailure.StateStale"/> by <see cref="ShellState.Apply"/> before this
    /// intent's own <see cref="ApplyTo"/> ever runs, rather than overwriting another actor's
    /// change. Neither field changes which path an intent takes or what it validates.</summary>
    public abstract class ShellIntent
    {
        private protected ShellIntent(IntentActor actor, long? expectedRevision)
        {
            Actor = actor;
            ExpectedRevision = expectedRevision;
        }

        /// <summary>Who issued this intent. Attribution and replay only; see the type doc.</summary>
        public IntentActor Actor { get; }

        /// <summary>The revision this intent's issuer last observed, or null to skip the check.</summary>
        public long? ExpectedRevision { get; }

        public abstract string Describe();

        internal abstract ShellResult<ShellState> ApplyTo(ShellState state);

        public override string ToString() => Describe();
    }

    public sealed class OpenIntent : ShellIntent
    {
        public OpenIntent(SurfaceId surface) : this(surface, IntentActor.Player, null) { }

        public OpenIntent(SurfaceId surface, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            Surface = surface;
        }

        public SurfaceId Surface { get; }

        public override string Describe() => $"open {Surface}";

        internal override ShellResult<ShellState> ApplyTo(ShellState state) => state.ApplyOpen(Surface);
    }

    public sealed class CloseIntent : ShellIntent
    {
        public CloseIntent(SurfaceId surface) : this(surface, IntentActor.Player, null) { }

        public CloseIntent(SurfaceId surface, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            Surface = surface;
        }

        public SurfaceId Surface { get; }

        public override string Describe() => $"close {Surface}";

        internal override ShellResult<ShellState> ApplyTo(ShellState state) => state.ApplyClose(Surface);
    }

    public sealed class FocusIntent : ShellIntent
    {
        public FocusIntent(SurfaceId surface) : this(surface, IntentActor.Player, null) { }

        public FocusIntent(SurfaceId surface, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            Surface = surface;
        }

        public SurfaceId Surface { get; }

        public override string Describe() => $"focus {Surface}";

        internal override ShellResult<ShellState> ApplyTo(ShellState state) => state.ApplyFocus(Surface);
    }

    /// <summary>Docks <see cref="Surface"/> to <see cref="Target"/>: the target must already be
    /// declared, and <see cref="Surface"/> must admit the target's home anchor kind.</summary>
    public sealed class DockIntent : ShellIntent
    {
        public DockIntent(SurfaceId surface, SurfaceId target) : this(surface, target, IntentActor.Player, null) { }

        public DockIntent(SurfaceId surface, SurfaceId target, IntentActor actor, long? expectedRevision = null)
            : base(actor, expectedRevision)
        {
            Surface = surface;
            Target = target;
        }

        public SurfaceId Surface { get; }
        public SurfaceId Target { get; }

        public override string Describe() => $"dock {Surface} -> {Target}";

        internal override ShellResult<ShellState> ApplyTo(ShellState state) => state.ApplyDock(Surface, Target);
    }

    /// <summary>Makes <see cref="Surface"/> follow <see cref="TargetAnchorKind"/>: the surface
    /// must admit that anchor kind.</summary>
    public sealed class FollowIntent : ShellIntent
    {
        public FollowIntent(SurfaceId surface, AnchorKind targetAnchorKind) : this(surface, targetAnchorKind, IntentActor.Player, null) { }

        public FollowIntent(SurfaceId surface, AnchorKind targetAnchorKind, IntentActor actor, long? expectedRevision = null)
            : base(actor, expectedRevision)
        {
            Surface = surface;
            TargetAnchorKind = targetAnchorKind;
        }

        public SurfaceId Surface { get; }
        public AnchorKind TargetAnchorKind { get; }

        public override string Describe() => $"follow {Surface} -> {TargetAnchorKind}";

        internal override ShellResult<ShellState> ApplyTo(ShellState state) => state.ApplyFollow(Surface, TargetAnchorKind);
    }

    /// <summary>Folds <see cref="Surface"/>: changes only its visibility (<see
    /// cref="SurfaceRuntimeState.IsFolded"/>) and leaves every other field of its runtime state
    /// intact, so a folded surface keeps its full open/docked/following state. Always rejected
    /// for the shell ornament.</summary>
    public sealed class FoldIntent : ShellIntent
    {
        public FoldIntent(SurfaceId surface) : this(surface, IntentActor.Player, null) { }

        public FoldIntent(SurfaceId surface, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            Surface = surface;
        }

        public SurfaceId Surface { get; }

        public override string Describe() => $"fold {Surface}";

        internal override ShellResult<ShellState> ApplyTo(ShellState state) => state.ApplyFold(Surface);
    }

    /// <summary>Unfolds <see cref="Surface"/>: the visibility-only inverse of
    /// <see cref="FoldIntent"/>. Always rejected for the shell ornament.</summary>
    public sealed class UnfoldIntent : ShellIntent
    {
        public UnfoldIntent(SurfaceId surface) : this(surface, IntentActor.Player, null) { }

        public UnfoldIntent(SurfaceId surface, IntentActor actor, long? expectedRevision = null) : base(actor, expectedRevision)
        {
            Surface = surface;
        }

        public SurfaceId Surface { get; }

        public override string Describe() => $"unfold {Surface}";

        internal override ShellResult<ShellState> ApplyTo(ShellState state) => state.ApplyUnfold(Surface);
    }

    /// <summary>The outcome of one intent inside a sequence. Together, the outcomes of a sequence
    /// are the replay log: <see cref="Actor"/> (the issuer, from the intent) and
    /// <see cref="RevisionAfter"/> (the state's revision immediately after this outcome — the new
    /// state's revision when accepted, the unchanged state's revision when rejected) record who
    /// did what and at which revision, for every intent, accepted or rejected, in order.</summary>
    public sealed class ShellIntentOutcome
    {
        public ShellIntentOutcome(int index, ShellIntent intent, bool accepted, string code, string message, long revisionAfter)
        {
            Index = index;
            Intent = intent;
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RevisionAfter = revisionAfter;
        }

        public int Index { get; }
        public ShellIntent Intent { get; }
        public bool Accepted { get; }
        public string Code { get; }
        public string Message { get; }
        public long RevisionAfter { get; }

        /// <summary>The actor that issued this outcome's intent. Forwarded from <see cref="Intent"/>
        /// for convenience; attribution and replay only, per <see cref="IntentActor"/>.</summary>
        public IntentActor Actor => Intent.Actor;
    }

    /// <summary>The state after a sequence and one outcome per intent. A rejected intent keeps the
    /// state it found; the next intent in the sequence still applies to that same state.</summary>
    public sealed class ShellSequenceResult
    {
        public ShellSequenceResult(ShellState state, IReadOnlyList<ShellIntentOutcome> outcomes)
        {
            State = state;
            Outcomes = outcomes;
        }

        public ShellState State { get; }
        public IReadOnlyList<ShellIntentOutcome> Outcomes { get; }

        public bool AllAccepted
        {
            get
            {
                foreach (var outcome in Outcomes)
                {
                    if (!outcome.Accepted) return false;
                }
                return true;
            }
        }

        public int AcceptedCount
        {
            get
            {
                var count = 0;
                foreach (var outcome in Outcomes)
                {
                    if (outcome.Accepted) count++;
                }
                return count;
            }
        }

        public int RejectedCount => Outcomes.Count - AcceptedCount;
    }
}
