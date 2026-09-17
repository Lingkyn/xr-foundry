using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lingkyn.Audio.Core;

namespace Lingkyn.Audio.Core.Editor.Tests
{
    public sealed class AudioCoreContractTests
    {
        private static readonly BusId Master = BusId.Parse("master");
        private static readonly BusId Music = BusId.Parse("music");
        private static readonly BusId Effects = BusId.Parse("effects");
        private static readonly ParameterId Volume = ParameterId.Parse("volume");
        private static readonly ParameterId Muted = ParameterId.Parse("muted");
        private static readonly ParameterId Mode = ParameterId.Parse("mode");
        private static readonly SnapshotId Calm = SnapshotId.Parse("calm");
        private static readonly SnapshotId Tense = SnapshotId.Parse("tense");
        private static readonly AudioEventId Click = AudioEventId.Parse("ui.click");
        private static readonly AudioEventId Hum = AudioEventId.Parse("ambience.hum");
        private static readonly AnchorId LeftHand = AnchorId.Parse("hand.left");
        private static readonly AnchorId RightHand = AnchorId.Parse("hand.right");

        // ----- identity -----

        [Test]
        public void EventIdCanonicalizesCaseAndKeepsSeparators()
        {
            var id = AudioEventId.TryCreate("UI.Click/Confirm_2-b");
            Assert.That(id.Succeeded, Is.True, id.Message);
            Assert.That(id.Value.Value, Is.EqualTo("ui.click/confirm_2-b"));
            Assert.That(id.Code, Is.Empty);
            Assert.That(id.Value.ToString(), Is.EqualTo("ui.click/confirm_2-b"));
        }

        [Test]
        public void EventIdRejectsEmptyWhitespaceAndMalformedShapesWithIdentityMalformed()
        {
            foreach (var text in new[] { null, "", " ", "ui click", "ui.click ", "\tui", ".leading", "-leading", "/leading", "trailing.", "trailing/", "double..dot", "mixed./sep", "ünïcode", "has:colon", new string('a', 129) })
            {
                var result = AudioEventId.TryCreate(text);
                Assert.That(result.Succeeded, Is.False, text ?? "<null>");
                Assert.That(result.Code, Is.EqualTo(AudioFailure.IdentityMalformed), text ?? "<null>");
                Assert.That(result.Message, Is.Not.Empty, text ?? "<null>");
            }
            Assert.That(AudioEventId.TryCreate(new string('a', 128)).Succeeded, Is.True);
        }

        [Test]
        public void EventIdsWithTheSameTextAreEqualValues()
        {
            var first = AudioEventId.Parse("ui.click");
            var second = AudioEventId.Parse("UI.CLICK");
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first != AudioEventId.Parse("ui.clack"), Is.True);
            Assert.That(first.CompareTo(AudioEventId.Parse("ui.clack")), Is.GreaterThan(0));
        }

        [Test]
        public void BusSnapshotAnchorAndParameterIdsShareTheIdentityRules()
        {
            Assert.That(BusId.TryCreate("Master").Value.Value, Is.EqualTo("master"));
            Assert.That(SnapshotId.TryCreate("Calm/Night").Value.Value, Is.EqualTo("calm/night"));
            Assert.That(AnchorId.TryCreate("Hand.Left").Value.Value, Is.EqualTo("hand.left"));
            Assert.That(ParameterId.TryCreate("Low_Pass").Value.Value, Is.EqualTo("low_pass"));
            Assert.That(BusId.TryCreate("").Code, Is.EqualTo(AudioFailure.IdentityMalformed));
            Assert.That(SnapshotId.TryCreate(" calm").Code, Is.EqualTo(AudioFailure.IdentityMalformed));
            Assert.That(AnchorId.TryCreate("hand left").Code, Is.EqualTo(AudioFailure.IdentityMalformed));
            Assert.That(ParameterId.TryCreate("..").Code, Is.EqualTo(AudioFailure.IdentityMalformed));
            Assert.That(BusId.Parse("music"), Is.EqualTo(BusId.Parse("MUSIC")));
            Assert.That(SnapshotId.Parse("calm"), Is.EqualTo(SnapshotId.Parse("Calm")));
            Assert.That(AnchorId.Parse("a"), Is.Not.EqualTo(AnchorId.Parse("b")));
            Assert.That(ParameterId.Parse("volume"), Is.EqualTo(ParameterId.Parse("Volume")));
        }

        [Test]
        public void ParseThrowsWithTheFailureMessageForMalformedIdentity()
        {
            var thrown = Assert.Throws<System.ArgumentException>(() => AudioEventId.Parse("bad id"));
            Assert.That(thrown.Message, Does.Contain("whitespace"));
            Assert.Throws<System.ArgumentException>(() => BusId.Parse(""));
            Assert.Throws<System.ArgumentException>(() => SnapshotId.Parse("."));
            Assert.Throws<System.ArgumentException>(() => AnchorId.Parse("x y"));
            Assert.Throws<System.ArgumentException>(() => ParameterId.Parse("a//b"));
        }

