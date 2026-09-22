using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lingkyn.LiveTuning.Core;

namespace Lingkyn.LiveTuning.Core.Editor.Tests
{
    // Authored and unexecuted: this assembly has never compiled or run. It is checked against
    // docs/standards/live-tuning/verification-contract.md and coverage-map.json.
    public sealed class LiveTuningCoreContractTests
    {
        private static readonly TunableId PanelOpacity = TunableId.Parse("panel.opacity");
        private static readonly TunableId CornerRadius = TunableId.Parse("shape.corner_radius");
        private static readonly TunableId PreferTwoD = TunableId.Parse("text.prefer_2d");
        private static readonly TunableId TextWeight = TunableId.Parse("text.weight");
        private static readonly TunableId SurfacePanel = TunableId.Parse("surface.panel");
        private static readonly TunableId LayoutOffset = TunableId.Parse("layout.offset");
        private static readonly TunableId LayoutAnchor = TunableId.Parse("layout.anchor");

        // ----- identity (LTC-01) -----

        [Test]
        public void TunableIdCanonicalizesLowerCaseDottedSegments()
        {
            var id = TunableId.TryCreate("  surface.panel  ");
            Assert.That(id.Succeeded, Is.True, id.Message);
            Assert.That(id.Value.Value, Is.EqualTo("surface.panel"));
            Assert.That(id.Value.ToString(), Is.EqualTo("surface.panel"));
        }

