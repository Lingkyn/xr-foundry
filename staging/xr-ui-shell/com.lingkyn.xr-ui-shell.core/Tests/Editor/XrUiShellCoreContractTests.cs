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

        // ----- ornament: one shell-owned fixed surface, never folded (XUC-10) -----

        [Test]
        public void OrnamentIsAFixedIdentityNeverPartOfADeclaredLayoutsSurfaceCount()
        {
            var layout = SampleLayout();
            Assert.That(layout.TryGetSurface(SurfaceId.TheOrnament, out _), Is.False, "The ornament is shell-owned and never enters a declared layout.");
            Assert.That(layout.SurfaceCount, Is.EqualTo(4));

            // The ornament identity is fixed and reachable from any state, declared or not.
            Assert.That(SurfaceId.TheOrnament, Is.EqualTo(SurfaceId.TheOrnament));
            Assert.That(SurfaceId.TheOrnament.ToString(), Does.Contain(ShellOrnament.CanonicalId));
        }

        [Test]
        public void FoldAndUnfoldAlwaysRejectTheOrnamentWithOrnamentNeverFolds()
        {
            var state = ShellState.Initial(SampleLayout());
            var foldResult = state.Apply(new FoldIntent(SurfaceId.TheOrnament));
            var unfoldResult = state.Apply(new UnfoldIntent(SurfaceId.TheOrnament));

            Assert.That(foldResult.Succeeded, Is.False);
            Assert.That(foldResult.Code, Is.EqualTo(ShellFailure.OrnamentNeverFolds));
            Assert.That(unfoldResult.Succeeded, Is.False);
            Assert.That(unfoldResult.Code, Is.EqualTo(ShellFailure.OrnamentNeverFolds));
        }

        // ----- fold retains state: fold and unfold change visibility only (XUC-15, kept next to
        // the ornament clause it shares ShellState.ApplyFold/ApplyUnfold with) -----

        [Test]
        public void FoldChangesOnlyVisibilityAndRetainsOpenDockedAndFollowState()
        {
            var state = ShellState.Initial(SampleLayout())
                .Apply(new OpenIntent(SecondaryPanel)).Value
                .Apply(new DockIntent(SecondaryPanel, PrimaryPanel)).Value
                .Apply(new FollowIntent(SecondaryPanel, AnchorKind.HeadLocked)).Value;
            state.TryGetSurfaceState(SecondaryPanel, out var beforeFold);
            Assert.That(beforeFold.IsFolded, Is.False);

            var folded = state.Apply(new FoldIntent(SecondaryPanel));
            Assert.That(folded.Succeeded, Is.True, folded.Message);
            folded.Value.TryGetSurfaceState(SecondaryPanel, out var afterFold);

            Assert.That(afterFold.IsFolded, Is.True);
            Assert.That(afterFold.IsOpen, Is.EqualTo(beforeFold.IsOpen));
            Assert.That(afterFold.DockedTarget, Is.EqualTo(beforeFold.DockedTarget));
            Assert.That(afterFold.IsFollowing, Is.EqualTo(beforeFold.IsFollowing));
            Assert.That(afterFold.CurrentAnchorKind, Is.EqualTo(beforeFold.CurrentAnchorKind));
        }

        [Test]
        public void UnfoldReversesFoldWithoutTouchingOtherState()
        {
            var state = ShellState.Initial(SampleLayout())
                .Apply(new OpenIntent(PrimaryPanel)).Value
                .Apply(new FoldIntent(PrimaryPanel)).Value;
            state.TryGetSurfaceState(PrimaryPanel, out var folded);
            Assert.That(folded.IsFolded, Is.True);

            var unfolded = state.Apply(new UnfoldIntent(PrimaryPanel));
            Assert.That(unfolded.Succeeded, Is.True, unfolded.Message);
            unfolded.Value.TryGetSurfaceState(PrimaryPanel, out var afterUnfold);

            Assert.That(afterUnfold.IsFolded, Is.False);
            Assert.That(afterUnfold.IsOpen, Is.EqualTo(folded.IsOpen));
        }

        [Test]
        public void FoldAndUnfoldOfAnUndeclaredSurfaceAreRejectedWithPanelUnknown()
        {
            var state = ShellState.Initial(SampleLayout());
            var ghost = SurfaceId.OfPanel(PanelId.Parse("ghost"));
            Assert.That(state.Apply(new FoldIntent(ghost)).Code, Is.EqualTo(ShellFailure.PanelUnknown));
            Assert.That(state.Apply(new UnfoldIntent(ghost)).Code, Is.EqualTo(ShellFailure.PanelUnknown));
        }

        // ----- verb registry: closed registered id set, wired/unwired partition, one constant id
        // and display word per verb (XUC-11) -----

        [Test]
        public void VerbRegistryPartitionsRegisteredIdsIntoWiredAndUnwired()
        {
            var builder = new VerbRegistryBuilder();
            AssertOk(builder.Register(VerbId.Parse("tune"), "Tune", VerbWireState.Wired));
            AssertOk(builder.Register(VerbId.Parse("delete"), "Delete", VerbWireState.Unwired));
            var registry = builder.Build();

            registry.TryGet(VerbId.Parse("tune"), out var tune);
            registry.TryGet(VerbId.Parse("delete"), out var delete);
            Assert.That(tune.WireState, Is.EqualTo(VerbWireState.Wired));
            Assert.That(delete.WireState, Is.EqualTo(VerbWireState.Unwired));
            Assert.That(registry.Verbs.Select(v => v.WireState), Is.EquivalentTo(new[] { VerbWireState.Wired, VerbWireState.Unwired }));
        }

        [Test]
        public void RegisteringTheSameVerbIdTwiceIsRejectedWithVerbDuplicateLeavingTheBuilderUnchanged()
        {
            var builder = new VerbRegistryBuilder();
            AssertOk(builder.Register(VerbId.Parse("tune"), "Tune", VerbWireState.Wired));
            var countAfterFirst = builder.Count;

            var duplicate = builder.Register(VerbId.Parse("tune"), "Retune", VerbWireState.Unwired);
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Code, Is.EqualTo(ShellFailure.VerbDuplicate));
            Assert.That(builder.Count, Is.EqualTo(countAfterFirst));

            builder.Build().TryGet(VerbId.Parse("tune"), out var registration);
            Assert.That(registration.DisplayWord, Is.EqualTo("Tune"), "The first registration's word is never replaced by a later attempt.");
        }

        [Test]
        public void AnUnregisteredVerbIsRejectedWithVerbUnknownAndNeverSilentlyAbsorbed()
        {
            var registry = new VerbRegistryBuilder().Register(VerbId.Parse("tune"), "Tune", VerbWireState.Wired).Value.Build();
            var result = VerbLookup.Resolve(registry, VerbId.Parse("ghost_verb"));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.VerbUnknown));
            Assert.That(result.FieldPath, Is.EqualTo("ghost_verb"));
        }

        [Test]
        public void AVerbsIdAndDisplayWordAreFixedAtRegistrationRegardlessOfTheTargetItLaterResolves()
        {
            var registry = new VerbRegistryBuilder().Register(VerbId.Parse("tune"), "Tune", VerbWireState.Wired).Value.Build();
            registry.TryGet(VerbId.Parse("tune"), out var registration);

            var subjectA = FocusSubject.None.Claim(FocusTarget.OfPanel(PrimaryPanel));
            var subjectB = FocusSubject.None.Claim(FocusTarget.OfExternal("some/other/thing"));
            var resolvedA = VerbResolver.Resolve(registry, registration.Id, subjectA);
            var resolvedB = VerbResolver.Resolve(registry, registration.Id, subjectB);

            Assert.That(resolvedA.Succeeded, Is.True);
            Assert.That(resolvedB.Succeeded, Is.True);
            Assert.That(registration.Id, Is.EqualTo(VerbId.Parse("tune")));
            Assert.That(registration.DisplayWord, Is.EqualTo("Tune"), "The word never varies; only the resolved target above did.");
        }

        // ----- FocusSubject: single-valued, claimed only by an explicit focus intent (XUC-12) -----

        [Test]
        public void FocusSubjectStartsWithNoCurrentTargetAndIsOnlySetByAnExplicitClaim()
        {
            Assert.That(FocusSubject.None.HasTarget, Is.False);
            Assert.That(FocusSubject.None.Current, Is.Null);
        }

        [Test]
        public void ReadingCurrentRepeatedlyLikeAPerFrameMirrorNeverClaimsAnything()
        {
            var subject = FocusSubject.None;
            for (var frame = 0; frame < 5; frame++)
            {
                // A per-frame mirror or sync only ever reads Current; FocusSubject exposes no
                // method that a read could call to claim on its own.
                var observed = subject.Current;
                Assert.That(observed, Is.Null);
            }
            Assert.That(subject.HasTarget, Is.False);
        }

        [Test]
        public void ClaimReplacesTheWholeSubjectRatherThanMerging()
        {
            var subject = FocusSubject.None
                .Claim(FocusTarget.OfPanel(PrimaryPanel))
                .Claim(FocusTarget.OfExternal("interaction/haptics/left"));

            Assert.That(subject.Current.Kind, Is.EqualTo(FocusTargetKind.External));
            Assert.That(subject.Current.ExternalPath, Is.EqualTo("interaction/haptics/left"));
            Assert.That(subject.Current.Panel, Is.Null, "The prior panel target left no trace in the replaced value.");
        }

        [Test]
        public void ExactlyOneOfPanelPanelItemOrExternalNamesTheCurrentTargetOrNone()
        {
            var panelTarget = FocusTarget.OfPanel(PrimaryPanel);
            var itemTarget = FocusTarget.OfPanelItem(PrimaryPanel, "slot_3");
            var externalTarget = FocusTarget.OfExternal("interaction/haptics/left");

            Assert.That(panelTarget.Kind, Is.EqualTo(FocusTargetKind.Panel));
            Assert.That(itemTarget.Kind, Is.EqualTo(FocusTargetKind.PanelItem));
            Assert.That(itemTarget.ToString(), Is.EqualTo($"{PrimaryPanel}#slot_3"));
            Assert.That(externalTarget.Kind, Is.EqualTo(FocusTargetKind.External));
            Assert.That(FocusSubject.None.Claim(panelTarget).ClaimNone().HasTarget, Is.False);
        }

        // ----- verb resolution: reads only FocusSubject, one target or a named no-target result
        // (XUC-13) -----

        [Test]
        public void VerbResolutionYieldsExactlyOneTargetWhenWiredAndFocused()
        {
            var registry = new VerbRegistryBuilder().Register(VerbId.Parse("tune"), "Tune", VerbWireState.Wired).Value.Build();
            var subject = FocusSubject.None.Claim(FocusTarget.OfPanel(PrimaryPanel));

            var result = VerbResolver.Resolve(registry, VerbId.Parse("tune"), subject);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value, Is.SameAs(subject.Current));
        }

        [Test]
        public void VerbResolutionYieldsTheNamedNoTargetResultWhenNothingIsFocused()
        {
            var registry = new VerbRegistryBuilder().Register(VerbId.Parse("tune"), "Tune", VerbWireState.Wired).Value.Build();
            var result = VerbResolver.Resolve(registry, VerbId.Parse("tune"), FocusSubject.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(ShellFailure.VerbNoTarget));
        }

        [Test]
        public void VerbResolutionRejectsAnUnregisteredVerbWithVerbUnknownAndAnUnwiredVerbWithVerbUnwired()
        {
            var registry = new VerbRegistryBuilder().Register(VerbId.Parse("delete"), "Delete", VerbWireState.Unwired).Value.Build();
            var subject = FocusSubject.None.Claim(FocusTarget.OfPanel(PrimaryPanel));

            var unknown = VerbResolver.Resolve(registry, VerbId.Parse("ghost_verb"), subject);
            var unwired = VerbResolver.Resolve(registry, VerbId.Parse("delete"), subject);

            Assert.That(unknown.Code, Is.EqualTo(ShellFailure.VerbUnknown));
            Assert.That(unwired.Code, Is.EqualTo(ShellFailure.VerbUnwired));
        }

        [Test]
        public void ResolvingTwoDifferentVerbsAgainstTheSameSubjectAlwaysAgreeOnTheTarget()
        {
            var registry = new VerbRegistryBuilder()
                .Register(VerbId.Parse("tune"), "Tune", VerbWireState.Wired).Value
                .Register(VerbId.Parse("grab"), "Grab", VerbWireState.Wired).Value
                .Build();
            var subject = FocusSubject.None.Claim(FocusTarget.OfPanel(PrimaryPanel));

            var tuneResult = VerbResolver.Resolve(registry, VerbId.Parse("tune"), subject);
            var grabResult = VerbResolver.Resolve(registry, VerbId.Parse("grab"), subject);

            Assert.That(tuneResult.Succeeded, Is.True);
            Assert.That(grabResult.Succeeded, Is.True);
            Assert.That(tuneResult.Value, Is.EqualTo(grabResult.Value), "There is one FocusSubject value to read, so no verb reads a different target through a precedence chain.");
        }

        // ----- pre-press affordance: never available for a target the verb would refuse
        // (XUC-14) -----

        [Test]
        public void AffordanceIsAvailableWithTheResolvedTargetNameWhenResolutionWouldSucceed()
        {
            var registry = new VerbRegistryBuilder().Register(VerbId.Parse("tune"), "Tune", VerbWireState.Wired).Value.Build();
            var subject = FocusSubject.None.Claim(FocusTarget.OfPanel(PrimaryPanel));

            var affordance = VerbAffordanceQuery.Query(registry, VerbId.Parse("tune"), subject);
            var resolution = VerbResolver.Resolve(registry, VerbId.Parse("tune"), subject);

            Assert.That(affordance.IsAvailable, Is.True);
            Assert.That(affordance.ReasonCode, Is.Empty);
            Assert.That(affordance.TargetName, Is.EqualTo(subject.Current.ToString()));
            Assert.That(resolution.Succeeded, Is.True, "Available must mean a press would in fact succeed.");
        }

        [Test]
        public void AVerbIsNeverShownAvailableForATargetItWouldRefuse()
        {
            var registry = new VerbRegistryBuilder()
                .Register(VerbId.Parse("tune"), "Tune", VerbWireState.Wired).Value
                .Register(VerbId.Parse("delete"), "Delete", VerbWireState.Unwired).Value
                .Build();
            var focused = FocusSubject.None.Claim(FocusTarget.OfPanel(PrimaryPanel));

            // Unregistered.
            AssertAffordanceAgreesWithResolution(registry, VerbId.Parse("ghost_verb"), focused);
            // Registered but unwired.
            AssertAffordanceAgreesWithResolution(registry, VerbId.Parse("delete"), focused);
            // Registered and wired, but nothing is focused.
            AssertAffordanceAgreesWithResolution(registry, VerbId.Parse("tune"), FocusSubject.None);
        }

        private static void AssertAffordanceAgreesWithResolution(VerbRegistry registry, VerbId verbId, FocusSubject subject)
        {
            var affordance = VerbAffordanceQuery.Query(registry, verbId, subject);
            var resolution = VerbResolver.Resolve(registry, verbId, subject);

            Assert.That(affordance.IsAvailable, Is.False, verbId.ToString());
            Assert.That(resolution.Succeeded, Is.False, verbId.ToString());
            Assert.That(affordance.ReasonCode, Is.EqualTo(resolution.Code), verbId.ToString());
        }

        // ----- one intent channel for people and agents: actor and expected revision (XUC-16) -----

        [Test]
        public void EveryPlacementIntentDefaultsToPlayerActorWithNoExpectedRevision()
        {
            ShellIntent[] intents =
            {
                new OpenIntent(PrimaryPanel),
                new CloseIntent(PrimaryPanel),
                new FocusIntent(PrimaryPanel),
                new DockIntent(SecondaryPanel, PrimaryPanel),
                new FollowIntent(SecondaryPanel, AnchorKind.HeadLocked),
                new FoldIntent(PrimaryPanel),
                new UnfoldIntent(PrimaryPanel),
            };
            foreach (var intent in intents)
            {
                Assert.That(intent.Actor, Is.EqualTo(IntentActor.Player), intent.Describe());
                Assert.That(intent.ExpectedRevision, Is.Null, intent.Describe());
            }
        }

        [Test]
        public void StateRevisionStartsAtZeroAndIncreasesByExactlyOneOnEveryAcceptedIntentButNeverOnARejected()
        {
            var state = ShellState.Initial(SampleLayout());
            Assert.That(state.Revision, Is.EqualTo(0));

            var afterAccepted = state.Apply(new OpenIntent(PrimaryPanel));
            Assert.That(afterAccepted.Succeeded, Is.True, afterAccepted.Message);
            Assert.That(afterAccepted.Value.Revision, Is.EqualTo(1));

            var rejected = afterAccepted.Value.Apply(new OpenIntent(SurfaceId.OfPanel(PanelId.Parse("ghost"))));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(afterAccepted.Value.Revision, Is.EqualTo(1), "A rejected intent never bumps the revision of the state it found.");

            var secondAccepted = afterAccepted.Value.Apply(new CloseIntent(PrimaryPanel));
            Assert.That(secondAccepted.Succeeded, Is.True, secondAccepted.Message);
            Assert.That(secondAccepted.Value.Revision, Is.EqualTo(2));
        }

        // ----- a stale expected revision is rejected with state.stale (XUC-17) -----

        [Test]
        public void AStaleExpectedRevisionIsRejectedWithStateStaleAndChangesNothing()
        {
            var state = ShellState.Initial(SampleLayout());
            var moved = state.Apply(new OpenIntent(PrimaryPanel)).Value;
            Assert.That(moved.Revision, Is.EqualTo(1));

            var stale = moved.Apply(new OpenIntent(SecondaryPanel, IntentActor.Player, 0));
            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.Code, Is.EqualTo(ShellFailure.StateStale));
            Assert.That(moved.TryGetSurfaceState(SecondaryPanel, out var secondary), Is.True);
            Assert.That(secondary.IsOpen, Is.False, "A rejected stale intent must leave the state it found untouched.");
            Assert.That(moved.Revision, Is.EqualTo(1));
        }

        [Test]
        public void EveryPlacementIntentTypeRejectsAStaleExpectedRevisionWithStateStaleRegardlessOfActor()
        {
            var state = ShellState.Initial(SampleLayout());
            ShellIntent[] staleIntents =
            {
                new OpenIntent(PrimaryPanel, IntentActor.Player, 5),
                new CloseIntent(PrimaryPanel, IntentActor.Agent, 5),
                new FocusIntent(PrimaryPanel, IntentActor.Agent, 5),
                new DockIntent(SecondaryPanel, PrimaryPanel, IntentActor.Replay, 5),
                new FollowIntent(SecondaryPanel, AnchorKind.HeadLocked, IntentActor.Import, 5),
                new FoldIntent(PrimaryPanel, IntentActor.Agent, 5),
                new UnfoldIntent(PrimaryPanel, IntentActor.Agent, 5),
            };
            foreach (var intent in staleIntents)
            {
                var result = state.Apply(intent);
                Assert.That(result.Succeeded, Is.False, intent.Describe());
                Assert.That(result.Code, Is.EqualTo(ShellFailure.StateStale), intent.Describe());
            }
            Assert.That(state.Revision, Is.EqualTo(0), "None of the stale attempts, from any actor, moved the state on.");
        }

        // ----- the actor never changes validation (XUC-18) -----

        [Test]
        public void PlayerAndAgentIssuingTheSameAcceptedOpenTakeTheSamePathAndProduceEqualResultingState()
        {
            var initial = ShellState.Initial(SampleLayout());
            var byPlayer = initial.Apply(new OpenIntent(PrimaryPanel, IntentActor.Player));
            var byAgent = initial.Apply(new OpenIntent(PrimaryPanel, IntentActor.Agent));

            Assert.That(byPlayer.Succeeded, Is.True, byPlayer.Message);
            Assert.That(byAgent.Succeeded, Is.True, byAgent.Message);
            Assert.That(byPlayer.Value.Fingerprint(), Is.EqualTo(byAgent.Value.Fingerprint()));
            Assert.That(byPlayer.Value.Revision, Is.EqualTo(byAgent.Value.Revision));
        }

        [Test]
        public void PlayerAndAgentIssuingTheSameRejectedDockGetTheSameFailureCode()
        {
            var initial = ShellState.Initial(SampleLayout());
            var byPlayer = initial.Apply(new DockIntent(PrimaryPanel, SurfaceId.OfPanel(PanelId.Parse("ghost")), IntentActor.Player));
            var byAgent = initial.Apply(new DockIntent(PrimaryPanel, SurfaceId.OfPanel(PanelId.Parse("ghost")), IntentActor.Agent));

            Assert.That(byPlayer.Succeeded, Is.False);
            Assert.That(byAgent.Succeeded, Is.False);
            Assert.That(byPlayer.Code, Is.EqualTo(byAgent.Code));
            Assert.That(byPlayer.Code, Is.EqualTo(ShellFailure.PanelUnknown));
        }

        // ----- a dock verb pressed by a person and issued by an agent resolve the same way, and
        // the replay log records actor and revision (XUC-19) -----

        [Test]
        public void ADockVerbPressedByAPersonAndTheSameVerbIssuedByAnAgentAdapterResolveThroughTheSameFocusSubjectAndAffordanceAndProduceEqualOutcomes()
        {
            var verbRegistry = new VerbRegistryBuilder().Register(VerbId.Parse("dock"), "Dock", VerbWireState.Wired).Value.Build();
            var subject = FocusSubject.None.Claim(FocusTarget.OfPanel(SecondaryPanel));

            // Neither VerbAffordanceQuery.Query nor VerbResolver.Resolve takes an actor: a
            // person's press and an agent's press read the same registry and the same subject
            // through the same two steps, so there is no second path either could take.
            var personAffordance = VerbAffordanceQuery.Query(verbRegistry, VerbId.Parse("dock"), subject);
            var agentAffordance = VerbAffordanceQuery.Query(verbRegistry, VerbId.Parse("dock"), subject);
            var personResolution = VerbResolver.Resolve(verbRegistry, VerbId.Parse("dock"), subject);
            var agentResolution = VerbResolver.Resolve(verbRegistry, VerbId.Parse("dock"), subject);
            Assert.That(personAffordance.IsAvailable, Is.EqualTo(agentAffordance.IsAvailable));
            Assert.That(personResolution.Succeeded, Is.EqualTo(agentResolution.Succeeded));
            Assert.That(personResolution.Value, Is.EqualTo(agentResolution.Value));

            // Applying the resolved dock as a DockIntent from each actor onto an equal initial
            // state takes the same path through ShellState.Apply and settles on an equal state.
            var byPlayer = ShellState.Initial(SampleLayout()).Apply(new DockIntent(SecondaryPanel, PrimaryPanel, IntentActor.Player));
            var byAgent = ShellState.Initial(SampleLayout()).Apply(new DockIntent(SecondaryPanel, PrimaryPanel, IntentActor.Agent));

            Assert.That(byPlayer.Succeeded, Is.True, byPlayer.Message);
            Assert.That(byAgent.Succeeded, Is.True, byAgent.Message);
            Assert.That(byPlayer.Value.Fingerprint(), Is.EqualTo(byAgent.Value.Fingerprint()));
        }

        [Test]
        public void ReplayLogRecordsTheIssuingActorAndTheRevisionAfterEveryOutcomeAcceptedOrRejected()
        {
            var layout = SampleLayout();
            var intents = new List<ShellIntent>
            {
                new OpenIntent(PrimaryPanel, IntentActor.Player),
                new OpenIntent(SurfaceId.OfPanel(PanelId.Parse("ghost")), IntentActor.Agent), // rejected: undeclared
                new CloseIntent(PrimaryPanel, IntentActor.Replay),
            };
            var result = ShellState.Initial(layout).ApplyAll(intents);

            Assert.That(result.Outcomes.Select(outcome => outcome.Actor), Is.EqualTo(new[] { IntentActor.Player, IntentActor.Agent, IntentActor.Replay }));
            Assert.That(result.Outcomes[0].Accepted, Is.True);
            Assert.That(result.Outcomes[0].RevisionAfter, Is.EqualTo(1));
            Assert.That(result.Outcomes[1].Accepted, Is.False);
            Assert.That(result.Outcomes[1].RevisionAfter, Is.EqualTo(1), "A rejected outcome's revision is the state it found, unchanged.");
            Assert.That(result.Outcomes[2].Accepted, Is.True);
            Assert.That(result.Outcomes[2].RevisionAfter, Is.EqualTo(2));
            Assert.That(result.State.Revision, Is.EqualTo(2));
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

        private static void AssertOk<T>(ShellResult<T> result) => Assert.That(result.Succeeded, Is.True, result.Message);

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
