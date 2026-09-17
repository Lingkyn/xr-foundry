using System;
using System.Linq;
using NUnit.Framework;
using Lingkyn.Locomotion.Core;

namespace Lingkyn.Locomotion.Core.Editor.Tests
{
    public sealed class LocomotionCoreContractTests
    {
        private static readonly AnchorId Spawn = AnchorId.Parse("spawn.point.a");
        private static readonly AnchorId Overlook = AnchorId.Parse("overlook.b");

        // ----- mode and anchor identity (LC-01, LC-02) -----

        [Test]
        public void ModeIdCanonicalizesCaseForDeclaredModes()
        {
            var id = ModeId.TryCreate("Teleport");
            Assert.That(id.Succeeded, Is.True, id.Message);
            Assert.That(id.Value.Value, Is.EqualTo("teleport"));
            Assert.That(id.Code, Is.Empty);
            Assert.That(ModeId.TryCreate("SNAP_TURN").Value, Is.EqualTo(ModeId.SnapTurn));
            Assert.That(ModeId.TryCreate("Smooth_Turn").Value, Is.EqualTo(ModeId.SmoothTurn));
            Assert.That(ModeId.TryCreate("CONTINUOUS_MOVE").Value, Is.EqualTo(ModeId.ContinuousMove));
            Assert.That(id.Value.ToString(), Is.EqualTo("teleport"));
        }

        [Test]
        public void ModeIdRejectsEmptyWhitespaceAndMalformedShapesWithIdentityMalformed()
        {
            foreach (var text in new[] { null, "", " ", "snap turn", "teleport ", "\tteleport", ".leading", "-leading", "/leading", "trailing.", "trailing/", "double..dot", "ünïcode", "has:colon", new string('a', 129) })
            {
                var result = ModeId.TryCreate(text);
                Assert.That(result.Succeeded, Is.False, text ?? "<null>");
                Assert.That(result.Code, Is.EqualTo(LocomotionFailure.IdentityMalformed), text ?? "<null>");
                Assert.That(result.Message, Is.Not.Empty, text ?? "<null>");
            }
        }

        [Test]
        public void ModeIdRejectsTextNotInTheClosedSet()
        {
            foreach (var text in new[] { "jump", "dash", "fly", "teleports", "snap-turn" })
            {
                var result = ModeId.TryCreate(text);
                Assert.That(result.Succeeded, Is.False, text);
                Assert.That(result.Code, Is.EqualTo(LocomotionFailure.IdentityMalformed), text);
                Assert.That(result.Message, Does.Contain("not a declared locomotion mode"), text);
            }
        }

        [Test]
        public void ModeIdsWithTheSameTextAreEqualValues()
        {
            var first = ModeId.Parse("teleport");
            var second = ModeId.Parse("TELEPORT");
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first != ModeId.SnapTurn, Is.True);
            Assert.That(first.CompareTo(ModeId.SnapTurn), Is.Not.EqualTo(0));
        }

        [Test]
        public void AnchorIdSharesCanonicalizationRulesWithModeId()
        {
            Assert.That(AnchorId.TryCreate("Hand.Left").Value.Value, Is.EqualTo("hand.left"));
            foreach (var text in new[] { null, "", " ", "hand left", "hand.left ", ".leading", "trailing.", "double..dot" })
            {
                var result = AnchorId.TryCreate(text);
                Assert.That(result.Succeeded, Is.False, text ?? "<null>");
                Assert.That(result.Code, Is.EqualTo(LocomotionFailure.IdentityMalformed), text ?? "<null>");
            }
            Assert.That(AnchorId.Parse("spawn"), Is.EqualTo(AnchorId.Parse("SPAWN")));
            Assert.That(AnchorId.Parse("a"), Is.Not.EqualTo(AnchorId.Parse("b")));
        }

        [Test]
        public void AnchorIdAcceptsArbitraryWellFormedText()
        {
            var sameTextAsMode = AnchorId.TryCreate("teleport");
            Assert.That(sameTextAsMode.Succeeded, Is.True, "An anchor id is an open vocabulary and may share text with a mode.");

            var custom = AnchorId.TryCreate("custom.spawn.zone.7");
            Assert.That(custom.Succeeded, Is.True, custom.Message);
            Assert.That(ModeId.TryCreate("custom.spawn.zone.7").Succeeded, Is.False, "The same text is not a declared mode, showing ModeId's set is closed while AnchorId's is open.");
        }