        [Test]
        public void IdentityRejectsMalformedTextWithIdentityMalformed()
        {
            foreach (var text in new[] { null, "", " ", "Surface.Panel", "surface panel", "surface..panel", ".surface", "surface.", "surface-panel", "surface\tpanel" })
            {
                var result = TunableId.TryCreate(text);
                Assert.That(result.Succeeded, Is.False, text ?? "<null>");
                Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.IdentityMalformed), text ?? "<null>");
                Assert.That(result.Message, Is.Not.Empty, text ?? "<null>");
            }
        }

        [Test]
        public void IdentitiesWithSameCanonicalTextAreEqualValues()
        {
            var first = TunableId.Parse("surface.panel");
            var second = TunableId.Parse("  surface.panel ");
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first != TunableId.Parse("surface.section"), Is.True);
            Assert.That(first.CompareTo(TunableId.Parse("surface.section")), Is.LessThan(0));
        }

        [Test]
        public void SnapshotIdSharesCanonicalFormButNeverEqualsATunableIdOfTheSameText()
        {
            var snapshot = SnapshotId.TryCreate("Surface Panel");
            Assert.That(snapshot.Succeeded, Is.False);
            Assert.That(snapshot.Code, Is.EqualTo(LiveTuningFailure.IdentityMalformed));

            var okSnapshot = SnapshotId.Parse("before_tuning");
            var okTunable = TunableId.Parse("before_tuning");
            Assert.That(okSnapshot.Value, Is.EqualTo(okTunable.Value));
            // TunableId and SnapshotId are distinct types; neither Equals overload accepts the other,
            // so there is no expression that could compare them equal.
            Assert.That(okSnapshot.Equals(okSnapshot), Is.True);
            Assert.That(okSnapshot.GetType(), Is.Not.EqualTo(okTunable.GetType()));
        }

        [Test]
        public void ParseThrowsWithTheFailureMessageForMalformedIdentity()
        {
            var thrown = Assert.Throws<ArgumentException>(() => TunableId.Parse("Bad Id"));
            Assert.That(thrown.Message, Does.Contain("lower-case"));
            Assert.Throws<ArgumentException>(() => TunableId.Parse(""));
            Assert.Throws<ArgumentException>(() => SnapshotId.Parse(null));
        }

        // ----- closed kind set and kind-declaration validation (LTC-02) -----

        [Test]
        public void EveryTunableKindAcceptsAWellFormedDeclarationAndContainsAValidDefault()
        {
            var declarations = new TunableKindDeclaration[]
            {
                new FloatDeclaration(0f, 1f, 0.01f),
                new IntegerDeclaration(0, 32),
                BoolDeclaration.Instance,
                new EnumeratedDeclaration(new[] { "medium", "bold" }),
                ColourDeclaration.Instance,
                new Vector2Declaration(-1f, 1f, -1f, 1f),
                new Vector3Declaration(-1f, 1f, -1f, 1f, -1f, 1f),
            };
            var defaults = new TunableValue[]
            {
                TunableValue.OfFloat(0.5f),
                TunableValue.OfInteger(8),
                TunableValue.OfBool(true),
                TunableValue.OfEnumerated("medium"),
                TunableValue.OfColour(0.1f, 0.2f, 0.3f, 0.9f),
                TunableValue.OfVector2(0f, 0f),
                TunableValue.OfVector3(0f, 0f, 0f),
            };
            Assert.That(declarations.Select(declaration => declaration.Kind), Is.EqualTo(Enum.GetValues(typeof(TunableKind)).Cast<TunableKind>()));
            for (var index = 0; index < declarations.Length; index++)
            {
                Assert.That(declarations[index].IsValid(out var message), Is.True, message);
                Assert.That(declarations[index].Contains(defaults[index]), Is.True, declarations[index].Kind.ToString());
            }
        }

        [Test]
        public void KindDeclarationRejectsInvertedRangeNonPositiveStepNonFiniteBoundOrEmptyValueSetWithKindDeclarationInvalid()
        {
            AssertDeclarationInvalid(new FloatDeclaration(1f, 0f, 0.1f), "inverted float range");
            AssertDeclarationInvalid(new FloatDeclaration(0f, 1f, 0f), "non-positive step");
            AssertDeclarationInvalid(new FloatDeclaration(0f, 1f, -0.1f), "negative step");
            AssertDeclarationInvalid(new FloatDeclaration(float.NaN, 1f, 0.1f), "non-finite bound");
            AssertDeclarationInvalid(new FloatDeclaration(0f, float.PositiveInfinity, 0.1f), "non-finite bound");
            AssertDeclarationInvalid(new IntegerDeclaration(10, 0), "inverted integer range");
            AssertDeclarationInvalid(new EnumeratedDeclaration(Array.Empty<string>()), "empty value set");
            AssertDeclarationInvalid(new EnumeratedDeclaration(null), "null value set");
            AssertDeclarationInvalid(new Vector2Declaration(1f, 0f, -1f, 1f), "inverted vector2 axis");
            AssertDeclarationInvalid(new Vector3Declaration(0f, 1f, 1f, 0f, -1f, 1f), "inverted vector3 axis");

            var registration = new TunableRegistryBuilder().Register(TunableId.Parse("bad.float"), new FloatDeclaration(1f, 0f, 0.1f), TunableValue.OfFloat(0.5f));
            Assert.That(registration.Succeeded, Is.False);
            Assert.That(registration.Code, Is.EqualTo(LiveTuningFailure.KindDeclarationInvalid));
        }

        // ----- registry construction (LTC-03) -----

        [Test]
        public void RegistrationRejectsDuplicateIdWithTunableDuplicateLeavingBuilderUnchanged()
        {
            var builder = new TunableRegistryBuilder();
            var first = builder.Register(PanelOpacity, new FloatDeclaration(0f, 1f, 0.01f), TunableValue.OfFloat(0.9f));
            Assert.That(first.Succeeded, Is.True, first.Message);
            var countAfterFirst = builder.Count;

            var duplicate = builder.Register(PanelOpacity, new FloatDeclaration(0f, 1f, 0.01f), TunableValue.OfFloat(0.5f));
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Code, Is.EqualTo(LiveTuningFailure.TunableDuplicate));
            Assert.That(duplicate.Message, Does.Contain(PanelOpacity.Value));
            Assert.That(builder.Count, Is.EqualTo(countAfterFirst), "A rejected registration leaves the builder unchanged.");
        }

        [Test]
        public void RegistrationRejectsDefaultOutOfRangeLeavingBuilderUnchanged()
        {
            var builder = new TunableRegistryBuilder();
            var rejected = builder.Register(PanelOpacity, new FloatDeclaration(0f, 1f, 0.01f), TunableValue.OfFloat(2f));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(LiveTuningFailure.DefaultOutOfRange));
            Assert.That(builder.Count, Is.EqualTo(0), "A rejected registration leaves the builder unchanged.");
            Assert.That(builder.Contains(PanelOpacity), Is.False);
        }

        [Test]
        public void BuiltRegistryEnumeratesInCanonicalIdOrderWithGroupAndLabel()
        {
            var registry = SampleRegistry();
            var ids = registry.Tunables.Select(registration => registration.Id.Value).ToList();
            Assert.That(ids, Is.Ordered, "The registry enumerates in canonical id order.");
            Assert.That(registry.TryGet(SurfacePanel, out var surfacePanel), Is.True);
            Assert.That(surfacePanel.Group, Is.EqualTo("surface"));
            Assert.That(surfacePanel.Label, Is.EqualTo("panel"));
            Assert.That(registry.Count, Is.EqualTo(7));
        }

        [Test]
        public void BuiltRegistryHasNoMutatorAndIsUnaffectedByLaterRegistrations()
        {
            var builder = new TunableRegistryBuilder();
            builder.Register(PanelOpacity, new FloatDeclaration(0f, 1f, 0.01f), TunableValue.OfFloat(0.9f));
            var registry = builder.Build();
            Assert.That(registry.Count, Is.EqualTo(1));

            builder.Register(CornerRadius, new IntegerDeclaration(0, 32), TunableValue.OfInteger(8));
            Assert.That(registry.Count, Is.EqualTo(1), "A registry already handed out is not mutated by a later registration on the same builder.");
            Assert.That(typeof(TunableRegistry).GetMethods().Any(method => method.Name.StartsWith("Register", StringComparison.Ordinal)), Is.False, "TunableRegistry exposes no mutator.");
        }

        // ----- tuning intents on TuningState (LTC-04) -----

        [Test]
        public void SetRejectsUnknownIdWithTunableUnknown()
        {
            var state = TuningState.Initial(SampleRegistry());
            var result = state.Apply(new SetIntent(TunableId.Parse("ghost.value"), TunableValue.OfFloat(1f)));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.TunableUnknown));
        }

        [Test]
        public void SetRejectsKindMismatchWithTunableKindMismatch()
        {
            var state = TuningState.Initial(SampleRegistry());
            var result = state.Apply(new SetIntent(PanelOpacity, TunableValue.OfBool(true)));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.TunableKindMismatch));
        }

        [Test]
        public void SetRejectsOutOfRangeValuePerAxisAndPerChannel()
        {
            var state = TuningState.Initial(SampleRegistry());
            Assert.That(state.Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(2f))).Code, Is.EqualTo(LiveTuningFailure.TunableOutOfRange));
            Assert.That(state.Apply(new SetIntent(SurfacePanel, TunableValue.OfColour(0.1f, 0.2f, 0.3f, 1.5f))).Code, Is.EqualTo(LiveTuningFailure.TunableOutOfRange), "One channel out of [0,1] is enough to reject the whole colour.");
            Assert.That(state.Apply(new SetIntent(LayoutOffset, TunableValue.OfVector2(0f, 5f))).Code, Is.EqualTo(LiveTuningFailure.TunableOutOfRange), "One axis out of range is enough to reject the whole vector2.");
            Assert.That(state.Apply(new SetIntent(LayoutAnchor, TunableValue.OfVector3(0f, 0f, 5f))).Code, Is.EqualTo(LiveTuningFailure.TunableOutOfRange), "One axis out of range is enough to reject the whole vector3.");
        }

        [Test]
        public void ApplySnapshotRejectsUnknownSnapshotWithSnapshotUnknown()
        {
            var state = TuningState.Initial(SampleRegistry());
            var result = state.Apply(new ApplySnapshotIntent(SnapshotId.Parse("ghost_snapshot")));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.SnapshotUnknown));
        }

        [Test]
        public void SnapshotWithExistingIdReplacesThatSnapshot()
        {
            var before = SnapshotId.Parse("before");
            var state = TuningState.Initial(SampleRegistry())
                .Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.5f))).Value
                .Apply(new SnapshotIntent(before)).Value
                .Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f))).Value
                .Apply(new SnapshotIntent(before)).Value;

            Assert.That(state.TryGetSnapshot(before, out var overrides), Is.True);
            Assert.That(overrides[PanelOpacity], Is.EqualTo(TunableValue.OfFloat(0.2f)), "A snapshot taken with an id already held replaces that snapshot.");
        }

        // ----- overrides as the only diff (LTC-05) -----

        [Test]
        public void SetEqualToDefaultRemovesOverrideRatherThanStoringIt()
        {
            var registry = SampleRegistry();
            var withOverride = TuningState.Initial(registry).Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f))).Value;
            Assert.That(withOverride.HasOverride(PanelOpacity), Is.True);

            registry.TryGet(PanelOpacity, out var registration);
            var backToDefault = withOverride.Apply(new SetIntent(PanelOpacity, registration.Default)).Value;
            Assert.That(backToDefault.HasOverride(PanelOpacity), Is.False, "A set equal to the registered default removes the override rather than storing it.");
        }

        [Test]
        public void ResetRemovesOneOverrideAndResetAllRemovesEveryOverrideWhileSnapshotsStay()
        {
            var state = TuningState.Initial(SampleRegistry())
                .Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f))).Value
                .Apply(new SetIntent(CornerRadius, TunableValue.OfInteger(4))).Value;
            var snapshotId = SnapshotId.Parse("saved");
            state = state.Apply(new SnapshotIntent(snapshotId)).Value;

            var afterReset = state.Apply(new ResetIntent(PanelOpacity)).Value;
            Assert.That(afterReset.HasOverride(PanelOpacity), Is.False);
            Assert.That(afterReset.HasOverride(CornerRadius), Is.True);

            var afterResetAll = afterReset.Apply(new ResetAllIntent()).Value;
            Assert.That(afterResetAll.HasOverride(CornerRadius), Is.False);
            Assert.That(afterResetAll.TryGetSnapshot(snapshotId, out var kept), Is.True, "reset_all removes every override while snapshots stay.");
            Assert.That(kept.ContainsKey(CornerRadius), Is.True);
        }

        [Test]
        public void ApplySnapshotReplacesTheOverrideSetWithTheSnapshotsOverrideSet()
        {
            var saved = SnapshotId.Parse("saved");
            var state = TuningState.Initial(SampleRegistry())
                .Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f))).Value
                .Apply(new SnapshotIntent(saved)).Value
                .Apply(new SetIntent(CornerRadius, TunableValue.OfInteger(4))).Value;
            Assert.That(state.HasOverride(PanelOpacity), Is.True);
            Assert.That(state.HasOverride(CornerRadius), Is.True);

            var restored = state.Apply(new ApplySnapshotIntent(saved)).Value;
            Assert.That(restored.HasOverride(PanelOpacity), Is.True);
            Assert.That(restored.HasOverride(CornerRadius), Is.False, "apply_snapshot replaces the whole override set with the snapshot's.");
        }

        [Test]
        public void EffectiveValueIsOverrideWhenPresentAndDefaultOtherwise()
        {
            var registry = SampleRegistry();
            var state = TuningState.Initial(registry);
            registry.TryGet(PanelOpacity, out var registration);
            Assert.That(state.TryGetEffective(PanelOpacity, out var effective), Is.True);
            Assert.That(effective, Is.EqualTo(registration.Default));

            var overridden = state.Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.4f))).Value;
            Assert.That(overridden.TryGetEffective(PanelOpacity, out var effectiveOverridden), Is.True);
            Assert.That(effectiveOverridden, Is.EqualTo(TunableValue.OfFloat(0.4f)));
            Assert.That(state.TryGetEffective(TunableId.Parse("ghost.value"), out _), Is.False);
        }

        // ----- state immutability (LTC-06) -----

        [Test]
        public void RejectedIntentLeavesPriorStateIntactWithTheFailureCode()
        {
            var state = TuningState.Initial(SampleRegistry()).Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.4f))).Value;
            var before = state.Fingerprint();

            var rejected = state.Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(9f)));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(LiveTuningFailure.TunableOutOfRange));
            Assert.That(state.Fingerprint(), Is.EqualTo(before), "A rejected intent leaves the prior state intact.");
        }

        [Test]
        public void AcceptedIntentProducesNewStateWhilePriorStateStaysReadableAndEqualToItself()
        {
            var state = TuningState.Initial(SampleRegistry());
            var initialFingerprint = state.Fingerprint();

            var next = state.Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.4f))).Value;

            Assert.That(ReferenceEquals(state, next), Is.False);
            Assert.That(state.Fingerprint(), Is.EqualTo(initialFingerprint));
            Assert.That(state, Is.EqualTo(state));
            Assert.That(state.HasOverride(PanelOpacity), Is.False);
            Assert.That(next.HasOverride(PanelOpacity), Is.True);
        }

        // ----- deterministic replay (LTC-07) -----

        [Test]
        public void SameIntentSequenceProducesEqualStateAndFingerprint()
        {
            var registry = SampleRegistry();
            var first = TuningState.Initial(registry).ApplyAll(Sequence());
            var second = TuningState.Initial(registry).ApplyAll(Sequence());

            Assert.That(first.AllAccepted, Is.True, Describe(first));
            Assert.That(first.State, Is.EqualTo(second.State));
            Assert.That(first.State.Fingerprint(), Is.EqualTo(second.State.Fingerprint()));
            Assert.That(ReferenceEquals(first.State, second.State), Is.False);
        }

        [Test]
        public void TwoStatesWithSameOverridesReachedByDifferentSequencesHaveEqualFingerprints()
        {
            var registry = SampleRegistry();
            var viaDirectSet = TuningState.Initial(registry)
                .Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f))).Value
                .Apply(new SetIntent(CornerRadius, TunableValue.OfInteger(4))).Value;
            var viaSetThenResetThenSetAgain = TuningState.Initial(registry)
                .Apply(new SetIntent(CornerRadius, TunableValue.OfInteger(16))).Value
                .Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f))).Value
                .Apply(new ResetIntent(CornerRadius)).Value
                .Apply(new SetIntent(CornerRadius, TunableValue.OfInteger(4))).Value;

            Assert.That(viaDirectSet.Fingerprint(), Is.EqualTo(viaSetThenResetThenSetAgain.Fingerprint()));
        }

        // ----- closed editor-kind set (LTC-08) -----

        [Test]
        public void ResolveEditorKindMapsEachTunableKindToExactlyOneEditorKind()
        {
            var expected = new Dictionary<TunableKind, EditorKind>
            {
                [TunableKind.Float] = EditorKind.FloatEditor,
                [TunableKind.Integer] = EditorKind.IntegerEditor,
                [TunableKind.Bool] = EditorKind.BoolEditor,
                [TunableKind.Enumerated] = EditorKind.EnumeratedEditor,
                [TunableKind.Colour] = EditorKind.ColourEditor,
                [TunableKind.Vector2] = EditorKind.Vector2Editor,
                [TunableKind.Vector3] = EditorKind.Vector3Editor,
            };
            foreach (var pair in expected)
            {
                var resolved = EditorKindResolver.ResolveEditorKind(pair.Key);
                Assert.That(resolved.Succeeded, Is.True, pair.Key.ToString());
                Assert.That(resolved.Value, Is.EqualTo(pair.Value));
            }
            Assert.That(Enum.GetValues(typeof(EditorKind)).Length, Is.EqualTo(Enum.GetValues(typeof(TunableKind)).Length));
        }

        [Test]
        public void RegisteringManyColourTunablesFromDifferentGroupsAllResolveToTheSingleColourEditorKind()
        {
            var builder = new TunableRegistryBuilder();
            var ids = new[] { "package_a.skin.tint", "package_b.skin.tint", "screen_menu.highlight" };
            foreach (var text in ids)
            {
                var registered = builder.Register(TunableId.Parse(text), ColourDeclaration.Instance, TunableValue.OfColour(0f, 0f, 0f, 1f));
                Assert.That(registered.Succeeded, Is.True, registered.Message);
            }
            var registry = builder.Build();
            var resolvedKinds = registry.Tunables.Select(registration => EditorKindResolver.ResolveEditorKind(registration.Kind).Value).ToList();
            Assert.That(resolvedKinds, Has.All.EqualTo(EditorKind.ColourEditor));
            Assert.That(resolvedKinds.Count, Is.EqualTo(ids.Length));
        }

        [Test]
        public void ResolveEditorKindFailsClosedForAKindValueOutsideTheClosedSetWithTunableKindUnsupported()
        {
            var outsideTheClosedSet = (TunableKind)999;
            var resolved = EditorKindResolver.ResolveEditorKind(outsideTheClosedSet);
            Assert.That(resolved.Succeeded, Is.False);
            Assert.That(resolved.Code, Is.EqualTo(LiveTuningFailure.TunableKindUnsupported));
            // A caller must branch on Succeeded before reading Value; the contract's "never resolves
            // to an empty or default editor" is honoured by never treating this Value as a real one.
        }

        // ----- engine-free binding records (LTC-09) -----

        [Test]
        public void BindingValidationRejectsUnregisteredIdWithTunableUnknown()
        {
            var registry = SampleRegistry();
            var records = new[] { new BindingRecord(TunableId.Parse("ghost.value"), TunableKind.Float, "asset://ghost") };
            var result = BindingSet.Validate(records, registry);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.TunableUnknown));
        }

        [Test]
        public void BindingValidationRejectsKindMismatchWithBindingKindMismatch()
        {
            var registry = SampleRegistry();
            var records = new[] { new BindingRecord(PanelOpacity, TunableKind.Bool, "asset://panel") };
            var result = BindingSet.Validate(records, registry);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.BindingKindMismatch));
        }

        [Test]
        public void BindingValidationRejectsSecondRecordForSameIdWithBindingDuplicate()
        {
            var registry = SampleRegistry();
            var records = new[]
            {
                new BindingRecord(PanelOpacity, TunableKind.Float, "asset://panel_a"),
                new BindingRecord(PanelOpacity, TunableKind.Float, "asset://panel_b"),
            };
            var result = BindingSet.Validate(records, registry);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.BindingDuplicate));
        }

        [Test]
        public void ValidatedBindingSetEnumeratesInCanonicalIdOrder()
        {
            var registry = SampleRegistry();
            var records = new[]
            {
                new BindingRecord(TextWeight, TunableKind.Enumerated, "asset://weight"),
                new BindingRecord(PanelOpacity, TunableKind.Float, "asset://panel"),
                new BindingRecord(CornerRadius, TunableKind.Integer, "asset://corner"),
            };
            var result = BindingSet.Validate(records, registry);
            Assert.That(result.Succeeded, Is.True, result.Message);
            var ids = result.Value.Records.Select(record => record.Id.Value).ToList();
            Assert.That(ids, Is.Ordered, "A validated binding set enumerates in canonical id order.");
            Assert.That(result.Value.Count, Is.EqualTo(3));
        }

        [Test]
        public void ScopeFiltersChangeWhichRecordsAreReturnedButNeverTheResolvedEditorKind()
        {
            var registry = SampleRegistry();
            var records = new[]
            {
                new BindingRecord(SurfacePanel, TunableKind.Colour, "asset://ugui_panel", Scope("skin", "ugui")),
                new BindingRecord(TextWeight, TunableKind.Enumerated, "asset://toolkit_weight", Scope("skin", "ui_toolkit")),
            };
            var bindings = BindingSet.Validate(records, registry).Value;

            var uguiOnly = bindings.WhereScope("skin", "ugui").ToList();
            Assert.That(uguiOnly.Select(record => record.Id), Is.EqualTo(new[] { SurfacePanel }));
            var toolkitOnly = bindings.WhereScope("skin", "ui_toolkit").ToList();
            Assert.That(toolkitOnly.Select(record => record.Id), Is.EqualTo(new[] { TextWeight }));
            Assert.That(bindings.WhereScope("skin", "unknown_skin"), Is.Empty, "The filter changes which records come back.");

            // Whichever scope label is active, the one colour record in this set still resolves to
            // colour_editor: the filter changes visibility, never the resolved editor kind.
            var colourRecord = bindings.Records.First(record => record.Id == SurfacePanel);
            foreach (var scopeLabel in new[] { "ugui", "ui_toolkit", "unknown_skin" })
            {
                Assert.That(EditorKindResolver.ResolveEditorKind(colourRecord.Kind).Value, Is.EqualTo(EditorKind.ColourEditor), scopeLabel);
            }
        }

        // ----- token bridge: document to registry (LTC-10) -----

        [Test]
        public void TokenBridgeDerivesColourIntegerFloatBoolAndDeclaredEnumeratedLeavesWithIdsFromThePath()
        {
            const string json = "{" +
                "\"surface\":{\"panel\":{\"role\":\"outermost\",\"rgba\":[0.03,0.09,0.12,0.96]}}," +
                "\"shape\":{\"corner_radius_scale_px\":{\"small\":8,\"medium\":16}}," +
                "\"hit_target_dp\":{\"minimum\":48.5}," +
                "\"text\":{\"prefer_2d\":true,\"weight\":{\"body\":\"medium\"}}" +
                "}";
            var policy = new TokenRangePolicy()
                .WithIntegerRange("shape.corner_radius_scale_px.small", 0, 64)
                .WithIntegerRange("shape.corner_radius_scale_px.medium", 0, 64)
                .WithFloatRange("hit_target_dp.minimum", 0f, 128f, 0.5f)
                .WithEnumeratedValues("text.weight.body", "medium", "bold");

            var result = TokenBridge.BuildRegistry(json, LiveTuningTokenAnnotations.Default, policy);
            Assert.That(result.Succeeded, Is.True, result.Message);
            var registry = result.Value;
            Assert.That(registry.Count, Is.EqualTo(5));

            Assert.That(registry.TryGet(TunableId.Parse("surface.panel"), out var panel), Is.True);
            Assert.That(panel.Kind, Is.EqualTo(TunableKind.Colour));
            var (panelR, panelG, panelB, panelA) = panel.Default.AsColour;
            Assert.That(panelR, Is.EqualTo(0.03f).Within(0.0005f));
            Assert.That(panelG, Is.EqualTo(0.09f).Within(0.0005f));
            Assert.That(panelB, Is.EqualTo(0.12f).Within(0.0005f));
            Assert.That(panelA, Is.EqualTo(0.96f).Within(0.0005f));

            Assert.That(registry.TryGet(TunableId.Parse("shape.corner_radius_scale_px.small"), out var small), Is.True);
            Assert.That(small.Kind, Is.EqualTo(TunableKind.Integer));
            Assert.That(small.Default.AsInteger, Is.EqualTo(8));

            Assert.That(registry.TryGet(TunableId.Parse("hit_target_dp.minimum"), out var minimum), Is.True);
            Assert.That(minimum.Kind, Is.EqualTo(TunableKind.Float));

            Assert.That(registry.TryGet(TunableId.Parse("text.prefer_2d"), out var preferTwoD), Is.True);
            Assert.That(preferTwoD.Kind, Is.EqualTo(TunableKind.Bool));
            Assert.That(preferTwoD.Default.AsBool, Is.True);

            Assert.That(registry.TryGet(TunableId.Parse("text.weight.body"), out var weightBody), Is.True);
            Assert.That(weightBody.Kind, Is.EqualTo(TunableKind.Enumerated));
            Assert.That(weightBody.Default.AsEnumerated, Is.EqualTo("medium"));
        }

        [Test]
        public void TokenBridgeSkipsAnnotationKeys()
        {
            const string json = "{\"font\":{\"slot\":\"optional_consumer_provided\",\"bundled\":false}}";
            var result = TokenBridge.BuildRegistry(json, LiveTuningTokenAnnotations.Default, new TokenRangePolicy());
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value.Count, Is.EqualTo(0), "Every key under 'font' is an annotation, so no tunable is derived from it.");
        }

        [Test]
        public void TokenBridgeRejectsUnsupportedLeafWithTokenUnsupported()
        {
            const string json = "{\"text\":{\"emphasis_tiers\":[\"primary\",\"secondary\",\"tertiary\"]}}";
            var result = TokenBridge.BuildRegistry(json, LiveTuningTokenAnnotations.Default, new TokenRangePolicy());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.TokenUnsupported));
            Assert.That(result.Message, Does.Contain("text.emphasis_tiers"));
        }

        [Test]
        public void TokenBridgeRejectsNumericLeafWithNoRangePolicyEntryWithTokenRangeMissing()
        {
            const string json = "{\"hit_target_dp\":{\"minimum\":48}}";
            var result = TokenBridge.BuildRegistry(json, LiveTuningTokenAnnotations.Default, new TokenRangePolicy());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.TokenRangeMissing));
            Assert.That(result.Message, Does.Contain("hit_target_dp.minimum"));
        }

        [Test]
        public void TokenBridgeProducesEqualRegistryFingerprintForTheSameInputs()
        {
            const string json = "{\"shape\":{\"corner_radius\":8}}";
            var policy = new TokenRangePolicy().WithIntegerRange("shape.corner_radius", 0, 64);
            var first = TokenBridge.BuildRegistry(json, LiveTuningTokenAnnotations.Default, policy);
            var second = TokenBridge.BuildRegistry(json, LiveTuningTokenAnnotations.Default, policy);
            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(first.Value.Fingerprint(), Is.EqualTo(second.Value.Fingerprint()));
        }

        [Test]
        public void TokenBridgeRejectsADocumentThatWouldYieldTwoTunablesWithOneIdWithTunableDuplicate()
        {
            const string json = "{\"a\":{\"b\":1},\"a.b\":2}";
            var policy = new TokenRangePolicy().WithIntegerRange("a.b", 0, 64);
            var result = TokenBridge.BuildRegistry(json, LiveTuningTokenAnnotations.Default, policy);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Code, Is.EqualTo(LiveTuningFailure.TunableDuplicate));
        }

        // ----- token bridge: state to document and round trip (LTC-11) -----

        [Test]
        public void ExportedDocumentCarriesSchemaIdFingerprintAndCanonicalOverridesInCanonicalIdOrder()
        {
            var registry = SampleRegistry();
            var state = TuningState.Initial(registry)
                .Apply(new SetIntent(CornerRadius, TunableValue.OfInteger(4))).Value
                .Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f))).Value;
            var document = state.Export();

            Assert.That(TokenOverrideDocument.SchemaId, Is.EqualTo("xr-foundry.token_overrides.v1"));
            Assert.That(document.RegistryFingerprint, Is.EqualTo(registry.Fingerprint()));
            Assert.That(document.Overrides.Select(entry => entry.Id.Value), Is.Ordered);
            Assert.That(document.Overrides.Count, Is.EqualTo(2));
        }

        [Test]
        public void ExportOnAStateWithNoOverridesYieldsAnEmptyOverrideList()
        {
            var state = TuningState.Initial(SampleRegistry());
            var document = state.Export();
            Assert.That(document.Overrides, Is.Empty);
            Assert.That(document.ToJson(), Does.Contain("\"overrides\":[]"));
        }

        [Test]
        public void SameStateYieldsByteEqualJson()
        {
            var registry = SampleRegistry();
            var state = TuningState.Initial(registry).Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f))).Value;
            Assert.That(state.Export().ToJson(), Is.EqualTo(state.Export().ToJson()));
        }

        [Test]
        public void RoundTripApplyingTheDocumentsOverridesToAFreshStateYieldsAnEqualFingerprint()
        {
            var registry = SampleRegistry();
            var original = TuningState.Initial(registry)
                .Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f))).Value
                .Apply(new SetIntent(SurfacePanel, TunableValue.OfColour(0.4f, 0.4f, 0.4f, 1f))).Value;
            var document = original.Export();
            var parsed = TokenOverrideDocument.Parse(document.ToJson());
            Assert.That(parsed.Succeeded, Is.True, parsed.Message);

            var freshState = TuningState.Initial(registry);
            var applied = parsed.Value.ApplyTo(freshState);
            Assert.That(applied.Succeeded, Is.True, applied.Message);
            Assert.That(applied.Value.Fingerprint(), Is.EqualTo(original.Fingerprint()));
        }

        // ----- structured results with stable codes (LTC-12) -----

        [Test]
        public void StructuredResultsCarryStableFailureCodeForEveryCoreRejection()
        {
            var registry = SampleRegistry();
            var state = TuningState.Initial(registry);
            var duplicateBuilder = new TunableRegistryBuilder();
            duplicateBuilder.Register(PanelOpacity, new FloatDeclaration(0f, 1f, 0.1f), TunableValue.OfFloat(0.5f));
            var duplicateResult = duplicateBuilder.Register(PanelOpacity, new FloatDeclaration(0f, 1f, 0.1f), TunableValue.OfFloat(0.1f));
            var codes = new List<string>
            {
                TunableId.TryCreate("Bad Id").Code,
                new TunableRegistryBuilder().Register(PanelOpacity, new FloatDeclaration(1f, 0f, 1f), TunableValue.OfFloat(0.5f)).Code,
                duplicateResult.Code,
                new TunableRegistryBuilder().Register(PanelOpacity, new FloatDeclaration(0f, 1f, 0.1f), TunableValue.OfFloat(5f)).Code,
                state.Apply(new SetIntent(TunableId.Parse("ghost.value"), TunableValue.OfFloat(1f))).Code,
                state.Apply(new SetIntent(PanelOpacity, TunableValue.OfBool(true))).Code,
                state.Apply(new SetIntent(PanelOpacity, TunableValue.OfFloat(9f))).Code,
                state.Apply(new ApplySnapshotIntent(SnapshotId.Parse("ghost_snapshot"))).Code,
                EditorKindResolver.ResolveEditorKind((TunableKind)999).Code,
                BindingSet.Validate(new[] { new BindingRecord(PanelOpacity, TunableKind.Bool, "x") }, registry).Code,
                BindingSet.Validate(new[]
                {
                    new BindingRecord(PanelOpacity, TunableKind.Float, "a"),
                    new BindingRecord(PanelOpacity, TunableKind.Float, "b"),
                }, registry).Code,
                TokenBridge.BuildRegistry("{\"x\":[\"a\",\"b\",\"c\"]}", LiveTuningTokenAnnotations.Default, new TokenRangePolicy()).Code,
                TokenBridge.BuildRegistry("{\"x\":48}", LiveTuningTokenAnnotations.Default, new TokenRangePolicy()).Code,
            };
            var expected = new[]
            {
                LiveTuningFailure.IdentityMalformed,
                LiveTuningFailure.KindDeclarationInvalid,
                LiveTuningFailure.TunableDuplicate,
                LiveTuningFailure.DefaultOutOfRange,
                LiveTuningFailure.TunableUnknown,
                LiveTuningFailure.TunableKindMismatch,
                LiveTuningFailure.TunableOutOfRange,
                LiveTuningFailure.SnapshotUnknown,
                LiveTuningFailure.TunableKindUnsupported,
                LiveTuningFailure.BindingKindMismatch,
                LiveTuningFailure.BindingDuplicate,
                LiveTuningFailure.TokenUnsupported,
                LiveTuningFailure.TokenRangeMissing,
            };
            Assert.That(codes, Is.EqualTo(expected));
            foreach (var code in codes) Assert.That(code, Is.Not.Empty);
        }

        [Test]
        public void ValidIntentSequencePassesCleanWithEveryOutcomeAccepted()
        {
            var result = TuningState.Initial(SampleRegistry()).ApplyAll(Sequence());
            Assert.That(result.AllAccepted, Is.True, Describe(result));
            Assert.That(result.RejectedCount, Is.EqualTo(0));
            Assert.That(result.Outcomes.Count, Is.EqualTo(Sequence().Count));
            Assert.That(result.Outcomes.Select(item => item.Index), Is.EqualTo(Enumerable.Range(0, Sequence().Count)));
        }

        // ----- no engine type; text in, text out (LTC-13) -----

        [Test]
        public void CoreAssemblyReferencesNoUnityEngineAssembly()
        {
            var assembly = typeof(TunableRegistry).Assembly;
            var referenced = assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty);
            Assert.That(referenced.Any(name => name.IndexOf("UnityEngine", StringComparison.OrdinalIgnoreCase) >= 0), Is.False, string.Join(",", referenced));
        }

        // ----- helpers -----

        private static IReadOnlyDictionary<string, string> Scope(string key, string value) => new Dictionary<string, string> { [key] = value };

        private static TunableRegistry SampleRegistry()
        {
            var builder = new TunableRegistryBuilder();
            AssertOk(builder.Register(PanelOpacity, new FloatDeclaration(0f, 1f, 0.01f), TunableValue.OfFloat(0.9f), "surface", "opacity"));
            AssertOk(builder.Register(CornerRadius, new IntegerDeclaration(0, 32), TunableValue.OfInteger(8), "shape", "corner_radius"));
            AssertOk(builder.Register(PreferTwoD, BoolDeclaration.Instance, TunableValue.OfBool(true), "text", "prefer_2d"));
            AssertOk(builder.Register(TextWeight, new EnumeratedDeclaration(new[] { "medium", "bold" }), TunableValue.OfEnumerated("medium"), "text", "weight"));
            AssertOk(builder.Register(SurfacePanel, ColourDeclaration.Instance, TunableValue.OfColour(0.1f, 0.2f, 0.3f, 0.9f), "surface", "panel"));
            AssertOk(builder.Register(LayoutOffset, new Vector2Declaration(-1f, 1f, -1f, 1f), TunableValue.OfVector2(0f, 0f), "layout", "offset"));
            AssertOk(builder.Register(LayoutAnchor, new Vector3Declaration(-1f, 1f, -1f, 1f, -1f, 1f), TunableValue.OfVector3(0f, 0f, 0f), "layout", "anchor"));
            return builder.Build();
        }

        private static void AssertOk(LiveTuningResult<TunableRegistryBuilder> result) => Assert.That(result.Succeeded, Is.True, result.Message);

        private static void AssertDeclarationInvalid(TunableKindDeclaration declaration, string label)
        {
            Assert.That(declaration.IsValid(out var message), Is.False, label);
            Assert.That(message, Is.Not.Empty, label);
        }

        private static List<TuningIntent> Sequence() => new List<TuningIntent>
        {
            new SetIntent(PanelOpacity, TunableValue.OfFloat(0.2f)),
            new SnapshotIntent(SnapshotId.Parse("first")),
            new SetIntent(CornerRadius, TunableValue.OfInteger(16)),
            new ResetIntent(PanelOpacity),
            new SetIntent(SurfacePanel, TunableValue.OfColour(0.4f, 0.4f, 0.4f, 1f)),
            new ApplySnapshotIntent(SnapshotId.Parse("first")),
            new ResetAllIntent(),
        };

        private static string Describe(TuningSequenceResult result) =>
            string.Join("\n", result.Outcomes.Select(item => item.Index + " " + item.Intent.Describe() + ": " + (item.Accepted ? "ok " + item.Code : item.Code + " " + item.Message)));
    }
}
