using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lingkyn.XrUiShell.Core;

namespace Lingkyn.XrUiShell.Core.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/xr-ui-shell/verification-contract.md and coverage-map.json.
    public sealed class XrUiShellCoreContractTests
    {
        // ----- identity (XUC-01) -----

        [Test]
        public void PanelIdCanonicalizesLowerCaseDottedSegments()
        {
            var id = PanelId.TryCreate("  hud.main  ");
            Assert.That(id.Succeeded, Is.True, id.Message);
            Assert.That(id.Value.Value, Is.EqualTo("hud.main"));
            Assert.That(id.Value.ToString(), Is.EqualTo("hud.main"));
        }

        [Test]
        public void EveryIdentityTypeRejectsMalformedTextWithIdentityMalformed()
        {
            foreach (var text in new[] { null, "", " ", "Hud.Main", "hud main", "hud..main", ".hud", "hud.", "hud-main", "hud\tmain" })
            {
                var panelResult = PanelId.TryCreate(text);
                Assert.That(panelResult.Succeeded, Is.False, text ?? "<null>");
                Assert.That(panelResult.Code, Is.EqualTo(ShellFailure.IdentityMalformed), text ?? "<null>");
                Assert.That(panelResult.Message, Is.Not.Empty, text ?? "<null>");

                Assert.That(WristMenuId.TryCreate(text).Code, Is.EqualTo(ShellFailure.IdentityMalformed), text ?? "<null>");
                Assert.That(HandMenuId.TryCreate(text).Code, Is.EqualTo(ShellFailure.IdentityMalformed), text ?? "<null>");
                Assert.That(InputSourceId.TryCreate(text).Code, Is.EqualTo(ShellFailure.IdentityMalformed), text ?? "<null>");
            }
        }

        [Test]
        public void IdentitiesWithSameCanonicalTextAreEqualValuesWithinOneType()
        {
            var first = PanelId.Parse("hud.main");
            var second = PanelId.Parse("  hud.main ");
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first != PanelId.Parse("hud.secondary"), Is.True);
            Assert.That(first.CompareTo(PanelId.Parse("hud.secondary")), Is.LessThan(0));
        }

        [Test]
        public void FourIdentityTypesShareCanonicalFormButAreDistinctCsharpTypesWithNoCrossTypeEquality()
        {
            var panel = PanelId.Parse("hud");
            var wrist = WristMenuId.Parse("hud");
            var hand = HandMenuId.Parse("hud");
            var source = InputSourceId.Parse("hud");
            Assert.That(panel.Value, Is.EqualTo(wrist.Value));
            Assert.That(panel.Value, Is.EqualTo(hand.Value));
            Assert.That(panel.Value, Is.EqualTo(source.Value));
            // Each type's Equals overload only accepts its own type, so there is no expression
            // that could compare any two of these equal; the types themselves enforce this.
            Assert.That(panel.GetType(), Is.Not.EqualTo(wrist.GetType()));
            Assert.That(wrist.GetType(), Is.Not.EqualTo(hand.GetType()));
            Assert.That(hand.GetType(), Is.Not.EqualTo(source.GetType()));
        }

        [Test]
        public void ParseThrowsWithTheFailureMessageForMalformedIdentity()
        {
            var thrown = Assert.Throws<ArgumentException>(() => PanelId.Parse("Bad Id"));
            Assert.That(thrown.Message, Does.Contain("lower-case"));
            Assert.Throws<ArgumentException>(() => WristMenuId.Parse(""));
            Assert.Throws<ArgumentException>(() => HandMenuId.Parse(null));
            Assert.Throws<ArgumentException>(() => InputSourceId.Parse(" "));
        }

        // ----- shell layout model (XUC-02) -----

        [Test]
        public void DeclaredPanelAdmitsItsHomeAnchorEvenWhenNotPassedExplicitly()
        {
            var builder = new ShellLayoutBuilder();
            var result = builder.DeclarePanel(PanelId.Parse("hud.main"), AnchorKind.World, AnchorKind.HeadLocked);
            Assert.That(result.Succeeded, Is.True, result.Message);
            var layout = builder.Build();
            layout.TryGetSurface(SurfaceId.OfPanel(PanelId.Parse("hud.main")), out var declaration);
            Assert.That(declaration.Home, Is.EqualTo(AnchorKind.World));
            Assert.That(declaration.AdmittedAnchors, Is.EquivalentTo(new[] { AnchorKind.World, AnchorKind.HeadLocked }));
        }

        [Test]
        public void WristMenuAlwaysAdmitsOnlyWristAndHandMenuAlwaysAdmitsOnlyHand()
        {
            var builder = new ShellLayoutBuilder();
            builder.DeclareWristMenu(WristMenuId.Parse("quick"));
            builder.DeclareHandMenu(HandMenuId.Parse("quick"));
            var layout = builder.Build();

            layout.TryGetSurface(SurfaceId.OfWristMenu(WristMenuId.Parse("quick")), out var wrist);
            Assert.That(wrist.Home, Is.EqualTo(AnchorKind.Wrist));
            Assert.That(wrist.AdmittedAnchors, Is.EquivalentTo(new[] { AnchorKind.Wrist }));

            layout.TryGetSurface(SurfaceId.OfHandMenu(HandMenuId.Parse("quick")), out var hand);
            Assert.That(hand.Home, Is.EqualTo(AnchorKind.Hand));
            Assert.That(hand.AdmittedAnchors, Is.EquivalentTo(new[] { AnchorKind.Hand }));
        }

        [Test]
        public void DeclaringTheSamePanelIdTwiceIsRejectedWithPanelDuplicateLeavingTheBuilderUnchanged()
        {
            var builder = new ShellLayoutBuilder();
            builder.DeclarePanel(PanelId.Parse("hud.main"), AnchorKind.World);
            var countAfterFirst = builder.SurfaceCount;

            var duplicate = builder.DeclarePanel(PanelId.Parse("hud.main"), AnchorKind.HeadLocked);
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Code, Is.EqualTo(ShellFailure.PanelDuplicate));
            Assert.That(builder.SurfaceCount, Is.EqualTo(countAfterFirst));
        }

        [Test]
        public void APanelIdAndAWristMenuIdWithTheSameTextDeclareTwoDistinctSurfacesRatherThanColliding()
        {
            var builder = new ShellLayoutBuilder();
            var panelResult = builder.DeclarePanel(PanelId.Parse("quick"), AnchorKind.World);
            var wristResult = builder.DeclareWristMenu(WristMenuId.Parse("quick"));
            Assert.That(panelResult.Succeeded, Is.True, panelResult.Message);
            Assert.That(wristResult.Succeeded, Is.True, wristResult.Message);
            Assert.That(builder.Build().SurfaceCount, Is.EqualTo(2));
        }

        [Test]
        public void BuiltLayoutEnumeratesSurfacesAndSourcesInCanonicalOrder()
        {
            var layout = SampleLayout();
            var surfaceIds = layout.Surfaces.Select(s => s.Id.ToString()).ToList();
            Assert.That(surfaceIds, Is.Ordered);
            var sourceIds = layout.Sources.Select(s => s.Id.Value).ToList();
            Assert.That(sourceIds, Is.Ordered);
            Assert.That(layout.SurfaceCount, Is.EqualTo(4));
            Assert.That(layout.SourceCount, Is.EqualTo(3));
        }

        [Test]
        public void RegisteringTheSameInputSourceIdTwiceReplacesRatherThanRejects()
        {
            var builder = new ShellLayoutBuilder();
            builder.RegisterInputSource(InputSourceId.Parse("right_ray"), InputSourceKind.Ray);
            builder.RegisterInputSource(InputSourceId.Parse("right_ray"), InputSourceKind.Poke);
            var layout = builder.Build();
            Assert.That(layout.SourceCount, Is.EqualTo(1));
            layout.TryGetSource(InputSourceId.Parse("right_ray"), out var registration);
            Assert.That(registration.Kind, Is.EqualTo(InputSourceKind.Poke));
        }

        // ----- placement intent application on immutable state (XUC-03) -----

        [Test]
        public void OpenCloseAndFocusRejectAnUndeclaredSurfaceWithPanelUnknown()
        {
            var state = ShellState.Initial(SampleLayout());
            var ghost = SurfaceId.OfPanel(PanelId.Parse("ghost"));
            Assert.That(state.Apply(new OpenIntent(ghost)).Code, Is.EqualTo(ShellFailure.PanelUnknown));
            Assert.That(state.Apply(new CloseIntent(ghost)).Code, Is.EqualTo(ShellFailure.PanelUnknown));
            Assert.That(state.Apply(new FocusIntent(ghost)).Code, Is.EqualTo(ShellFailure.PanelUnknown));
        }

        [Test]
        public void DockRejectsAnUndeclaredSurfaceOrTargetWithPanelUnknown()
        {
            var state = ShellState.Initial(SampleLayout());
            var ghost = SurfaceId.OfPanel(PanelId.Parse("ghost"));
            Assert.That(state.Apply(new DockIntent(ghost, PrimaryPanel)).Code, Is.EqualTo(ShellFailure.PanelUnknown));
            Assert.That(state.Apply(new DockIntent(SecondaryPanel, ghost)).Code, Is.EqualTo(ShellFailure.PanelUnknown));
        }

        [Test]
        public void FollowRejectsAnUndeclaredSurfaceWithPanelUnknown()
        {
            var state = ShellState.Initial(SampleLayout());
            var ghost = SurfaceId.OfPanel(PanelId.Parse("ghost"));
            Assert.That(state.Apply(new FollowIntent(ghost, AnchorKind.Wrist)).Code, Is.EqualTo(ShellFailure.PanelUnknown));
        }

        [Test]
        public void DockRejectsATargetAnchorKindTheSurfaceDoesNotAdmitWithAnchorKindUnsupported()
        {
            var state = ShellState.Initial(SampleLayout());
            // WristMenu only admits Wrist; docking it to the world-anchored primary panel is refused.
            var result = state.Apply(new DockIntent(WristMenu, PrimaryPanel));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.AnchorKindUnsupported));
        }

        [Test]
        public void FollowRejectsAnAnchorKindTheSurfaceDoesNotAdmitWithAnchorKindUnsupported()
        {
            var state = ShellState.Initial(SampleLayout());
            var result = state.Apply(new FollowIntent(HandMenu, AnchorKind.Wrist));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.AnchorKindUnsupported));
        }

        [Test]
        public void AcceptedDockSetsTheDockedTargetAndAdoptsTheTargetsHomeAnchorKind()
        {
            var state = ShellState.Initial(SampleLayout());
            var result = state.Apply(new DockIntent(SecondaryPanel, PrimaryPanel));
            Assert.That(result.Succeeded, Is.True, result.Message);
            result.Value.TryGetSurfaceState(SecondaryPanel, out var runtime);
            Assert.That(runtime.DockedTarget, Is.EqualTo(PrimaryPanel));
            Assert.That(runtime.CurrentAnchorKind, Is.EqualTo(AnchorKind.World));
        }

        [Test]
        public void AcceptedFollowSetsTheFollowFlagAndTheTargetAnchorKind()
        {
            var state = ShellState.Initial(SampleLayout());
            var result = state.Apply(new FollowIntent(SecondaryPanel, AnchorKind.HeadLocked));
            Assert.That(result.Succeeded, Is.True, result.Message);
            result.Value.TryGetSurfaceState(SecondaryPanel, out var runtime);
            Assert.That(runtime.IsFollowing, Is.True);
            Assert.That(runtime.CurrentAnchorKind, Is.EqualTo(AnchorKind.HeadLocked));
        }

        [Test]
        public void FocusWhileAnotherOpenPanelHoldsExclusiveFocusIsRejectedWithFocusConflict()
        {
            var state = ShellState.Initial(SampleLayout())
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new FocusIntent(PrimaryPanel)).Value
                .Apply(new OpenIntent(SecondaryPanel)).Value;
            var result = state.Apply(new FocusIntent(SecondaryPanel));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.FocusConflict));
        }

        [Test]
        public void FocusIsAllowedWhenThePreviouslyFocusedPanelIsNoLongerOpen()
        {
            var state = ShellState.Initial(SampleLayout())
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new FocusIntent(PrimaryPanel)).Value
                .Apply(new CloseIntent(PrimaryPanel)).Value
                .Apply(new OpenIntent(SecondaryPanel)).Value;
            var result = state.Apply(new FocusIntent(SecondaryPanel));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.FocusedSurface, Is.EqualTo(SecondaryPanel));
        }

        [Test]
        public void AtMostOnePanelIsFocusedAtAnyTime()
        {
            var state = ShellState.Initial(SampleLayout())
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new FocusIntent(PrimaryPanel)).Value;
            Assert.That(state.FocusedSurface, Is.EqualTo(PrimaryPanel));
            var refocused = state.Apply(new FocusIntent(PrimaryPanel)).Value;
            Assert.That(refocused.FocusedSurface, Is.EqualTo(PrimaryPanel));
        }

        [Test]
        public void ClosingTheFocusedPanelClearsFocus()
        {
            var state = ShellState.Initial(SampleLayout())
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new FocusIntent(PrimaryPanel)).Value
                .Apply(new CloseIntent(PrimaryPanel)).Value;
            Assert.That(state.FocusedSurface, Is.Null);
        }

        // ----- state immutability (XUC-04) -----

        [Test]
        public void RejectedIntentLeavesPriorStateIntactWithTheFailureCode()
        {
            var state = ShellState.Initial(SampleLayout());
            var before = state.Fingerprint();
            var rejected = state.Apply(new OpenIntent(SurfaceId.OfPanel(PanelId.Parse("ghost"))));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(ShellFailure.PanelUnknown));
            Assert.That(state.Fingerprint(), Is.EqualTo(before));
        }

        [Test]
        public void AcceptedIntentProducesNewStateWhilePriorStateStaysReadableAndEqualToItself()
        {
            var state = ShellState.Initial(SampleLayout());
            var initialFingerprint = state.Fingerprint();
            var next = state.Apply(new OpenIntent(PrimaryPanel)).Value;

            Assert.That(ReferenceEquals(state, next), Is.False);
            Assert.That(state.Fingerprint(), Is.EqualTo(initialFingerprint));
            Assert.That(state, Is.EqualTo(state));
            state.TryGetSurfaceState(PrimaryPanel, out var priorRuntime);
            Assert.That(priorRuntime.IsOpen, Is.False);
            next.TryGetSurfaceState(PrimaryPanel, out var nextRuntime);
            Assert.That(nextRuntime.IsOpen, Is.True);
        }

        // ----- deterministic replay (XUC-05) -----

        [Test]
        public void SameIntentSequenceProducesEqualStateAndFingerprint()
        {
            var layout = SampleLayout();
            var first = ShellState.Initial(layout).ApplyAll(Sequence());
            var second = ShellState.Initial(layout).ApplyAll(Sequence());

            Assert.That(first.AllAccepted, Is.True, Describe(first));
            Assert.That(first.State, Is.EqualTo(second.State));
            Assert.That(first.State.Fingerprint(), Is.EqualTo(second.State.Fingerprint()));
            Assert.That(ReferenceEquals(first.State, second.State), Is.False);
        }

        [Test]
        public void StateEnumeratesEveryDeclaredSurfaceWithItsRuntimeFieldsTheFocusedSurfaceAndTheRegisteredSources()
        {
            var state = ShellState.Initial(SampleLayout())
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new FocusIntent(PrimaryPanel)).Value
                .Apply(new DockIntent(SecondaryPanel, PrimaryPanel)).Value;

            var surfaces = state.Surfaces.ToList();
            Assert.That(surfaces.Count, Is.EqualTo(4));
            Assert.That(surfaces.Select(s => s.Id.ToString()), Is.Ordered);
            Assert.That(state.FocusedSurface, Is.EqualTo(PrimaryPanel));
            Assert.That(state.RegisteredSources.Count(), Is.EqualTo(3));

            state.TryGetSurfaceState(SecondaryPanel, out var secondary);
            Assert.That(secondary.DockedTarget, Is.EqualTo(PrimaryPanel));
            Assert.That(secondary.IsOpen, Is.False);
            Assert.That(secondary.IsFollowing, Is.False);
        }

        // ----- pointer and gaze routing (XUC-06) -----

        [Test]
        public void RoutingRejectsAnUnregisteredSourceWithSourceUnknown()
        {
            var state = ShellState.Initial(SampleLayout()).Apply(new OpenIntent(PrimaryPanel)).Value;
            var ghostSource = InputSourceId.Parse("ghost_source");
            var result = ShellRouter.Resolve(state, new HoverIntent(ghostSource, new[] { PrimaryPanel }));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.SourceUnknown));
        }

        [Test]
        public void RoutingResolvesToTheSingleOpenCandidate()
        {
            var state = ShellState.Initial(SampleLayout()).Apply(new OpenIntent(PrimaryPanel)).Value;
            var result = ShellRouter.Resolve(state, new HoverIntent(RightRay, new[] { PrimaryPanel }));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value, Is.EqualTo(PrimaryPanel));
        }

        [Test]
        public void RoutingPrefersTheExclusivelyFocusedPanelWhenItIsACandidate()
        {
            var state = ShellState.Initial(SampleLayout())
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new OpenIntent(SecondaryPanel)).Value
                .Apply(new FocusIntent(SecondaryPanel)).Value;
            var result = ShellRouter.Resolve(state, new HoverIntent(RightRay, new[] { PrimaryPanel, SecondaryPanel }));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value, Is.EqualTo(SecondaryPanel));
        }

        [Test]
        public void RoutingReturnsRouteAmbiguousForTwoOrMoreOpenCandidatesWithNoFocusedPanelAmongThem()
        {
            var state = ShellState.Initial(SampleLayout())
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new OpenIntent(SecondaryPanel)).Value;
            var result = ShellRouter.Resolve(state, new HoverIntent(RightRay, new[] { PrimaryPanel, SecondaryPanel }));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.RouteAmbiguous));
        }

        [Test]
        public void RoutingReturnsRouteNoneWhenNoCandidateIsOpen()
        {
            var state = ShellState.Initial(SampleLayout());
            var result = ShellRouter.Resolve(state, new HoverIntent(RightRay, new[] { PrimaryPanel }));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.RouteNone));
        }

        [Test]
        public void SelectOrScrollFromAGazeSourceWithoutARegisteredCommitSourceIsRejectedWithSourceKindUnsupported()
        {
            var state = ShellState.Initial(SampleLayout()).Apply(new OpenIntent(PrimaryPanel)).Value;
            var withoutCommit = ShellRouter.Resolve(state, new SelectIntent(GazeSource, new[] { PrimaryPanel }));
            Assert.That(withoutCommit.Succeeded, Is.False);
            Assert.That(withoutCommit.Code, Is.EqualTo(ShellFailure.SourceKindUnsupported));

            var withUnregisteredCommit = ShellRouter.Resolve(state, new ScrollIntent(GazeSource, new[] { PrimaryPanel }, InputSourceId.Parse("ghost_commit")));
            Assert.That(withUnregisteredCommit.Code, Is.EqualTo(ShellFailure.SourceKindUnsupported));
        }

        [Test]
        public void SelectFromAGazeSourceWithARegisteredCommitSourceResolvesNormally()
        {
            var state = ShellState.Initial(SampleLayout()).Apply(new OpenIntent(PrimaryPanel)).Value;
            var result = ShellRouter.Resolve(state, new SelectIntent(GazeSource, new[] { PrimaryPanel }, RightRay));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value, Is.EqualTo(PrimaryPanel));
        }

        [Test]
        public void HoverFromAGazeSourceNeedsNoCommitSource()
        {
            var state = ShellState.Initial(SampleLayout()).Apply(new OpenIntent(PrimaryPanel)).Value;
            var result = ShellRouter.Resolve(state, new HoverIntent(GazeSource, new[] { PrimaryPanel }));
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        [Test]
        public void ARejectedRoutingIntentChangesNoState()
        {
            var state = ShellState.Initial(SampleLayout()).Apply(new OpenIntent(PrimaryPanel)).Value;
            var before = state.Fingerprint();
            ShellRouter.Resolve(state, new HoverIntent(InputSourceId.Parse("ghost_source"), new[] { PrimaryPanel }));
            Assert.That(state.Fingerprint(), Is.EqualTo(before));
        }

        // ----- skin contract (XUC-07) -----

        [Test]
        public void SkinMappingRejectsATokenOutsideTheDesignLanguageSetWithTokenUnknown()
        {
            var result = new SkinMappingBuilder().Map("panel.background", "surface.ghost");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.TokenUnknown));
        }

        [Test]
        public void SkinMappingRejectsASlotOutsideTheClosedSetWithSlotUnknown()
        {
            var result = new SkinMappingBuilder().Map("panel.ghost", "surface.panel");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.SlotUnknown));
        }

        [Test]
        public void BuildingAMappingThatLeavesAnySlotUnmappedIsRejectedWithSlotUnmapped()
        {
            var builder = new SkinMappingBuilder();
            foreach (var slot in ShellSlotNames.AllSlots.Skip(1))
            {
                builder.Map(slot, DesignToken.SurfacePanel);
            }
            var result = builder.Build();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.SlotUnmapped));
            Assert.That(result.Message, Does.Contain(ShellSlotNames.ToName(ShellSlotNames.AllSlots[0])));
        }

        [Test]
        public void TheCanonicalSkinMappingResolvesEverySlot()
        {
            var mapping = CanonicalSkinMapping.Build();
            foreach (var slot in ShellSlotNames.AllSlots)
            {
                var resolved = mapping.Resolve(slot);
                Assert.That(Enum.IsDefined(typeof(DesignToken), resolved), Is.True, slot.ToString());
            }
            Assert.That(mapping.Entries.Count(), Is.EqualTo(ShellSlotNames.AllSlots.Count));
        }

        // ----- structured results with stable codes (XUC-08) -----

        [Test]
        public void StructuredResultsCarryStableFailureCodeForEveryCoreRejection()
        {
            var layout = SampleLayout();
            var state = ShellState.Initial(layout).Apply(new OpenIntent(PrimaryPanel)).Value;
            var ghost = SurfaceId.OfPanel(PanelId.Parse("ghost"));

            var dupBuilder = new ShellLayoutBuilder();
            dupBuilder.DeclarePanel(PanelId.Parse("dup"), AnchorKind.World);
            var duplicateDeclarationResult = dupBuilder.DeclarePanel(PanelId.Parse("dup"), AnchorKind.World);

            var focusConflictState = ShellState.Initial(layout)
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new FocusIntent(PrimaryPanel)).Value
                .Apply(new OpenIntent(SecondaryPanel)).Value;
            var focusConflictResult = focusConflictState.Apply(new FocusIntent(SecondaryPanel));

            var bothOpenState = ShellState.Initial(layout)
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new OpenIntent(SecondaryPanel)).Value;

            var codes = new List<string>
            {
                PanelId.TryCreate("Bad Id").Code,
                state.Apply(new OpenIntent(ghost)).Code,
                duplicateDeclarationResult.Code,
                state.Apply(new DockIntent(WristMenu, PrimaryPanel)).Code,
                focusConflictResult.Code,
                ShellRouter.Resolve(state, new HoverIntent(InputSourceId.Parse("ghost_source"), new[] { PrimaryPanel })).Code,
                ShellRouter.Resolve(state, new SelectIntent(GazeSource, new[] { PrimaryPanel })).Code,
                ShellRouter.Resolve(bothOpenState, new HoverIntent(RightRay, new[] { PrimaryPanel, SecondaryPanel })).Code,
                ShellRouter.Resolve(ShellState.Initial(layout), new HoverIntent(RightRay, new[] { PrimaryPanel })).Code,
                new SkinMappingBuilder().Map("panel.background", "surface.ghost").Code,
                new SkinMappingBuilder().Map("panel.ghost", "surface.panel").Code,
                BuildMappingMissingFirstSlot().Code,
            };
            var expected = new[]
            {
                ShellFailure.IdentityMalformed,
                ShellFailure.PanelUnknown,
                ShellFailure.PanelDuplicate,
                ShellFailure.AnchorKindUnsupported,
                ShellFailure.FocusConflict,
                ShellFailure.SourceUnknown,
                ShellFailure.SourceKindUnsupported,
                ShellFailure.RouteAmbiguous,
                ShellFailure.RouteNone,
                ShellFailure.TokenUnknown,
                ShellFailure.SlotUnknown,
                ShellFailure.SlotUnmapped,
            };
            Assert.That(codes, Is.EqualTo(expected));
            foreach (var code in codes) Assert.That(code, Is.Not.Empty);
        }

        [Test]
        public void ValidIntentSequencePassesCleanWithEveryOutcomeAccepted()
        {
            var result = ShellState.Initial(SampleLayout()).ApplyAll(Sequence());
            Assert.That(result.AllAccepted, Is.True, Describe(result));
            Assert.That(result.RejectedCount, Is.EqualTo(0));
            Assert.That(result.Outcomes.Count, Is.EqualTo(Sequence().Count));
            Assert.That(result.Outcomes.Select(item => item.Index), Is.EqualTo(Enumerable.Range(0, Sequence().Count)));
        }

        // ----- no engine type (XUC-09) -----

        [Test]
        public void CoreAssemblyReferencesNoUnityEngineAssembly()
        {
            var assembly = typeof(ShellState).Assembly;
            var referenced = assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty);
            Assert.That(referenced.Any(name => name.IndexOf("UnityEngine", StringComparison.OrdinalIgnoreCase) >= 0), Is.False, string.Join(",", referenced));
        }

        // ----- helpers -----

        private static readonly SurfaceId PrimaryPanel = SurfaceId.OfPanel(PanelId.Parse("hud.primary"));
        private static readonly SurfaceId SecondaryPanel = SurfaceId.OfPanel(PanelId.Parse("hud.secondary"));
        private static readonly SurfaceId WristMenu = SurfaceId.OfWristMenu(WristMenuId.Parse("quick_menu"));
        private static readonly SurfaceId HandMenu = SurfaceId.OfHandMenu(HandMenuId.Parse("palm_menu"));
        private static readonly InputSourceId RightRay = InputSourceId.Parse("right_ray");
        private static readonly InputSourceId LeftPoke = InputSourceId.Parse("left_poke");
        private static readonly InputSourceId GazeSource = InputSourceId.Parse("head_gaze");

        private static ShellLayout SampleLayout()
        {
            var builder = new ShellLayoutBuilder();
            AssertOk(builder.DeclarePanel(PanelId.Parse("hud.primary"), AnchorKind.World, AnchorKind.HeadLocked));
            AssertOk(builder.DeclarePanel(PanelId.Parse("hud.secondary"), AnchorKind.World, AnchorKind.World, AnchorKind.HeadLocked));
            AssertOk(builder.DeclareWristMenu(WristMenuId.Parse("quick_menu")));
            AssertOk(builder.DeclareHandMenu(HandMenuId.Parse("palm_menu")));
            builder.RegisterInputSource(RightRay, InputSourceKind.Ray);
            builder.RegisterInputSource(LeftPoke, InputSourceKind.Poke);
            builder.RegisterInputSource(GazeSource, InputSourceKind.Gaze);
            return builder.Build();
        }

        private static void AssertOk(ShellResult<ShellLayoutBuilder> result) => Assert.That(result.Succeeded, Is.True, result.Message);

        private static ShellResult<SkinMapping> BuildMappingMissingFirstSlot()
        {
            var builder = new SkinMappingBuilder();
            foreach (var slot in ShellSlotNames.AllSlots.Skip(1))
            {
                builder.Map(slot, DesignToken.SurfacePanel);
            }
            return builder.Build();
        }

        private static List<ShellIntent> Sequence() => new List<ShellIntent>
        {
            new OpenIntent(PrimaryPanel),
            new FocusIntent(PrimaryPanel),
            new OpenIntent(SecondaryPanel),
            new DockIntent(SecondaryPanel, PrimaryPanel),
            new CloseIntent(PrimaryPanel),
            new FocusIntent(SecondaryPanel),
            new FollowIntent(SecondaryPanel, AnchorKind.HeadLocked),
        };

        private static string Describe(ShellSequenceResult result) =>
            string.Join("\n", result.Outcomes.Select(item => item.Index + " " + item.Intent.Describe() + ": " + (item.Accepted ? "ok " + item.Code : item.Code + " " + item.Message)));
    }
}