        [Test]
        public void ParseThrowsWithTheFailureMessageForMalformedIdentity()
        {
            var thrown = Assert.Throws<ArgumentException>(() => AnchorId.Parse("bad id"));
            Assert.That(thrown.Message, Does.Contain("whitespace"));
            Assert.Throws<ArgumentException>(() => ModeId.Parse(""));
            Assert.Throws<ArgumentException>(() => ModeId.Parse("dash"));
            Assert.Throws<ArgumentException>(() => AnchorId.Parse("."));
        }

        // ----- comfort policy declaration (LC-03) -----

        [Test]
        public void ComfortOptionsDeclareClosedKindsWithRangeOrValueSet()
        {
            Assert.That(ComfortOptions.VignetteEnabledOption.Kind, Is.EqualTo(ComfortOptionKind.Bool));
            Assert.That(ComfortOptions.VignetteEnabledOption.DefaultValue, Is.EqualTo(ComfortOptionValue.Bool(false)));

            Assert.That(ComfortOptions.VignetteIntensityOption.Kind, Is.EqualTo(ComfortOptionKind.FloatRange));
            Assert.That(ComfortOptions.VignetteIntensityOption.Minimum, Is.EqualTo(0f));
            Assert.That(ComfortOptions.VignetteIntensityOption.Maximum, Is.EqualTo(1f));

            Assert.That(ComfortOptions.TurnModeOption.Kind, Is.EqualTo(ComfortOptionKind.Enumerated));
            Assert.That(ComfortOptions.TurnModeOption.Values, Is.EqualTo(new[] { "snap", "smooth" }));
            Assert.That(ComfortOptions.TurnModeOption.DefaultValue, Is.EqualTo(ComfortOptionValue.Enumerated("snap")));

            Assert.That(ComfortOptions.TurnIncrementDegreesOption.Kind, Is.EqualTo(ComfortOptionKind.DiscreteDegrees));
            Assert.That(ComfortOptions.TurnIncrementDegreesOption.DiscreteValues, Is.EqualTo(new[] { 15f, 30f, 45f, 60f, 90f }));
            Assert.That(ComfortOptions.TurnIncrementDegreesOption.DefaultValue, Is.EqualTo(ComfortOptionValue.DiscreteDegrees(45f)));

            Assert.That(ComfortOptions.MovementSpeedOption.Kind, Is.EqualTo(ComfortOptionKind.FloatRange));
            Assert.That(ComfortOptions.MovementSpeedOption.Minimum, Is.EqualTo(0f));
            Assert.That(ComfortOptions.MovementSpeedOption.Maximum, Is.EqualTo(5f));

            Assert.That(ComfortOptions.PostureOption.Kind, Is.EqualTo(ComfortOptionKind.Enumerated));
            Assert.That(ComfortOptions.PostureOption.Values, Is.EqualTo(new[] { "seated", "standing" }));
            Assert.That(ComfortOptions.PostureOption.DefaultValue, Is.EqualTo(ComfortOptionValue.Enumerated("standing")));

            Assert.That(Enum.GetValues(typeof(ComfortOptionKind)).Length, Is.EqualTo(4));
        }

