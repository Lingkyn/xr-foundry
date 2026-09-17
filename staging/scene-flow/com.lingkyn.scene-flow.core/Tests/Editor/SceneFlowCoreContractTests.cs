using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lingkyn.SceneFlow.Core;

namespace Lingkyn.SceneFlow.Core.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked
    // against docs/standards/scene-flow/verification-contract.md and coverage-map.json.
    public sealed class SceneFlowCoreContractTests
    {
        private static readonly SceneSetId Menu = SceneSetId.Parse("menu");
        private static readonly SceneSetId Level1 = SceneSetId.Parse("level1");
        private static readonly SceneSetId Level2 = SceneSetId.Parse("level2");
        private static readonly SceneSetId Fallback = SceneSetId.Parse("fallback");
        private static readonly SceneId MenuMain = SceneId.Parse("menu.main");
        private static readonly SceneId Level1Play = SceneId.Parse("level1.play");
        private static readonly SceneId Level1Audio = SceneId.Parse("level1.audio");
        private static readonly SceneId Level2Play = SceneId.Parse("level2.play");
        private static readonly SceneId FallbackError = SceneId.Parse("fallback.error");

        // ----- identity -----

        [Test]
        public void SceneSetIdAndSceneIdCanonicalizeByTrimmingSurroundingWhitespace()
        {
            var set = SceneSetId.TryCreate("  menu  ");
            Assert.That(set.Succeeded, Is.True, set.Message);
            Assert.That(set.Value.Value, Is.EqualTo("menu"));
            Assert.That(set.Code, Is.Empty);

            var scene = SceneId.TryCreate("\tlevel1.play\t");
            Assert.That(scene.Succeeded, Is.True, scene.Message);
            Assert.That(scene.Value.Value, Is.EqualTo("level1.play"));
            Assert.That(scene.Value.ToString(), Is.EqualTo("level1.play"));
        }

        [Test]
        public void IdentityRejectsNullEmptyWhitespaceAndControlCharactersWithIdentityMalformed()
        {
            foreach (var text in new[] { null, "", " ", "   ", "menu room", "menu\troom", "menu\nroom", "menu\u0007room" })
            {
                var set = SceneSetId.TryCreate(text);
                Assert.That(set.Succeeded, Is.False, text ?? "<null>");
                Assert.That(set.Code, Is.EqualTo(SceneFlowFailure.IdentityMalformed), text ?? "<null>");
                Assert.That(set.Message, Is.Not.Empty, text ?? "<null>");

                var scene = SceneId.TryCreate(text);
                Assert.That(scene.Succeeded, Is.False, text ?? "<null>");
                Assert.That(scene.Code, Is.EqualTo(SceneFlowFailure.IdentityMalformed), text ?? "<null>");
            }
        }

        [Test]
        public void IdentitiesWithTheSameCanonicalTextAreEqualValues()
        {
            var first = SceneSetId.Parse("level1");
            var second = SceneSetId.Parse("  level1  ");
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first != SceneSetId.Parse("level2"), Is.True);
            Assert.That(first.CompareTo(SceneSetId.Parse("level2")), Is.LessThan(0));

            var sceneA = SceneId.Parse("level1.play");
            var sceneB = SceneId.Parse(" level1.play");
            Assert.That(sceneA, Is.EqualTo(sceneB));
        }

        [Test]
        public void ParseThrowsWithTheFailureMessageForMalformedIdentity()
        {
            var thrown = Assert.Throws<System.ArgumentException>(() => SceneSetId.Parse("bad id"));
            Assert.That(thrown.Message, Does.Contain("whitespace"));
            Assert.Throws<System.ArgumentException>(() => SceneSetId.Parse(""));
            Assert.Throws<System.ArgumentException>(() => SceneId.Parse(null));
        }

        // ----- scene graph -----

        [Test]
        public void GraphRejectsDuplicateSetIdWithSetDuplicate()
        {
            var result = new SceneGraphBuilder()
                .Set(Menu, MenuMain, MenuMain)
                .Set(SceneSetId.Parse("menu"), MenuMain, MenuMain)
                .Build(Menu);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(SceneFlowFailure.SetDuplicate));
            Assert.That(result.Message, Does.Contain("menu"));
        }

        [Test]
        public void GraphRejectsSceneDeclaredTwiceWithinOneSetWithSceneDuplicate()
        {
            var result = new SceneGraphBuilder()
                .Set(Level1, Level1Play, Level1Play, Level1Play)
                .Build(Level1);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(SceneFlowFailure.SceneDuplicate));
        }

        [Test]
        public void GraphRequiresActiveSceneToBeADeclaredMemberWithActiveMissing()
        {
            var absent = new SceneGraphBuilder().Set(Menu, Level1Play, MenuMain).Build(Menu);
            Assert.That(absent.Code, Is.EqualTo(SceneFlowFailure.ActiveMissing));

            var ok = new SceneGraphBuilder().Set(Menu, MenuMain, MenuMain).Build(Menu);
            Assert.That(ok.Succeeded, Is.True, ok.Message);
            Assert.That(ok.Value.TryGetSet(Menu, out var set), Is.True);
            Assert.That(set.Active, Is.EqualTo(MenuMain));
        }

        [Test]
        public void GraphRejectsUndeclaredFallbackSetWithSetUnknown()
        {
            var result = new SceneGraphBuilder().Set(Menu, MenuMain, MenuMain).Build(Fallback);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(SceneFlowFailure.SetUnknown));
            Assert.That(result.Message, Does.Contain("fallback"));
        }

        [Test]
        public void GraphAllowsASceneToBeAMemberOfMoreThanOneSet()
        {
            var result = new SceneGraphBuilder()
                .Set(Level1, Level1Play, Level1Play, Level1Audio)
                .Set(Level2, Level2Play, Level2Play, Level1Audio)
                .Build(Level1);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.TryGetSet(Level1, out var level1), Is.True);
            Assert.That(result.Value.TryGetSet(Level2, out var level2), Is.True);
            Assert.That(level1.Contains(Level1Audio), Is.True);
            Assert.That(level2.Contains(Level1Audio), Is.True, "A persistent scene may belong to more than one set.");
        }

        // ----- transition options -----

        [Test]
        public void TransitionOptionsAcceptNonNegativeDurationsIncludingZero()
        {
            var options = TransitionOptions.TryCreate(0f, 2.5f, 0f);
            Assert.That(options.Succeeded, Is.True, options.Message);
            Assert.That(options.Value.FadeOut.IsZero, Is.True);
            Assert.That(options.Value.Hold.Seconds, Is.EqualTo(2.5f));
            Assert.That(options.Value.FadeIn.IsZero, Is.True);
        }

        [Test]
        public void TransitionOptionsRejectNegativeOrNonFiniteDurationBeforeAnyIntentIsApplied()
        {
            foreach (var invalid in new[] { -0.01f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.That(TransitionOptions.TryCreate(invalid, 1f, 1f).Code, Is.EqualTo(SceneFlowFailure.DurationInvalid), invalid.ToString());
                Assert.That(TransitionOptions.TryCreate(1f, invalid, 1f).Code, Is.EqualTo(SceneFlowFailure.DurationInvalid), invalid.ToString());
                Assert.That(TransitionOptions.TryCreate(1f, 1f, invalid).Code, Is.EqualTo(SceneFlowFailure.DurationInvalid), invalid.ToString());
            }
            Assert.Throws<System.ArgumentException>(() => TransitionOptions.Create(-1f, 0f, 0f));
        }

        [Test]
        public void DurationTryParseRejectsUnparseableTextWithDurationInvalid()
        {
            foreach (var text in new[] { null, "", "fast", "1.5x" })
            {
                var result = SceneFlowDuration.TryParse(text);
                Assert.That(result.Succeeded, Is.False, text ?? "<null>");
                Assert.That(result.Code, Is.EqualTo(SceneFlowFailure.DurationInvalid), text ?? "<null>");
            }
            var parsed = SceneFlowDuration.TryParse("1.25");
            Assert.That(parsed.Succeeded, Is.True, parsed.Message);
            Assert.That(parsed.Value.Seconds, Is.EqualTo(1.25f));
        }

        // ----- load / unload / switch intents -----

        [Test]
        public void LoadIntentAddsDeclaredSetAdditively()
        {
            var state = Initial();
            var loaded = state.Apply(new LoadSetIntent(Menu)).Value;
            loaded = Complete(loaded);
            Assert.That(loaded.LoadedSets, Is.EquivalentTo(new[] { Menu }));
            Assert.That(loaded.ActiveScene, Is.EqualTo(MenuMain));

            var alsoLevel1 = Complete(loaded.Apply(new LoadSetIntent(Level1)).Value);
            Assert.That(alsoLevel1.LoadedSets, Is.EquivalentTo(new[] { Menu, Level1 }), "Load is additive; the menu stays loaded.");
        }

        [Test]
        public void UnloadIntentRemovesALoadedSet()
        {
            var state = Complete(Initial().Apply(new LoadSetIntent(Menu)).Value);
            var unloaded = Complete(state.Apply(new UnloadSetIntent(Menu)).Value);
            Assert.That(unloaded.LoadedSets, Is.Empty);
        }

        [Test]
        public void SwitchIntentReplacesTheContentSetLeavingOtherSetsLoaded()
        {
            var state = Complete(Complete(Initial().Apply(new LoadSetIntent(Menu)).Value).Apply(new LoadSetIntent(Level1)).Value);
            Assert.That(state.LoadedSets, Is.EquivalentTo(new[] { Menu, Level1 }));

            var switched = Complete(state.Apply(new SwitchSetIntent(Level1, Level2)).Value);
            Assert.That(switched.LoadedSets, Is.EquivalentTo(new[] { Menu, Level2 }), "Menu is not involved in the switch and stays loaded.");
            Assert.That(switched.ActiveScene, Is.EqualTo(Level2Play));
        }

        [Test]
        public void DestinationIsLoadedBeforeOriginIsUnloadedDuringASwitch()
        {
            var state = Complete(Initial().Apply(new LoadSetIntent(Level1)).Value);
            var fadingOut = state.Apply(new SwitchSetIntent(Level1, Level2)).Value;
            var loading = fadingOut.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(loading.Phase, Is.EqualTo(TransitionPhase.Loading));
            Assert.That(loading.LoadedSets, Is.EquivalentTo(new[] { Level1 }), "Still only the origin is loaded while Level2's scenes are pending.");

            var holding = loading.Apply(new SceneLoadCompletedIntent(Level2Play)).Value;
            Assert.That(holding.Phase, Is.EqualTo(TransitionPhase.Holding));
            Assert.That(holding.LoadedSets, Is.EquivalentTo(new[] { Level1, Level2 }), "The destination is loaded before the origin is unloaded.");
            Assert.That(holding.LoadedSets.Count, Is.GreaterThan(0), "The loaded set count is never zero mid-switch.");

            var fadingIn = holding.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(fadingIn.LoadedSets, Is.EquivalentTo(new[] { Level2 }), "The origin is unloaded only after the destination is confirmed loaded.");
        }

        [Test]
        public void IntentNamingUndeclaredSetIsRejectedWithSetUnknown()
        {
            var ghost = SceneSetId.Parse("ghost");
            var state = Initial();
            Assert.That(state.Apply(new LoadSetIntent(ghost)).Code, Is.EqualTo(SceneFlowFailure.SetUnknown));
            Assert.That(state.Apply(new UnloadSetIntent(ghost)).Code, Is.EqualTo(SceneFlowFailure.SetUnknown));
            Assert.That(state.Apply(new SwitchSetIntent(ghost, Menu)).Code, Is.EqualTo(SceneFlowFailure.SetUnknown));
            var loadedMenu = Complete(state.Apply(new LoadSetIntent(Menu)).Value);
            Assert.That(loadedMenu.Apply(new SwitchSetIntent(Menu, ghost)).Code, Is.EqualTo(SceneFlowFailure.SetUnknown));
        }

        [Test]
        public void UnloadOrSwitchAwayFromASetThatIsNotLoadedIsRejectedWithSceneNotLoaded()
        {
            var state = Initial();
            Assert.That(state.Apply(new UnloadSetIntent(Menu)).Code, Is.EqualTo(SceneFlowFailure.SceneNotLoaded));
            Assert.That(state.Apply(new SwitchSetIntent(Menu, Level1)).Code, Is.EqualTo(SceneFlowFailure.SceneNotLoaded));
        }

        [Test]
        public void IntentArrivingWhileTransitionInProgressIsRejectedWithTransitionInProgress()
        {
            var midTransition = Initial().Apply(new LoadSetIntent(Menu)).Value;
            Assert.That(midTransition.Phase, Is.Not.EqualTo(TransitionPhase.Idle));

            Assert.That(midTransition.Apply(new LoadSetIntent(Level1)).Code, Is.EqualTo(SceneFlowFailure.TransitionInProgress));
            Assert.That(midTransition.Apply(new UnloadSetIntent(Menu)).Code, Is.EqualTo(SceneFlowFailure.TransitionInProgress));
            Assert.That(midTransition.Apply(new SwitchSetIntent(Menu, Level1)).Code, Is.EqualTo(SceneFlowFailure.TransitionInProgress));
        }

        // ----- transition state machine -----

        [Test]
        public void TransitionAdvancesThroughEveryPhaseAndBackToIdle()
        {
            var state = Initial();
            var fadingOut = state.Apply(new LoadSetIntent(Level1)).Value;
            Assert.That(fadingOut.Phase, Is.EqualTo(TransitionPhase.FadingOut));

            var loading = fadingOut.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(loading.Phase, Is.EqualTo(TransitionPhase.Loading));

            var stillLoading = loading.Apply(new SceneLoadCompletedIntent(Level1Play)).Value;
            Assert.That(stillLoading.Phase, Is.EqualTo(TransitionPhase.Loading), "Level1.audio has not reported yet.");

            var holding = stillLoading.Apply(new SceneLoadCompletedIntent(Level1Audio)).Value;
            Assert.That(holding.Phase, Is.EqualTo(TransitionPhase.Holding));

            var fadingIn = holding.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(fadingIn.Phase, Is.EqualTo(TransitionPhase.FadingIn));

            var idle = fadingIn.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(idle.Phase, Is.EqualTo(TransitionPhase.Idle));
            Assert.That(idle.LoadedSets, Is.EquivalentTo(new[] { Level1 }));
        }

        [Test]
        public void ZeroDurationPhaseCompletesOnTheTickThatEntersIt()
        {
            var zero = Options(0f, 0f, 0f);
            var afterLoad = SceneFlowState.Initial(Graph(), zero).Apply(new LoadSetIntent(Menu)).Value;
            Assert.That(afterLoad.Phase, Is.EqualTo(TransitionPhase.Loading),
                "fading_out has zero duration and completes on the tick it is entered, but loading is never duration-gated: it still waits for every destination scene to report.");

            var afterCompleted = afterLoad.Apply(new SceneLoadCompletedIntent(MenuMain)).Value;
            Assert.That(afterCompleted.Phase, Is.EqualTo(TransitionPhase.Idle),
                "holding and fading_in both have zero duration, so both complete on the tick loading finishes.");
            Assert.That(afterCompleted.LoadedSets, Is.EquivalentTo(new[] { Menu }));
            Assert.That(afterCompleted.ActiveScene, Is.EqualTo(MenuMain));
        }

        [Test]
        public void ElapsedTimeIntentIsIllegalOutsideFadingOutHoldingAndFadingIn()
        {
            var idle = Initial();
            Assert.That(idle.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Code, Is.EqualTo(SceneFlowFailure.TransitionIllegal));

            var loading = idle.Apply(new LoadSetIntent(Level1)).Value.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(loading.Phase, Is.EqualTo(TransitionPhase.Loading));
            Assert.That(loading.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(0.1f))).Code, Is.EqualTo(SceneFlowFailure.TransitionIllegal));
        }

        [Test]
        public void LoadCompletedCompletesLoadingOnlyWhenEveryDestinationSceneHasReported()
        {
            var loading = Initial().Apply(new LoadSetIntent(Level1)).Value.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(loading.PendingScenesRemaining, Is.EquivalentTo(new[] { Level1Play, Level1Audio }));

            var partial = loading.Apply(new SceneLoadCompletedIntent(Level1Play)).Value;
            Assert.That(partial.Phase, Is.EqualTo(TransitionPhase.Loading));
            Assert.That(partial.PendingScenesRemaining, Is.EquivalentTo(new[] { Level1Audio }));

            var complete = partial.Apply(new SceneLoadCompletedIntent(Level1Audio)).Value;
            Assert.That(complete.Phase, Is.EqualTo(TransitionPhase.Holding));
            Assert.That(complete.PendingScenesRemaining, Is.Empty);
        }

        [Test]
        public void LoadCompletedForASceneNotBeingLoadedIsTransitionIllegal()
        {
            var loading = Initial().Apply(new LoadSetIntent(Level1)).Value.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            var result = loading.Apply(new SceneLoadCompletedIntent(MenuMain));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(SceneFlowFailure.TransitionIllegal));

            var idle = Initial();
            Assert.That(idle.Apply(new SceneLoadCompletedIntent(Level1Play)).Code, Is.EqualTo(SceneFlowFailure.TransitionIllegal), "A completion while idle is also illegal.");
        }

        [Test]
        public void StateEnumeratesPhaseElapsedLoadedSetsLoadedScenesActiveSceneAndPendingDestination()
        {
            var state = Initial().Apply(new LoadSetIntent(Level1)).Value;
            Assert.That(state.Phase, Is.EqualTo(TransitionPhase.FadingOut));
            Assert.That(state.PhaseElapsedSeconds, Is.EqualTo(0f));
            Assert.That(state.PendingDestination, Is.EqualTo(Level1));
            Assert.That(state.PendingOrigin, Is.Null);

            var advanced = state.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(0.4f))).Value;
            Assert.That(advanced.Phase, Is.EqualTo(TransitionPhase.FadingOut));
            Assert.That(advanced.PhaseElapsedSeconds, Is.EqualTo(0.4f));

            var loading = advanced.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(0.6f))).Value;
            var afterFirst = loading.Apply(new SceneLoadCompletedIntent(Level1Play)).Value;
            var holding = afterFirst.Apply(new SceneLoadCompletedIntent(Level1Audio)).Value;
            Assert.That(holding.LoadedSets, Is.EquivalentTo(new[] { Level1 }));
            Assert.That(holding.LoadedScenes, Is.EquivalentTo(new[] { Level1Play, Level1Audio }));
            Assert.That(holding.ActiveScene, Is.Null, "Activation happens at the end of holding, not at its start.");

            var fadingIn = holding.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(fadingIn.ActiveScene, Is.EqualTo(Level1Play));
            Assert.That(fadingIn.PendingDestination, Is.EqualTo(Level1), "The pending destination is still readable during fading_in.");
        }

        // ----- immutability -----

        [Test]
        public void RejectedIntentLeavesPriorStateIntact()
        {
            var state = Complete(Initial().Apply(new LoadSetIntent(Menu)).Value);
            var before = state.Fingerprint();

            var rejected = state.Apply(new UnloadSetIntent(Level1));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(state.Fingerprint(), Is.EqualTo(before));
            Assert.That(state.LoadedSets, Is.EquivalentTo(new[] { Menu }));
        }

        [Test]
        public void AcceptedIntentProducesNewStateWhilePriorStateStaysReadableAndEqualToItself()
        {
            var state = Initial();
            var initialFingerprint = state.Fingerprint();

            var next = state.Apply(new LoadSetIntent(Menu)).Value;

            Assert.That(ReferenceEquals(state, next), Is.False);
            Assert.That(state.Fingerprint(), Is.EqualTo(initialFingerprint));
            Assert.That(state, Is.EqualTo(state), "A state is equal to itself after being read again.");
            Assert.That(state.Phase, Is.EqualTo(TransitionPhase.Idle));
            Assert.That(next.Phase, Is.Not.EqualTo(TransitionPhase.Idle));
        }

        // ----- error recovery -----

        [Test]
        public void LoadFailedDuringLoadingRecoversToTheFallbackSetFromTheSamePhase()
        {
            var loading = Initial().Apply(new LoadSetIntent(Level1)).Value.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(loading.Phase, Is.EqualTo(TransitionPhase.Loading));

            var result = loading.Apply(new SceneLoadFailedIntent(Level1Play, "asset missing"));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Code, Is.EqualTo(SceneFlowFailure.LoadFailed));
            var recovered = result.Value;
            Assert.That(recovered.Phase, Is.EqualTo(TransitionPhase.Loading), "Recovery starts from the same phase (loading), not back at fading_out.");
            Assert.That(recovered.PendingDestination, Is.EqualTo(Fallback));
            Assert.That(recovered.PendingScenesRemaining, Is.EquivalentTo(new[] { FallbackError }));
            Assert.That(recovered.LoadedSets, Is.Empty, "Level1 was never confirmed loaded, so nothing changed yet.");

            var settled = Complete(recovered);
            Assert.That(settled.LoadedSets, Is.EquivalentTo(new[] { Fallback }));
            Assert.That(settled.ActiveScene, Is.EqualTo(FallbackError));
        }

        [Test]
        public void LoadFailedWhileLoadingTheFallbackSetItselfNeverLoopsAndReturnsToIdle()
        {
            var loadingFallback = Initial().Apply(new LoadSetIntent(Fallback)).Value.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(1f))).Value;
            Assert.That(loadingFallback.Phase, Is.EqualTo(TransitionPhase.Loading));
            Assert.That(loadingFallback.PendingDestination, Is.EqualTo(Fallback));

            var result = loadingFallback.Apply(new SceneLoadFailedIntent(FallbackError, "asset missing"));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Code, Is.EqualTo(SceneFlowFailure.FallbackFailed));
            Assert.That(result.Value.Phase, Is.EqualTo(TransitionPhase.Idle));
            Assert.That(result.Value.LoadedSets, Is.Empty, "Nothing was loaded before the failed transition, so nothing is loaded after it.");
            Assert.That(result.Value.PendingDestination, Is.Null);

            Assert.That(result.Value.Apply(new SceneLoadFailedIntent(FallbackError, "again")).Code, Is.EqualTo(SceneFlowFailure.TransitionIllegal), "Idle accepts no load-failed intent, so recovery cannot loop.");
        }

        // ----- deterministic replay and structured results -----

        [Test]
        public void SameIntentSequenceProducesEqualStateAndFingerprint()
        {
            var graph = Graph();
            var options = Options();
            var first = SceneFlowState.Initial(graph, options).ApplyAll(Sequence());
            var second = SceneFlowState.Initial(graph, options).ApplyAll(Sequence());

            Assert.That(first.AllAccepted, Is.True, Describe(first));
            Assert.That(first.State, Is.EqualTo(second.State));
            Assert.That(first.State.Fingerprint(), Is.EqualTo(second.State.Fingerprint()));
            Assert.That(first.State.GetHashCode(), Is.EqualTo(second.State.GetHashCode()));
            Assert.That(ReferenceEquals(first.State, second.State), Is.False);
        }

        [Test]
        public void StructuredResultsCarryStableFailureCodeForEveryRejectionListedInTheContract()
        {
            var ghost = SceneSetId.Parse("ghost");
            var state = Initial();
            var codes = new[]
            {
                state.Apply(new UnloadSetIntent(Menu)).Code,
                state.Apply(new LoadSetIntent(ghost)).Code,
                Complete(state.Apply(new LoadSetIntent(Menu)).Value).Apply(new UnloadSetIntent(ghost)).Code,
            };
            Assert.That(codes, Is.EqualTo(new[] { SceneFlowFailure.SceneNotLoaded, SceneFlowFailure.SetUnknown, SceneFlowFailure.SetUnknown }));

            var midTransition = state.Apply(new LoadSetIntent(Menu)).Value;
            Assert.That(midTransition.Apply(new LoadSetIntent(Level1)).Code, Is.EqualTo(SceneFlowFailure.TransitionInProgress));
            Assert.That(midTransition.Apply(new SceneLoadCompletedIntent(MenuMain)).Code, Is.EqualTo(SceneFlowFailure.TransitionIllegal));

            var loadFailure = SceneFlowResult<int>.Fail(SceneFlowFailure.SetUnknown, "no set");
            Assert.That(loadFailure.Succeeded, Is.False);
            Assert.That(loadFailure.Message, Is.EqualTo("no set"));
            Assert.Throws<System.ArgumentException>(() => SceneFlowResult<int>.Fail("", "codeless"));
            Assert.Throws<System.InvalidOperationException>(() => SceneFlowResult<int>.Ok(1).As<string>());
        }

        [Test]
        public void ValidIntentSequencePassesCleanWithEveryOutcomeAccepted()
        {
            var result = Initial().ApplyAll(Sequence());
            Assert.That(result.AllAccepted, Is.True, Describe(result));
            Assert.That(result.RejectedCount, Is.EqualTo(0));
            Assert.That(result.Outcomes.Count, Is.EqualTo(Sequence().Count));
            Assert.That(result.Outcomes.Select(item => item.Index), Is.EqualTo(Enumerable.Range(0, Sequence().Count)));
        }

        // ----- helpers -----

        private static SceneGraph Graph()
        {
            var result = new SceneGraphBuilder()
                .Set(Menu, MenuMain, MenuMain)
                .Set(Level1, Level1Play, Level1Play, Level1Audio)
                .Set(Level2, Level2Play, Level2Play)
                .Set(Fallback, FallbackError, FallbackError)
                .Build(Fallback);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static TransitionOptions Options(float fadeOut = 1f, float hold = 1f, float fadeIn = 1f)
        {
            var result = TransitionOptions.TryCreate(fadeOut, hold, fadeIn);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static SceneFlowState Initial() => SceneFlowState.Initial(Graph(), Options());

        /// <summary>Drives every explicit intent needed to carry an in-flight transition through
        /// to idle, using only elapsed-time and load-completed intents, never a clock.</summary>
        private static SceneFlowState Complete(SceneFlowState state)
        {
            var guard = 0;
            while (state.Phase != TransitionPhase.Idle)
            {
                guard++;
                if (guard > 20) throw new System.InvalidOperationException("Transition did not settle.");
                if (state.Phase == TransitionPhase.Loading)
                {
                    var next = state.PendingScenesRemaining.FirstOrDefault();
                    state = state.Apply(new SceneLoadCompletedIntent(next)).Value;
                }
                else
                {
                    state = state.Apply(new ElapsedTimeIntent(SceneFlowDuration.Create(state.Options.FadeOut.Seconds + state.Options.Hold.Seconds + state.Options.FadeIn.Seconds + 1f))).Value;
                }
            }
            return state;
        }

        private static List<SceneFlowIntent> Sequence() => new List<SceneFlowIntent>
        {
            new LoadSetIntent(Menu),
            new ElapsedTimeIntent(SceneFlowDuration.Create(1f)),
            new SceneLoadCompletedIntent(MenuMain),
            new ElapsedTimeIntent(SceneFlowDuration.Create(1f)),
            new ElapsedTimeIntent(SceneFlowDuration.Create(1f)),
            new LoadSetIntent(Level1),
            new ElapsedTimeIntent(SceneFlowDuration.Create(1f)),
            new SceneLoadCompletedIntent(Level1Play),
            new SceneLoadCompletedIntent(Level1Audio),
            new ElapsedTimeIntent(SceneFlowDuration.Create(1f)),
            new ElapsedTimeIntent(SceneFlowDuration.Create(1f)),
            new SwitchSetIntent(Level1, Level2),
            new ElapsedTimeIntent(SceneFlowDuration.Create(1f)),
            new SceneLoadCompletedIntent(Level2Play),
            new ElapsedTimeIntent(SceneFlowDuration.Create(1f)),
            new ElapsedTimeIntent(SceneFlowDuration.Create(1f)),
        };

        private static string Describe(SceneFlowSequenceResult result) =>
            string.Join("\n", result.Outcomes.Select(item => item.Index + " " + item.Intent.Describe() + ": " + (item.Accepted ? "ok " + item.Code : item.Code + " " + item.Message)));
    }
}