        // ----- mix graph -----

        [Test]
        public void MixGraphRequiresExactlyOneRootBus()
        {
            var noRoot = new MixGraphBuilder().Bus(Music, Master).Build();
            Assert.That(noRoot.Succeeded, Is.False);
            Assert.That(noRoot.Code, Is.EqualTo(AudioFailure.GraphRootMissing));

            var twoRoots = new MixGraphBuilder().RootBus(Master).RootBus(Music).Build();
            Assert.That(twoRoots.Code, Is.EqualTo(AudioFailure.GraphRootDuplicate));

            var graph = new MixGraphBuilder().RootBus(Master).Build();
            Assert.That(graph.Succeeded, Is.True, graph.Message);
            Assert.That(graph.Value.Root.Id, Is.EqualTo(Master));
            Assert.That(graph.Value.Root.IsRoot, Is.True);
        }

        [Test]
        public void MixGraphRejectsDuplicateBusIds()
        {
            var result = new MixGraphBuilder().RootBus(Master).Bus(Music, Master).Bus(BusId.Parse("MUSIC"), Master).Build();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(AudioFailure.GraphBusDuplicate));
            Assert.That(result.Message, Does.Contain("music"));
        }

        [Test]
        public void MixGraphRejectsDuplicateSnapshotIds()
        {
            var result = new MixGraphBuilder().RootBus(Master).Snapshot(Calm, Master).Snapshot(Calm).Build();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(AudioFailure.GraphSnapshotDuplicate));
        }

        [Test]
        public void MixGraphRejectsSnapshotReferencingBusOutsideGraph()
        {
            var result = new MixGraphBuilder().RootBus(Master).Snapshot(Calm, Master, Effects).Build();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(AudioFailure.BusUnknown));
            Assert.That(result.Message, Does.Contain("calm").And.Contain("effects"));
        }

        [Test]
        public void MixGraphRejectsBusWithUnknownParentOrUnreachableFromRoot()
        {
            var orphan = new MixGraphBuilder().RootBus(Master).Bus(Music, Effects).Build();
            Assert.That(orphan.Code, Is.EqualTo(AudioFailure.BusUnknown));

            var cycle = new MixGraphBuilder().RootBus(Master).Bus(Music, Effects).Bus(Effects, Music).Build();
            Assert.That(cycle.Succeeded, Is.False);
            Assert.That(cycle.Code, Is.EqualTo(AudioFailure.GraphBusUnreachable));

            var chain = new MixGraphBuilder().RootBus(Master).Bus(Music, Master).Bus(Effects, Music).Build();
            Assert.That(chain.Succeeded, Is.True, chain.Message);
            Assert.That(chain.Value.BusCount, Is.EqualTo(3));
        }

        [Test]
        public void MixGraphRejectsDuplicateParametersOnOneBusAndDuplicateOrUnroutedEvents()
        {
            var duplicateParameter = new MixGraphBuilder()
                .RootBus(Master, ParameterContract.Float(Volume, 0f, 1f, 1f), ParameterContract.Float(ParameterId.Parse("VOLUME"), 0f, 1f, 1f))
                .Build();
            Assert.That(duplicateParameter.Code, Is.EqualTo(AudioFailure.GraphParameterDuplicate));

            var duplicateEvent = new MixGraphBuilder().RootBus(Master).Event(Click, Master).Event(Click, Master).Build();
            Assert.That(duplicateEvent.Code, Is.EqualTo(AudioFailure.GraphEventDuplicate));

            var unroutedEvent = new MixGraphBuilder().RootBus(Master).Event(Click, Effects).Build();
            Assert.That(unroutedEvent.Code, Is.EqualTo(AudioFailure.BusUnknown));
        }

        [Test]
        public void MixGraphIsImmutableAfterBuild()
        {
            var builder = new MixGraphBuilder().RootBus(Master).Snapshot(Calm, Master).Event(Click, Master);
            var graph = builder.Build().Value;
            builder.Bus(Music, Master).Snapshot(Tense).Event(Hum, Master);

            Assert.That(graph.BusCount, Is.EqualTo(1));
            Assert.That(graph.SnapshotCount, Is.EqualTo(1));
            Assert.That(graph.EventCount, Is.EqualTo(1));
            Assert.That(graph.TryGetBus(Music, out _), Is.False);
            Assert.That(graph.TryGetSnapshot(Tense, out _), Is.False);
            Assert.That(graph.TryGetEvent(Hum, out _), Is.False);
            Assert.That(builder.Build().Value.BusCount, Is.EqualTo(2));
        }

        [Test]
        public void MixGraphResolvesParametersWithBusUnknownAndParameterUnknown()
        {
            var graph = Graph();
            var resolved = graph.ResolveParameter(Music, Volume);
            Assert.That(resolved.Succeeded, Is.True, resolved.Message);
            Assert.That(resolved.Value.Kind, Is.EqualTo(ParameterKind.Float));
            Assert.That(graph.ResolveParameter(BusId.Parse("voice"), Volume).Code, Is.EqualTo(AudioFailure.BusUnknown));
            Assert.That(graph.ResolveParameter(Music, ParameterId.Parse("pitch")).Code, Is.EqualTo(AudioFailure.ParameterUnknown));
            Assert.That(graph.TryGetSnapshot(Calm, out var calm), Is.True);
            Assert.That(calm.Buses, Is.EquivalentTo(new[] { Music, Effects }));
            Assert.That(graph.TryGetEvent(Click, out var click), Is.True);
            Assert.That(click.Bus, Is.EqualTo(Effects));
        }

        // ----- parameter contracts -----

        [Test]
        public void ParameterContractsDeclareClosedKindsWithRangeOrValueSet()
        {
            var volume = ParameterContract.Float(Volume, -80f, 0f, -6f);
            Assert.That(volume.Kind, Is.EqualTo(ParameterKind.Float));
            Assert.That(volume.Minimum, Is.EqualTo(-80f));
            Assert.That(volume.Maximum, Is.EqualTo(0f));
            Assert.That(volume.DefaultValue, Is.EqualTo(ParameterValue.Float(-6f)));

            var muted = ParameterContract.Bool(Muted, false);
            Assert.That(muted.Kind, Is.EqualTo(ParameterKind.Bool));
            Assert.That(muted.DefaultValue, Is.EqualTo(ParameterValue.Bool(false)));

            var mode = ParameterContract.Enumerated(Mode, new[] { "indoor", "outdoor" }, "indoor");
            Assert.That(mode.Kind, Is.EqualTo(ParameterKind.Enumerated));
            Assert.That(mode.Values, Is.EqualTo(new[] { "indoor", "outdoor" }));
            Assert.That(mode.DefaultValue, Is.EqualTo(ParameterValue.Enumerated("indoor")));
            Assert.That(System.Enum.GetValues(typeof(ParameterKind)).Length, Is.EqualTo(3));
        }

        [Test]
        public void ParameterContractRejectsInvalidDeclarations()
        {
            Assert.That(ParameterContract.TryFloat(Volume, 1f, 0f, 0.5f).Code, Is.EqualTo(AudioFailure.ParameterContractInvalid));
            Assert.That(ParameterContract.TryFloat(Volume, 0f, 1f, 2f).Code, Is.EqualTo(AudioFailure.ParameterContractInvalid));
            Assert.That(ParameterContract.TryFloat(Volume, float.NaN, 1f, 0f).Code, Is.EqualTo(AudioFailure.ParameterContractInvalid));
            Assert.That(ParameterContract.TryEnumerated(Mode, new string[0], "a").Code, Is.EqualTo(AudioFailure.ParameterContractInvalid));
            Assert.That(ParameterContract.TryEnumerated(Mode, new[] { "a", "a" }, "a").Code, Is.EqualTo(AudioFailure.ParameterContractInvalid));
            Assert.That(ParameterContract.TryEnumerated(Mode, new[] { "a", "" }, "a").Code, Is.EqualTo(AudioFailure.ParameterContractInvalid));
            Assert.That(ParameterContract.TryEnumerated(Mode, new[] { "a" }, "b").Code, Is.EqualTo(AudioFailure.ParameterContractInvalid));
            Assert.Throws<System.ArgumentException>(() => ParameterContract.Float(Volume, 1f, 0f, 0f));
        }

        [Test]
        public void FloatContractAcceptsInRangeAndRejectsOutOfRange()
        {
            var contract = ParameterContract.Float(Volume, 0f, 1f, 1f);
            Assert.That(contract.Validate(ParameterValue.Float(0f)).Succeeded, Is.True);
            Assert.That(contract.Validate(ParameterValue.Float(0.25f)).Succeeded, Is.True);
            Assert.That(contract.Validate(ParameterValue.Float(1f)).Succeeded, Is.True);
            foreach (var value in new[] { -0.01f, 1.01f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var result = contract.Validate(ParameterValue.Float(value));
                Assert.That(result.Succeeded, Is.False, value.ToString());
                Assert.That(result.Code, Is.EqualTo(AudioFailure.ParameterOutOfRange), value.ToString());
            }
        }

        [Test]
        public void ContractRejectsWrongKindAsKindMismatchBeforeRangeCheck()
        {
            var flag = ParameterContract.Bool(Muted, false);
            Assert.That(flag.Validate(ParameterValue.Bool(true)).Succeeded, Is.True);
            Assert.That(flag.Validate(ParameterValue.Float(1f)).Code, Is.EqualTo(AudioFailure.ParameterKindMismatch));
            Assert.That(flag.Validate(ParameterValue.Enumerated("true")).Code, Is.EqualTo(AudioFailure.ParameterKindMismatch));

            var range = ParameterContract.Float(Volume, 0f, 1f, 0f);
            Assert.That(range.Validate(ParameterValue.Bool(true)).Code, Is.EqualTo(AudioFailure.ParameterKindMismatch));
            Assert.That(range.Validate(ParameterValue.Enumerated("0.5")).Code, Is.EqualTo(AudioFailure.ParameterKindMismatch));
        }

        [Test]
        public void EnumeratedContractRejectsUndeclaredValueAsOutOfRange()
        {
            var mode = ParameterContract.Enumerated(Mode, new[] { "indoor", "outdoor" }, "indoor");
            Assert.That(mode.Validate(ParameterValue.Enumerated("outdoor")).Succeeded, Is.True);
            var undeclared = mode.Validate(ParameterValue.Enumerated("underwater"));
            Assert.That(undeclared.Code, Is.EqualTo(AudioFailure.ParameterOutOfRange));
            Assert.That(undeclared.Message, Does.Contain("underwater").And.Contain("indoor"));
            Assert.That(mode.Validate(ParameterValue.Enumerated("Indoor")).Code, Is.EqualTo(AudioFailure.ParameterOutOfRange), "Enumerated values are exact, not case-folded.");
            Assert.That(mode.Validate(ParameterValue.Enumerated("")).Code, Is.EqualTo(AudioFailure.ParameterOutOfRange));
            Assert.That(mode.Validate(ParameterValue.Float(1f)).Code, Is.EqualTo(AudioFailure.ParameterKindMismatch));
        }

        [Test]
        public void SetParameterOnUnknownBusOrParameterFailsClosedWithoutChangingState()
        {
            var state = AudioState.Initial(Graph());
            var before = state.Fingerprint();

            var unknownBus = state.Apply(new SetParameterIntent(BusId.Parse("voice"), Volume, ParameterValue.Float(0.5f)));
            Assert.That(unknownBus.Succeeded, Is.False);
            Assert.That(unknownBus.Code, Is.EqualTo(AudioFailure.BusUnknown));

            var unknownParameter = state.Apply(new SetParameterIntent(Music, ParameterId.Parse("pitch"), ParameterValue.Float(0.5f)));
            Assert.That(unknownParameter.Code, Is.EqualTo(AudioFailure.ParameterUnknown));

            Assert.That(state.Fingerprint(), Is.EqualTo(before));
            Assert.That(state.TryGetParameter(Music, Volume, out var volume), Is.True);
            Assert.That(volume, Is.EqualTo(ParameterValue.Float(1f)));
        }

        [Test]
        public void SetParameterKindMismatchAndOutOfRangeLeaveStateIntact()
        {
            var state = AudioState.Initial(Graph());
            var before = state.Fingerprint();

            var mismatch = state.Apply(new SetParameterIntent(Music, Volume, ParameterValue.Bool(true)));
            Assert.That(mismatch.Code, Is.EqualTo(AudioFailure.ParameterKindMismatch));
            var outOfRange = state.Apply(new SetParameterIntent(Music, Volume, ParameterValue.Float(2f)));
            Assert.That(outOfRange.Code, Is.EqualTo(AudioFailure.ParameterOutOfRange));
            var undeclared = state.Apply(new SetParameterIntent(Effects, Mode, ParameterValue.Enumerated("space")));
            Assert.That(undeclared.Code, Is.EqualTo(AudioFailure.ParameterOutOfRange));

            Assert.That(state.Fingerprint(), Is.EqualTo(before));
            Assert.That(state.TryGetParameter(Music, Volume, out var volume), Is.True);
            Assert.That(volume, Is.EqualTo(ParameterValue.Float(1f)));
            Assert.That(state.TryGetParameter(Effects, Mode, out var mode), Is.True);
            Assert.That(mode, Is.EqualTo(ParameterValue.Enumerated("indoor")));
        }

        [Test]
        public void SetParameterAcceptedValueIsVisibleInTheNewStateOnly()
        {
            var state = AudioState.Initial(Graph());
            var next = state.Apply(new SetParameterIntent(Music, Volume, ParameterValue.Float(0.25f)));
            Assert.That(next.Succeeded, Is.True, next.Message);
            Assert.That(next.Value.TryGetParameter(Music, Volume, out var updated), Is.True);
            Assert.That(updated, Is.EqualTo(ParameterValue.Float(0.25f)));
            Assert.That(state.TryGetParameter(Music, Volume, out var original), Is.True);
            Assert.That(original, Is.EqualTo(ParameterValue.Float(1f)));

            var flagged = next.Value.Apply(new SetParameterIntent(Effects, Muted, ParameterValue.Bool(true))).Value;
            var moded = flagged.Apply(new SetParameterIntent(Effects, Mode, ParameterValue.Enumerated("outdoor"))).Value;
            Assert.That(moded.ParameterValues.Select(item => item.Value.ToString()), Is.EquivalentTo(new[] { "0.25", "true", "outdoor", "1" }));
        }

        // ----- anchors and attachment -----

        [Test]
        public void AttachRequiresRegisteredAnchorAndActiveEvent()
        {
            var state = AudioState.Initial(Graph());
            var inactive = state.Apply(new AttachEventIntent(Click, LeftHand));
            Assert.That(inactive.Code, Is.EqualTo(AudioFailure.EventInactive));

            var posted = state.Apply(new PostEventIntent(Click)).Value;
            var unknownAnchor = posted.Apply(new AttachEventIntent(Click, LeftHand));
            Assert.That(unknownAnchor.Succeeded, Is.False);
            Assert.That(unknownAnchor.Code, Is.EqualTo(AudioFailure.AnchorUnknown));
            Assert.That(posted.Attachments, Is.Empty);

            var registered = posted.Apply(new RegisterAnchorIntent(LeftHand)).Value;
            var attached = registered.Apply(new AttachEventIntent(Click, LeftHand));
            Assert.That(attached.Succeeded, Is.True, attached.Message);
            Assert.That(attached.Value.TryGetAttachment(Click, out var anchor), Is.True);
            Assert.That(anchor, Is.EqualTo(LeftHand));
            Assert.That(attached.Value.Apply(new AttachEventIntent(AudioEventId.Parse("ghost"), LeftHand)).Code, Is.EqualTo(AudioFailure.EventUnknown));
        }

        [Test]
        public void AttachMoveAndDetachAreDistinctIntents()
        {
            var state = Prepared();
            Assert.That(state.Apply(new MoveEventIntent(Click, RightHand)).Code, Is.EqualTo(AudioFailure.EventUnattached), "Move needs a prior attachment.");
            Assert.That(state.Apply(new DetachEventIntent(Click)).Code, Is.EqualTo(AudioFailure.EventUnattached));

            var attached = state.Apply(new AttachEventIntent(Click, LeftHand)).Value;
            Assert.That(attached.Apply(new AttachEventIntent(Click, RightHand)).Code, Is.EqualTo(AudioFailure.EventAttached), "Attach does not silently re-parent.");

            var moved = attached.Apply(new MoveEventIntent(Click, RightHand));
            Assert.That(moved.Succeeded, Is.True, moved.Message);
            Assert.That(moved.Value.TryGetAttachment(Click, out var anchor), Is.True);
            Assert.That(anchor, Is.EqualTo(RightHand));
            Assert.That(moved.Value.Apply(new MoveEventIntent(Click, AnchorId.Parse("head"))).Code, Is.EqualTo(AudioFailure.AnchorUnknown));

            var detached = moved.Value.Apply(new DetachEventIntent(Click));
            Assert.That(detached.Succeeded, Is.True, detached.Message);
            Assert.That(detached.Value.TryGetAttachment(Click, out _), Is.False);
            Assert.That(detached.Value.IsActive(Click), Is.True, "Detaching does not stop the event.");
        }

        [Test]
        public void EventIsAttachedToExactlyOneAnchorOrUnattached()
        {
            var attached = Prepared().Apply(new AttachEventIntent(Click, LeftHand)).Value;
            Assert.That(attached.Attachments.Count(item => item.Event == Click), Is.EqualTo(1));

            var moved = attached.Apply(new MoveEventIntent(Click, RightHand)).Value;
            Assert.That(moved.Attachments.Count(item => item.Event == Click), Is.EqualTo(1));
            Assert.That(moved.Attachments.Single(item => item.Event == Click).Anchor, Is.EqualTo(RightHand));

            var stopped = moved.Apply(new StopEventIntent(Click)).Value;
            Assert.That(stopped.IsActive(Click), Is.False);
            Assert.That(stopped.Attachments.Any(item => item.Event == Click), Is.False, "A stopped event holds no attachment.");
            Assert.That(stopped.HasAnchor(RightHand), Is.True, "Stopping an event keeps the anchor registered.");
        }

        [Test]
        public void UnregisterAnchorDetachesItsEventsAndUnknownAnchorIsRejected()
        {
            var state = Prepared()
                .Apply(new PostEventIntent(Hum)).Value
                .Apply(new AttachEventIntent(Click, LeftHand)).Value
                .Apply(new AttachEventIntent(Hum, RightHand)).Value;
            Assert.That(state.Apply(new UnregisterAnchorIntent(AnchorId.Parse("head"))).Code, Is.EqualTo(AudioFailure.AnchorUnknown));
            Assert.That(state.Apply(new RegisterAnchorIntent(LeftHand)).Code, Is.EqualTo(AudioFailure.AnchorDuplicate));

            var removed = state.Apply(new UnregisterAnchorIntent(LeftHand)).Value;
            Assert.That(removed.HasAnchor(LeftHand), Is.False);
            Assert.That(removed.TryGetAttachment(Click, out _), Is.False);
            Assert.That(removed.IsActive(Click), Is.True);
            Assert.That(removed.TryGetAttachment(Hum, out var humAnchor), Is.True);
            Assert.That(humAnchor, Is.EqualTo(RightHand));
        }

        // ----- deterministic state -----

        [Test]
        public void SameIntentSequenceProducesEqualStateAndFingerprint()
        {
            var graph = Graph();
            var first = AudioState.Initial(graph).ApplyAll(Sequence());
            var second = AudioState.Initial(graph).ApplyAll(Sequence());

            Assert.That(first.AllAccepted, Is.True, Describe(first));
            Assert.That(first.State, Is.EqualTo(second.State));
            Assert.That(first.State.Fingerprint(), Is.EqualTo(second.State.Fingerprint()));
            Assert.That(first.State.GetHashCode(), Is.EqualTo(second.State.GetHashCode()));
            Assert.That(ReferenceEquals(first.State, second.State), Is.False);

            var reordered = AudioState.Initial(graph).ApplyAll(Enumerable.Reverse(Sequence()).ToList());
            Assert.That(reordered.State, Is.Not.EqualTo(first.State));
        }

        [Test]
        public void StateIsImmutableAcrossApplications()
        {
            var initial = AudioState.Initial(Graph());
            var initialFingerprint = initial.Fingerprint();
            var events = initial.ActiveEvents;
            var parameters = initial.ParameterValues;

            var later = initial.ApplyAll(Sequence()).State;

            Assert.That(initial.Fingerprint(), Is.EqualTo(initialFingerprint));
            Assert.That(initial.ActiveEvents, Is.Empty);
            Assert.That(initial.CurrentSnapshot, Is.Null);
            Assert.That(initial.Anchors, Is.Empty);
            Assert.That(initial.Attachments, Is.Empty);
            Assert.That(events, Is.Empty);
            Assert.That(parameters.Single(item => item.Bus == Music && item.Parameter == Volume).Value, Is.EqualTo(ParameterValue.Float(1f)));
            Assert.That(later.ActiveEvents, Is.Not.Empty);
            Assert.That(later, Is.Not.EqualTo(initial));
            var view = (ICollection<AudioEventId>)later.ActiveEvents;
            view.Clear();
            Assert.That(later.ActiveEvents, Is.Not.Empty, "Enumerated views are copies; clearing one does not touch the state.");
        }

        [Test]
        public void RejectedIntentLeavesPriorStateIntactInsideASequence()
        {
            var state = AudioState.Initial(Graph());
            var result = state.ApplyAll(new AudioIntent[]
            {
                new PostEventIntent(Click),
                new SetParameterIntent(Music, Volume, ParameterValue.Float(5f)),
                new TransitionSnapshotIntent(SnapshotId.Parse("missing"), 1f),
                new PostEventIntent(Click),
                new SetParameterIntent(Music, Volume, ParameterValue.Float(0.5f)),
            });

            Assert.That(result.AllAccepted, Is.False);
            Assert.That(result.AcceptedCount, Is.EqualTo(2));
            Assert.That(result.RejectedCount, Is.EqualTo(3));
            Assert.That(result.Outcomes.Select(item => item.Code), Is.EqualTo(new[] { "", AudioFailure.ParameterOutOfRange, AudioFailure.SnapshotUnknown, AudioFailure.EventActive, "" }));
            Assert.That(result.Outcomes[1].Index, Is.EqualTo(1));
            Assert.That(result.Outcomes[1].Intent, Is.InstanceOf<SetParameterIntent>());
            Assert.That(result.State.TryGetParameter(Music, Volume, out var volume), Is.True);
            Assert.That(volume, Is.EqualTo(ParameterValue.Float(0.5f)));
            Assert.That(result.State.CurrentSnapshot, Is.Null);
            Assert.That(result.State.ActiveEvents, Is.EqualTo(new[] { Click }));

            var expected = state.Apply(new PostEventIntent(Click)).Value.Apply(new SetParameterIntent(Music, Volume, ParameterValue.Float(0.5f))).Value;
            Assert.That(result.State, Is.EqualTo(expected), "Rejected intents contribute nothing to the final state.");
        }

        [Test]
        public void StateEnumeratesActiveEventsSnapshotBusParametersAndAttachments()
        {
            var state = AudioState.Initial(Graph()).ApplyAll(Sequence()).State;

            Assert.That(state.ActiveEvents, Is.EqualTo(new[] { Hum, Click }), "Active events enumerate in canonical order.");
            Assert.That(state.CurrentSnapshot, Is.EqualTo(Tense));
            Assert.That(state.ParameterValues.Select(item => item.Bus + "/" + item.Parameter + "=" + item.Value),
                Is.EqualTo(new[] { "effects/mode=outdoor", "effects/muted=false", "master/volume=1", "music/volume=0.5" }));
            Assert.That(state.Attachments.Select(item => item.Event + "->" + item.Anchor), Is.EqualTo(new[] { "ambience.hum->hand.right", "ui.click->hand.left" }));
            Assert.That(state.Anchors, Is.EqualTo(new[] { LeftHand, RightHand }));
            Assert.That(state.Fingerprint(), Does.Contain("snapshot[tense]").And.Contain("music|volume=0.5").And.Contain("ui.click->hand.left"));
        }

        [Test]
        public void InitialStateCarriesDeclaredDefaultsAndNoSnapshot()
        {
            var state = AudioState.Initial(Graph());
            Assert.That(state.ActiveEvents, Is.Empty);
            Assert.That(state.CurrentSnapshot, Is.Null);
            Assert.That(state.Attachments, Is.Empty);
            Assert.That(state.Anchors, Is.Empty);
            Assert.That(state.ParameterValues.Count, Is.EqualTo(4));
            Assert.That(state.TryGetParameter(Master, Volume, out var master), Is.True);
            Assert.That(master, Is.EqualTo(ParameterValue.Float(1f)));
            Assert.That(state.TryGetParameter(Effects, Muted, out var muted), Is.True);
            Assert.That(muted, Is.EqualTo(ParameterValue.Bool(false)));
            Assert.That(state.TryGetParameter(Effects, Volume, out _), Is.False, "Only declared parameters exist.");
            Assert.That(state, Is.EqualTo(AudioState.Initial(state.Graph)));
        }

        // ----- structured results -----

        [Test]
        public void UnknownEventBusSnapshotParameterAndAnchorReportStableCodes()
        {
            var state = Prepared();
            Assert.That(state.Apply(new PostEventIntent(AudioEventId.Parse("ghost"))).Code, Is.EqualTo(AudioFailure.EventUnknown));
            Assert.That(state.Apply(new StopEventIntent(AudioEventId.Parse("ghost"))).Code, Is.EqualTo(AudioFailure.EventUnknown));
            Assert.That(state.Apply(new SetParameterIntent(BusId.Parse("voice"), Volume, ParameterValue.Float(0f))).Code, Is.EqualTo(AudioFailure.BusUnknown));
            Assert.That(state.Apply(new TransitionSnapshotIntent(SnapshotId.Parse("storm"), 0f)).Code, Is.EqualTo(AudioFailure.SnapshotUnknown));
            Assert.That(state.Apply(new SetParameterIntent(Music, ParameterId.Parse("pitch"), ParameterValue.Float(0f))).Code, Is.EqualTo(AudioFailure.ParameterUnknown));
            Assert.That(state.Apply(new AttachEventIntent(Click, AnchorId.Parse("head"))).Code, Is.EqualTo(AudioFailure.AnchorUnknown));
            foreach (var failure in new[]
            {
                state.Apply(new PostEventIntent(AudioEventId.Parse("ghost"))),
                state.Apply(new AttachEventIntent(Click, AnchorId.Parse("head"))),
            })
            {
                Assert.That(failure.Succeeded, Is.False);
                Assert.That(failure.Message, Is.Not.Empty);
                Assert.That(failure.Value, Is.Null);
            }
        }

        [Test]
        public void ValidIntentSequencePassesCleanWithEveryOutcomeAccepted()
        {
            var result = AudioState.Initial(Graph()).ApplyAll(Sequence());
            Assert.That(result.AllAccepted, Is.True, Describe(result));
            Assert.That(result.RejectedCount, Is.EqualTo(0));
            Assert.That(result.Outcomes.Count, Is.EqualTo(Sequence().Count));
            Assert.That(result.Outcomes.All(item => item.Code == string.Empty && item.Message == string.Empty), Is.True);
            Assert.That(result.Outcomes.Select(item => item.Index), Is.EqualTo(Enumerable.Range(0, Sequence().Count)));
        }

        [Test]
        public void AudioResultCarriesCodeAndMessageAndOkHasEmptyCode()
        {
            var ok = AudioResult<int>.Ok(3);
            Assert.That(ok.Succeeded, Is.True);
            Assert.That(ok.Value, Is.EqualTo(3));
            Assert.That(ok.Code, Is.Empty);
            Assert.That(ok.Message, Is.Empty);

            var failed = AudioResult<int>.Fail(AudioFailure.BusUnknown, "no bus");
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Code, Is.EqualTo("bus.unknown"));
            Assert.That(failed.Message, Is.EqualTo("no bus"));
            var retyped = failed.As<string>();
            Assert.That(retyped.Code, Is.EqualTo("bus.unknown"));
            Assert.Throws<System.ArgumentException>(() => AudioResult<int>.Fail("", "codeless"));
            Assert.Throws<System.InvalidOperationException>(() => ok.As<string>());
        }

        [Test]
        public void SnapshotTransitionRequiresNonNegativeDurationAndRecordsTheSnapshot()
        {
            var state = AudioState.Initial(Graph());
            Assert.That(state.Apply(new TransitionSnapshotIntent(Calm, -0.1f)).Code, Is.EqualTo(AudioFailure.SnapshotDurationInvalid));
            Assert.That(state.Apply(new TransitionSnapshotIntent(Calm, float.NaN)).Code, Is.EqualTo(AudioFailure.SnapshotDurationInvalid));
            Assert.That(state.CurrentSnapshot, Is.Null);

            var instant = state.Apply(new TransitionSnapshotIntent(Calm, 0f));
            Assert.That(instant.Succeeded, Is.True, instant.Message);
            Assert.That(instant.Value.CurrentSnapshot, Is.EqualTo(Calm));
            var timed = instant.Value.Apply(new TransitionSnapshotIntent(Tense, 2.5f));
            Assert.That(timed.Value.CurrentSnapshot, Is.EqualTo(Tense));
            Assert.That(new TransitionSnapshotIntent(Tense, 2.5f).Describe(), Does.Contain("2.5"));
        }

        [Test]
        public void PostingActiveEventOrStoppingInactiveEventIsRejected()
        {
            var state = AudioState.Initial(Graph());
            Assert.That(state.Apply(new StopEventIntent(Click)).Code, Is.EqualTo(AudioFailure.EventInactive));
            var posted = state.Apply(new PostEventIntent(Click)).Value;
            Assert.That(posted.Apply(new PostEventIntent(Click)).Code, Is.EqualTo(AudioFailure.EventActive));
            var stopped = posted.Apply(new StopEventIntent(Click)).Value;
            Assert.That(stopped.IsActive(Click), Is.False);
            Assert.That(stopped, Is.EqualTo(state));
            Assert.That(stopped.Apply(new PostEventIntent(Click)).Succeeded, Is.True, "An event can be posted again after it stopped.");
        }

        // ----- helpers -----

        private static MixGraph Graph()
        {
            var result = new MixGraphBuilder()
                .RootBus(Master, ParameterContract.Float(Volume, 0f, 1f, 1f))
                .Bus(Music, Master, ParameterContract.Float(Volume, 0f, 1f, 1f))
                .Bus(Effects, Master,
                    ParameterContract.Bool(Muted, false),
                    ParameterContract.Enumerated(Mode, new[] { "indoor", "outdoor" }, "indoor"))
                .Snapshot(Calm, Music, Effects)
                .Snapshot(Tense, Music)
                .Event(Click, Effects)
                .Event(Hum, Music)
                .Build();
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static AudioState Prepared()
        {
            var result = AudioState.Initial(Graph()).ApplyAll(new AudioIntent[]
            {
                new RegisterAnchorIntent(LeftHand),
                new RegisterAnchorIntent(RightHand),
                new PostEventIntent(Click),
            });
            Assert.That(result.AllAccepted, Is.True, Describe(result));
            return result.State;
        }

        private static List<AudioIntent> Sequence() => new List<AudioIntent>
        {
            new RegisterAnchorIntent(LeftHand),
            new RegisterAnchorIntent(RightHand),
            new PostEventIntent(Click),
            new PostEventIntent(Hum),
            new AttachEventIntent(Click, LeftHand),
            new AttachEventIntent(Hum, LeftHand),
            new MoveEventIntent(Hum, RightHand),
            new SetParameterIntent(Music, Volume, ParameterValue.Float(0.5f)),
            new SetParameterIntent(Effects, Mode, ParameterValue.Enumerated("outdoor")),
            new TransitionSnapshotIntent(Calm, 1f),
            new TransitionSnapshotIntent(Tense, 0.5f),
        };

        private static string Describe(AudioSequenceResult result) =>
            string.Join("\n", result.Outcomes.Select(item => item.Index + " " + item.Intent.Describe() + ": " + (item.Accepted ? "ok" : item.Code + " " + item.Message)));
    }
}