        [Test]
        public void ComfortOptionContractConstructionRejectsInvalidDeclarations()
        {
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.FloatRange("x", 1f, 0f, 0.5f));
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.FloatRange("x", 0f, 1f, 2f));
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.FloatRange("x", float.NaN, 1f, 0f));
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.DiscreteDegrees("x", new float[0], 1f));
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.DiscreteDegrees("x", new[] { 1f, 1f }, 1f));
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.DiscreteDegrees("x", new[] { 1f, 2f }, 3f));
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.Enumerated("x", new string[0], "a"));
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.Enumerated("x", new[] { "a", "a" }, "a"));
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.Enumerated("x", new[] { "a", "" }, "a"));
            Assert.Throws<ArgumentException>(() => ComfortOptionContract.Enumerated("x", new[] { "a" }, "b"));
        }

        // ----- fail-closed option rejection (LC-04, LC-05, LC-06) -----

        [Test]
        public void SetComfortOptionOnUnknownNameFailsClosedWithOptionUnknown()
        {
            var policy = ComfortPolicy.Default();
            var malformed = policy.TrySet("bad name!", ComfortOptionValue.Bool(true));
            Assert.That(malformed.Succeeded, Is.False);
            Assert.That(malformed.Code, Is.EqualTo(LocomotionFailure.IdentityMalformed));

            var unknown = policy.TrySet("warp.factor", ComfortOptionValue.Bool(true));
            Assert.That(unknown.Succeeded, Is.False);
            Assert.That(unknown.Code, Is.EqualTo(LocomotionFailure.OptionUnknown));
            Assert.That(unknown.Value, Is.Null);
            Assert.That(policy.Equals(ComfortPolicy.Default()), Is.True, "Rejected sets never change the policy.");
        }

        [Test]
        public void SetComfortOptionWithWrongKindFailsClosedWithKindMismatch()
        {
            var policy = ComfortPolicy.Default();
            Assert.That(policy.TrySet(ComfortOptions.VignetteEnabledName, ComfortOptionValue.FloatRange(1f)).Code, Is.EqualTo(LocomotionFailure.OptionKindMismatch));
            Assert.That(policy.TrySet(ComfortOptions.MovementSpeedName, ComfortOptionValue.Bool(true)).Code, Is.EqualTo(LocomotionFailure.OptionKindMismatch));
            Assert.That(policy.TrySet(ComfortOptions.TurnModeName, ComfortOptionValue.FloatRange(1f)).Code, Is.EqualTo(LocomotionFailure.OptionKindMismatch));
            Assert.That(policy.TrySet(ComfortOptions.TurnIncrementDegreesName, ComfortOptionValue.Enumerated("45")).Code, Is.EqualTo(LocomotionFailure.OptionKindMismatch));
        }

        [Test]
        public void SetComfortOptionOutOfRangeOrUndeclaredFailsClosedWithoutChangingPolicy()
        {
            var policy = ComfortPolicy.Default();
            var before = policy.ToString();

            var outOfRange = policy.TrySet(ComfortOptions.VignetteIntensityName, ComfortOptionValue.FloatRange(1.5f));
            Assert.That(outOfRange.Code, Is.EqualTo(LocomotionFailure.OptionOutOfRange));
            Assert.That(outOfRange.Value, Is.Null);

            var negative = policy.TrySet(ComfortOptions.MovementSpeedName, ComfortOptionValue.FloatRange(-1f));
            Assert.That(negative.Code, Is.EqualTo(LocomotionFailure.OptionOutOfRange));

            var notFinite = policy.TrySet(ComfortOptions.VignetteIntensityName, ComfortOptionValue.FloatRange(float.NaN));
            Assert.That(notFinite.Code, Is.EqualTo(LocomotionFailure.OptionOutOfRange));

            Assert.That(policy.ToString(), Is.EqualTo(before));
        }

        [Test]
        public void DiscreteTurnIncrementRejectsUndeclaredDegreeValue()
        {
            var policy = ComfortPolicy.Default();
            var rejected = policy.TrySet(ComfortOptions.TurnIncrementDegreesName, ComfortOptionValue.DiscreteDegrees(50f));
            Assert.That(rejected.Code, Is.EqualTo(LocomotionFailure.OptionOutOfRange));

            var accepted = policy.TrySet(ComfortOptions.TurnIncrementDegreesName, ComfortOptionValue.DiscreteDegrees(60f));
            Assert.That(accepted.Succeeded, Is.True, accepted.Message);
            Assert.That(accepted.Value.TurnIncrementDegrees, Is.EqualTo(60f));
        }

        [Test]
        public void EnumeratedTurnModeAndPostureRejectUndeclaredChoice()
        {
            var policy = ComfortPolicy.Default();
            Assert.That(policy.TrySet(ComfortOptions.TurnModeName, ComfortOptionValue.Enumerated("diagonal")).Code, Is.EqualTo(LocomotionFailure.OptionOutOfRange));
            Assert.That(policy.TrySet(ComfortOptions.PostureName, ComfortOptionValue.Enumerated("floating")).Code, Is.EqualTo(LocomotionFailure.OptionOutOfRange));

            var smooth = policy.TrySet(ComfortOptions.TurnModeName, ComfortOptionValue.Enumerated("smooth"));
            Assert.That(smooth.Succeeded, Is.True, smooth.Message);
            Assert.That(smooth.Value.TurnMode, Is.EqualTo(TurnMode.Smooth));

            var seated = policy.TrySet(ComfortOptions.PostureName, ComfortOptionValue.Enumerated("seated"));
            Assert.That(seated.Value.Posture, Is.EqualTo(Posture.Seated));
        }

        // ----- teleport (LC-07) -----

        [Test]
        public void TeleportToRegisteredAnchorUpdatesCurrentAnchor()
        {
            var state = Prepared();
            var teleported = state.Apply(new TeleportIntent(Spawn));
            Assert.That(teleported.Succeeded, Is.True, teleported.Message);
            Assert.That(teleported.Value.CurrentAnchor, Is.EqualTo(Spawn));
            Assert.That(state.CurrentAnchor, Is.Null, "The prior state is left intact.");
        }

        [Test]
        public void TeleportToUnregisteredAnchorFailsWithAnchorUnknown()
        {
            var state = Prepared();
            var result = state.Apply(new TeleportIntent(Overlook));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LocomotionFailure.AnchorUnknown));
            Assert.That(result.Value, Is.Null);
            Assert.That(state.CurrentAnchor, Is.Null);
        }

        // ----- turn (LC-08) -----

        [Test]
        public void TurnAppliesThePolicysDeclaredIncrementInTheRequestedDirection()
        {
            var state = LocomotionState.Initial(ComfortPolicy.Default());
            var right = state.Apply(new TurnIntent(ModeId.SnapTurn, TurnDirection.Right));
            Assert.That(right.Succeeded, Is.True, right.Message);
            Assert.That(right.Value.HeadingDegrees, Is.EqualTo(45f));

            var left = state.Apply(new TurnIntent(ModeId.SnapTurn, TurnDirection.Left));
            Assert.That(left.Value.HeadingDegrees, Is.EqualTo(315f), "Turning left from zero wraps to 360 minus the increment.");

            var twice = right.Value.Apply(new TurnIntent(ModeId.SnapTurn, TurnDirection.Right));
            Assert.That(twice.Value.HeadingDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void TurnWithModeThePolicyDoesNotSelectFailsWithModeDisabled()
        {
            var state = LocomotionState.Initial(ComfortPolicy.Default());
            var rejected = state.Apply(new TurnIntent(ModeId.SmoothTurn, TurnDirection.Right));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(LocomotionFailure.ModeDisabled));
            Assert.That(rejected.Value, Is.Null);
            Assert.That(state.HeadingDegrees, Is.EqualTo(0f));

            var smoothState = state.Apply(new SetComfortOptionIntent(ComfortOptions.TurnModeName, ComfortOptionValue.Enumerated("smooth"))).Value;
            Assert.That(smoothState.Apply(new TurnIntent(ModeId.SnapTurn, TurnDirection.Right)).Code, Is.EqualTo(LocomotionFailure.ModeDisabled));
            Assert.That(smoothState.Apply(new TurnIntent(ModeId.SmoothTurn, TurnDirection.Right)).Succeeded, Is.True);
        }

        [Test]
        public void TurnIntentConstructionRejectsANonTurnMode()
        {
            Assert.Throws<ArgumentException>(() => new TurnIntent(ModeId.Teleport, TurnDirection.Right));
            Assert.Throws<ArgumentException>(() => new TurnIntent(ModeId.ContinuousMove, TurnDirection.Left));
        }

        // ----- move (LC-09) -----

        [Test]
        public void MoveAccumulatesPlanarOffsetByPolicySpeedAndTickDuration()
        {
            var state = LocomotionState.Initial(ComfortPolicy.Default());
            var first = state.Apply(new MoveIntent(new PlanarOffset(1f, 0f), 2f));
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.Value.Offset, Is.EqualTo(new PlanarOffset(3f, 0f)), "1.5 m/s for 2 seconds along x is 3 meters.");

            var second = first.Value.Apply(new MoveIntent(new PlanarOffset(0f, 1f), 1f));
            Assert.That(second.Value.Offset, Is.EqualTo(new PlanarOffset(3f, 1.5f)));
            Assert.That(state.Offset, Is.EqualTo(PlanarOffset.Zero), "The prior state is left intact.");
        }

        [Test]
        public void MoveIsRejectedWithPostureMismatchWhenPostureIsSeated()
        {
            var seated = LocomotionState.Initial(ComfortPolicy.Default())
                .Apply(new SetComfortOptionIntent(ComfortOptions.PostureName, ComfortOptionValue.Enumerated("seated"))).Value;

            var result = seated.Apply(new MoveIntent(new PlanarOffset(1f, 0f), 1f));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LocomotionFailure.PostureMismatch));
            Assert.That(result.Value, Is.Null);
            Assert.That(seated.Offset, Is.EqualTo(PlanarOffset.Zero));
            Assert.That(seated.Apply(new TurnIntent(ModeId.SnapTurn, TurnDirection.Right)).Succeeded, Is.True, "Posture only gates continuous movement.");
        }

        // ----- state immutability (LC-10) -----

        [Test]
        public void AcceptedIntentProducesNewStateAndLeavesPriorIntact()
        {
            var state = LocomotionState.Initial(ComfortPolicy.Default());
            var next = state.Apply(new RegisterAnchorIntent(Spawn));
            Assert.That(next.Succeeded, Is.True, next.Message);
            Assert.That(next.Value.IsAnchorRegistered(Spawn), Is.True);
            Assert.That(state.IsAnchorRegistered(Spawn), Is.False);
            Assert.That(state.RegisteredAnchors, Is.Empty);
        }

        [Test]
        public void StateIsImmutableAcrossApplications()
        {
            var initial = LocomotionState.Initial(ComfortPolicy.Default());
            var initialFingerprint = initial.Fingerprint();

            var later = initial.ApplyAll(Sequence()).State;

            Assert.That(initial.Fingerprint(), Is.EqualTo(initialFingerprint));
            Assert.That(initial.CurrentAnchor, Is.Null);
            Assert.That(initial.HeadingDegrees, Is.EqualTo(0f));
            Assert.That(initial.Offset, Is.EqualTo(PlanarOffset.Zero));
            Assert.That(initial.RegisteredAnchors, Is.Empty);
            Assert.That(later, Is.Not.EqualTo(initial));
            var view = initial.RegisteredAnchors;
            Assert.That(view, Is.Empty, "Enumerated views reflect the immutable state they were read from.");
        }

        [Test]
        public void RejectedIntentLeavesPriorStateIntactInsideASequence()
        {
            var state = LocomotionState.Initial(ComfortPolicy.Default());
            var result = state.ApplyAll(new LocomotionIntent[]
            {
                new RegisterAnchorIntent(Spawn),
                new TeleportIntent(Overlook),
                new TeleportIntent(Spawn),
                new SetComfortOptionIntent(ComfortOptions.MovementSpeedName, ComfortOptionValue.FloatRange(9f)),
                new SetComfortOptionIntent(ComfortOptions.MovementSpeedName, ComfortOptionValue.FloatRange(2f)),
            });

            Assert.That(result.AllAccepted, Is.False);
            Assert.That(result.AcceptedCount, Is.EqualTo(3));
            Assert.That(result.RejectedCount, Is.EqualTo(2));
            Assert.That(result.Outcomes.Select(item => item.Code), Is.EqualTo(new[] { "", LocomotionFailure.AnchorUnknown, "", LocomotionFailure.OptionOutOfRange, "" }));
            Assert.That(result.State.CurrentAnchor, Is.EqualTo(Spawn));
            Assert.That(result.State.Policy.MovementSpeed, Is.EqualTo(2f));

            var expected = state.Apply(new RegisterAnchorIntent(Spawn)).Value
                .Apply(new TeleportIntent(Spawn)).Value
                .Apply(new SetComfortOptionIntent(ComfortOptions.MovementSpeedName, ComfortOptionValue.FloatRange(2f))).Value;
            Assert.That(result.State, Is.EqualTo(expected), "Rejected intents contribute nothing to the final state.");
        }

        // ----- deterministic replay (LC-11) -----

        [Test]
        public void SameIntentSequenceProducesEqualStateAndFingerprint()
        {
            var first = LocomotionState.Initial(ComfortPolicy.Default()).ApplyAll(Sequence());
            var second = LocomotionState.Initial(ComfortPolicy.Default()).ApplyAll(Sequence());

            Assert.That(first.AllAccepted, Is.True, Describe(first));
            Assert.That(first.State, Is.EqualTo(second.State));
            Assert.That(first.State.Fingerprint(), Is.EqualTo(second.State.Fingerprint()));
            Assert.That(first.State.GetHashCode(), Is.EqualTo(second.State.GetHashCode()));
            Assert.That(ReferenceEquals(first.State, second.State), Is.False);

            var reordered = LocomotionState.Initial(ComfortPolicy.Default()).ApplyAll(Enumerable.Reverse(Sequence()).ToList());
            Assert.That(reordered.State, Is.Not.EqualTo(first.State));
        }

        // ----- state enumeration (LC-12) -----

        [Test]
        public void StateEnumeratesActiveModesPolicyAnchorHeadingAndOffset()
        {
            var state = LocomotionState.Initial(ComfortPolicy.Default()).ApplyAll(Sequence()).State;

            Assert.That(state.ActiveModes, Is.EqualTo(new[] { ModeId.Teleport, ModeId.ContinuousMove, ModeId.SnapTurn }));
            Assert.That(state.Policy.MovementSpeed, Is.EqualTo(3f));
            Assert.That(state.CurrentAnchor, Is.EqualTo(Overlook));
            Assert.That(state.HeadingDegrees, Is.EqualTo(90f));
            Assert.That(state.Offset, Is.EqualTo(new PlanarOffset(3f, 3f)));
            Assert.That(state.RegisteredAnchors, Is.EqualTo(new[] { Overlook, Spawn }), "Anchors enumerate in canonical order.");
        }

        [Test]
        public void InitialStateStartsAtZeroHeadingZeroOffsetNoAnchorWithDefaultPolicy()
        {
            var state = LocomotionState.Initial(ComfortPolicy.Default());
            Assert.That(state.CurrentAnchor, Is.Null);
            Assert.That(state.HeadingDegrees, Is.EqualTo(0f));
            Assert.That(state.Offset, Is.EqualTo(PlanarOffset.Zero));
            Assert.That(state.RegisteredAnchors, Is.Empty);
            Assert.That(state.ActiveModes, Is.EqualTo(new[] { ModeId.Teleport, ModeId.ContinuousMove, ModeId.SnapTurn }));
            Assert.That(state, Is.EqualTo(LocomotionState.Initial(ComfortPolicy.Default())));
        }

        // ----- structured results (LC-13, LC-14) -----

        [Test]
        public void AllSevenFailureCodesAreStableAcrossFailureSites()
        {
            Assert.That(LocomotionFailure.IdentityMalformed, Is.EqualTo("identity.malformed"));
            Assert.That(LocomotionFailure.OptionUnknown, Is.EqualTo("option.unknown"));
            Assert.That(LocomotionFailure.OptionKindMismatch, Is.EqualTo("option.kind.mismatch"));
            Assert.That(LocomotionFailure.OptionOutOfRange, Is.EqualTo("option.out_of_range"));
            Assert.That(LocomotionFailure.AnchorUnknown, Is.EqualTo("anchor.unknown"));
            Assert.That(LocomotionFailure.ModeDisabled, Is.EqualTo("mode.disabled"));
            Assert.That(LocomotionFailure.PostureMismatch, Is.EqualTo("posture.mismatch"));

            var state = Prepared();
            var seated = state.Apply(new SetComfortOptionIntent(ComfortOptions.PostureName, ComfortOptionValue.Enumerated("seated"))).Value;
            foreach (var result in new[]
            {
                ModeId.TryCreate("bad id").As<object>(),
                ComfortPolicy.Default().TrySet("warp.factor", ComfortOptionValue.Bool(true)).As<object>(),
                ComfortPolicy.Default().TrySet(ComfortOptions.VignetteEnabledName, ComfortOptionValue.FloatRange(1f)).As<object>(),
                ComfortPolicy.Default().TrySet(ComfortOptions.MovementSpeedName, ComfortOptionValue.FloatRange(99f)).As<object>(),
                state.Apply(new TeleportIntent(Overlook)).As<object>(),
                state.Apply(new TurnIntent(ModeId.SmoothTurn, TurnDirection.Right)).As<object>(),
                seated.Apply(new MoveIntent(new PlanarOffset(1f, 0f), 1f)).As<object>(),
            })
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Message, Is.Not.Empty);
                Assert.That(result.Value, Is.Null);
            }
        }

        [Test]
        public void LocomotionResultCarriesCodeAndMessageAndOkHasEmptyCode()
        {
            var ok = LocomotionResult<int>.Ok(3);
            Assert.That(ok.Succeeded, Is.True);
            Assert.That(ok.Value, Is.EqualTo(3));
            Assert.That(ok.Code, Is.Empty);
            Assert.That(ok.Message, Is.Empty);

            var failed = LocomotionResult<int>.Fail(LocomotionFailure.AnchorUnknown, "no anchor");
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Code, Is.EqualTo("anchor.unknown"));
            Assert.That(failed.Message, Is.EqualTo("no anchor"));
            var retyped = failed.As<string>();
            Assert.That(retyped.Code, Is.EqualTo("anchor.unknown"));
            Assert.Throws<ArgumentException>(() => LocomotionResult<int>.Fail("", "codeless"));
            Assert.Throws<InvalidOperationException>(() => ok.As<string>());
        }

        [Test]
        public void ValidIntentSequencePassesCleanWithEveryOutcomeAccepted()
        {
            var result = LocomotionState.Initial(ComfortPolicy.Default()).ApplyAll(Sequence());
            Assert.That(result.AllAccepted, Is.True, Describe(result));
            Assert.That(result.RejectedCount, Is.EqualTo(0));
            Assert.That(result.Outcomes.Count, Is.EqualTo(Sequence().Count));
            Assert.That(result.Outcomes.All(item => item.Code == string.Empty && item.Message == string.Empty), Is.True);
            Assert.That(result.Outcomes.Select(item => item.Index), Is.EqualTo(Enumerable.Range(0, Sequence().Count)));
        }

        // ----- anchor registry (outside the contract's numbered clauses) -----

        [Test]
        public void RegisterAnchorRejectsDuplicateRegistration()
        {
            var state = LocomotionState.Initial(ComfortPolicy.Default()).Apply(new RegisterAnchorIntent(Spawn)).Value;
            var duplicate = state.Apply(new RegisterAnchorIntent(Spawn));
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Code, Is.EqualTo(LocomotionFailure.AnchorDuplicate));
            Assert.That(state.RegisteredAnchors.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnregisterAnchorClearsCurrentAnchorWhenItWasTheRegisteredOne()
        {
            var registered = LocomotionState.Initial(ComfortPolicy.Default()).Apply(new RegisterAnchorIntent(Spawn)).Value;
            var teleported = registered.Apply(new TeleportIntent(Spawn)).Value;
            Assert.That(teleported.CurrentAnchor, Is.EqualTo(Spawn));

            var unregistered = teleported.Apply(new UnregisterAnchorIntent(Spawn));
            Assert.That(unregistered.Succeeded, Is.True, unregistered.Message);
            Assert.That(unregistered.Value.CurrentAnchor, Is.Null);
            Assert.That(unregistered.Value.IsAnchorRegistered(Spawn), Is.False);

            Assert.That(teleported.Apply(new UnregisterAnchorIntent(Overlook)).Code, Is.EqualTo(LocomotionFailure.AnchorUnknown));
        }

        // ----- helpers -----

        private static LocomotionState Prepared()
        {
            var result = LocomotionState.Initial(ComfortPolicy.Default()).Apply(new RegisterAnchorIntent(Spawn));
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static System.Collections.Generic.List<LocomotionIntent> Sequence() => new System.Collections.Generic.List<LocomotionIntent>
        {
            new RegisterAnchorIntent(Spawn),
            new RegisterAnchorIntent(Overlook),
            new TeleportIntent(Spawn),
            new TurnIntent(ModeId.SnapTurn, TurnDirection.Right),
            new TurnIntent(ModeId.SnapTurn, TurnDirection.Right),
            new MoveIntent(new PlanarOffset(1f, 0f), 2f),
            new SetComfortOptionIntent(ComfortOptions.MovementSpeedName, ComfortOptionValue.FloatRange(3f)),
            new MoveIntent(new PlanarOffset(0f, 1f), 1f),
            new TeleportIntent(Overlook),
        };

        private static string Describe(LocomotionSequenceResult result) =>
            string.Join("\n", result.Outcomes.Select(item => item.Index + " " + item.Intent.Describe() + ": " + (item.Accepted ? "ok" : item.Code + " " + item.Message)));
    }
}
