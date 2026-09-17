using System;
using System.Collections.Generic;
using Lingkyn.Locomotion.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace Lingkyn.Locomotion.Unity
{
    // Thin Unity adapter for the Locomotion Core: a ScriptableObject that binds mode
    // identity to XR Interaction Toolkit locomotion providers and the tunneling
    // vignette controller; fail-closed binding validation with stable codes; an
    // injectable provider surface seam that wraps the engine's by-name/optional
    // resolutions; and a plain runtime built from explicit references that applies an
    // accepted comfort policy to the bound providers, forwards accepted intents in
    // order, and reports every by-name or optional resolution failure (LESSON-004). No
    // singleton, no static instance, no scene search, no reflection discovery, no claim
    // of rig motion, vignette rendering, or device input.
    //
    // The concrete XR Interaction Toolkit types and member names below reflect the
    // toolkit's 3.x locomotion API shape. This file is authored and unexecuted: no
    // Unity run has confirmed them against a pinned toolkit version yet, so the exact
    // members must be reconfirmed against the compatibility profile recorded by the
    // first `run_unity_gates.py` receipt before any adapter claim is trusted
    // (see LESSON-004 and the version-adaptive reference model).

    [Serializable]
    public sealed class ProviderBindingEntry
    {
        [SerializeField] private string modeId = string.Empty;
        [SerializeField] private LocomotionProvider provider;
        [SerializeField] private XRBodyTransformer bodyTransformer;
        [SerializeField] private InputActionReference activateAction;

        public string ModeId => modeId ?? string.Empty;
        /// <summary>The real provider component; null in every EditMode test because no provider can be constructed without a rig.</summary>
        public LocomotionProvider Provider => provider;
        /// <summary>Optional explicit body transformer; when null the real surface performs the toolkit's optional parent lookup.</summary>
        public XRBodyTransformer BodyTransformer => bodyTransformer;
        /// <summary>Optional input action reference; a reference that resolves to no action is reported, not ignored (LESSON-004).</summary>
        public InputActionReference ActivateAction => activateAction;
    }

    /// <summary>
    /// The engine calls the adapter needs, behind a seam. The real implementation wraps
    /// XR Interaction Toolkit providers and the vignette controller; a test substitutes a
    /// fake because no provider can be constructed in EditMode without a rig.
    /// </summary>
    public interface ILocomotionProviderSurface
    {
        /// <summary>True when a provider component is bound for the mode.</summary>
        bool HasProvider(ModeId mode);
        /// <summary>Meaningful only when <see cref="HasProvider"/> is true: false when the bound component is the wrong concrete type for the mode.</summary>
        bool ProviderMatchesMode(ModeId mode);
        /// <summary>True when a vignette controller reference is bound.</summary>
        bool HasVignette { get; }
        /// <summary>True when the mode's body transformer resolves, by an explicit reference or the toolkit's optional lookup.</summary>
        bool HasBodyTransformer(ModeId mode);
        /// <summary>True when no input action reference was declared, or the declared one resolves to a real action.</summary>
        bool HasResolvedInputAction(ModeId mode);

        bool TrySetTurnEnabled(ModeId mode, bool enabled);
        bool TrySetTurnIncrementDegrees(ModeId mode, float degrees);
        bool TrySetMoveSpeed(float metersPerSecond);
        bool TrySetVignetteEnabled(bool enabled);
        bool TrySetVignetteIntensity(float intensity);
        /// <summary>Forwards a teleport request for the given anchor id; the Core decided it is valid, this call never claims the rig moved.</summary>
        bool TryRequestTeleport(string anchorId);
        bool TryApplyTurn(ModeId mode, float signedDegrees);
        bool TryApplyMove(float x, float z);
    }

    /// <summary>The real surface over a validated <see cref="LocomotionProviderBindingAsset"/>. Every call is by name or optional lookup and reports failure through its return value.</summary>
    public sealed class LocomotionProviderSurface : ILocomotionProviderSurface
    {
        private readonly LocomotionProviderBindingAsset _asset;

        public LocomotionProviderSurface(LocomotionProviderBindingAsset asset)
        {
            _asset = asset;
        }

        public bool HasVignette => _asset != null && _asset.Vignette != null;

        public bool HasProvider(ModeId mode) => TryFindEntry(mode, out var entry) && entry.Provider != null;

        public bool ProviderMatchesMode(ModeId mode)
        {
            if (!TryFindEntry(mode, out var entry) || entry.Provider == null) return false;
            var expected = ExpectedType(mode);
            return expected != null && expected.IsInstanceOfType(entry.Provider);
        }

        public bool HasBodyTransformer(ModeId mode)
        {
            if (!TryFindEntry(mode, out var entry)) return false;
            if (entry.BodyTransformer != null) return true;
            // Optional lookup: the toolkit resolves an unassigned body transformer from a
            // parent in the rig hierarchy when the provider itself is present.
            return entry.Provider != null && entry.Provider.GetComponentInParent<XRBodyTransformer>() != null;
        }

        public bool HasResolvedInputAction(ModeId mode)
        {
            if (!TryFindEntry(mode, out var entry)) return true;
            return entry.ActivateAction == null || entry.ActivateAction.action != null;
        }

        public bool TrySetTurnEnabled(ModeId mode, bool enabled)
        {
            if (!TryFindEntry(mode, out var entry) || entry.Provider == null) return false;
            entry.Provider.enabled = enabled;
            return true;
        }

        public bool TrySetTurnIncrementDegrees(ModeId mode, float degrees)
        {
            if (!TryFindEntry(mode, out var entry) || entry.Provider == null) return false;
            switch (entry.Provider)
            {
                case SnapTurnProvider snap:
                    snap.turnAmount = degrees;
                    return true;
                case ContinuousTurnProvider continuous:
                    continuous.turnSpeed = degrees;
                    return true;
                default:
                    return false;
            }
        }

        public bool TrySetMoveSpeed(float metersPerSecond)
        {
            if (!TryFindEntry(ModeId.ContinuousMove, out var entry) || !(entry.Provider is ContinuousMoveProvider move)) return false;
            move.moveSpeed = metersPerSecond;
            return true;
        }

        public bool TrySetVignetteEnabled(bool enabled)
        {
            if (_asset == null || _asset.Vignette == null) return false;
            _asset.Vignette.enabled = enabled;
            return true;
        }

        public bool TrySetVignetteIntensity(float intensity)
        {
            if (_asset == null || _asset.Vignette == null) return false;
            var parameters = _asset.Vignette.defaultParameters;
            parameters.apertureSize = 1f - Mathf.Clamp01(intensity);
            _asset.Vignette.defaultParameters = parameters;
            return true;
        }

        public bool TryRequestTeleport(string anchorId)
        {
            // Anchor-id to world pose resolution is a consuming scene's concern, outside
            // the Core and this adapter; this call only confirms a teleport provider is
            // bound and reachable. No test built on this surface claims the rig moved.
            return TryFindEntry(ModeId.Teleport, out var entry) && entry.Provider != null;
        }

        public bool TryApplyTurn(ModeId mode, float signedDegrees) => TryFindEntry(mode, out var entry) && entry.Provider != null;

        public bool TryApplyMove(float x, float z) => TryFindEntry(ModeId.ContinuousMove, out var entry) && entry.Provider != null;

        private bool TryFindEntry(ModeId mode, out ProviderBindingEntry found)
        {
            if (_asset != null)
            {
                foreach (var entry in _asset.Providers)
                {
                    if (entry == null) continue;
                    var candidate = ModeId.TryCreate(entry.ModeId);
                    if (candidate.Succeeded && candidate.Value == mode)
                    {
                        found = entry;
                        return true;
                    }
                }
            }
            found = null;
            return false;
        }

        private static Type ExpectedType(ModeId mode)
        {
            if (mode == ModeId.Teleport) return typeof(TeleportationProvider);
            if (mode == ModeId.SnapTurn) return typeof(SnapTurnProvider);
            if (mode == ModeId.SmoothTurn) return typeof(ContinuousTurnProvider);
            if (mode == ModeId.ContinuousMove) return typeof(ContinuousMoveProvider);
            return null;
        }
    }

    [CreateAssetMenu(menuName = "Lingkyn/Locomotion/Provider Binding", fileName = "LocomotionProviderBinding")]
    public sealed class LocomotionProviderBindingAsset : ScriptableObject
    {
        [SerializeField] private List<ProviderBindingEntry> providers = new List<ProviderBindingEntry>();
        [SerializeField] private TunnelingVignetteController vignette;

        public IReadOnlyList<ProviderBindingEntry> Providers => providers;
        public TunnelingVignetteController Vignette => vignette;

        /// <summary>The real surface over this asset's references; present entries are those with a non-null provider.</summary>
        public ILocomotionProviderSurface CreateSurface() => new LocomotionProviderSurface(this);

        /// <summary>Converts the asset without mutating it; throws with the validation report when invalid.</summary>
        public LocomotionProviderBinding ToBinding(ComfortPolicy policy) => LocomotionProviderBinding.Create(this, policy, CreateSurface());

        /// <summary>Converts through an explicit surface, for tests and for hosts that own the providers elsewhere.</summary>
        public LocomotionProviderBinding ToBinding(ComfortPolicy policy, ILocomotionProviderSurface surface) => LocomotionProviderBinding.Create(this, policy, surface);
    }

    public sealed class BindingDiagnostic
    {
        public BindingDiagnostic(string code, UnityEngine.Object source, string fieldPath, string message)
        {
            Code = code;
            Source = source;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>Stable code: provider.missing, provider.mode.mismatch, vignette.missing, binding.duplicate, binding.asset.missing, binding.entry.missing, provider.body_transformer.missing, provider.input_action.unresolved, identity.malformed.</summary>
        public string Code { get; }
        public UnityEngine.Object Source { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    public sealed class BindingReport
    {
        public BindingReport(IReadOnlyList<BindingDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }

        public IReadOnlyList<BindingDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;
    }

    public sealed class LocomotionBindingException : Exception
    {
        public LocomotionBindingException(BindingReport report)
            : base(Describe(report))
        {
            Report = report;
        }

        public BindingReport Report { get; }

        private static string Describe(BindingReport report)
        {
            var lines = new List<string>(report.Diagnostics.Count + 1) { "Locomotion provider binding is invalid:" };
            foreach (var diagnostic in report.Diagnostics)
            {
                lines.Add($"  [{diagnostic.Code}] {diagnostic.FieldPath}: {diagnostic.Message}");
            }
            return string.Join("\n", lines);
        }
    }

    public static class LocomotionBindingValidation
    {
        public const string ProviderMissing = "provider.missing";
        public const string ProviderModeMismatch = "provider.mode.mismatch";
        public const string VignetteMissing = "vignette.missing";
        public const string BindingDuplicate = "binding.duplicate";
        public const string AssetMissing = "binding.asset.missing";
        public const string EntryMissing = "binding.entry.missing";
        public const string BodyTransformerMissing = "provider.body_transformer.missing";
        public const string InputActionUnresolved = "provider.input_action.unresolved";

        public static BindingReport Validate(LocomotionProviderBindingAsset asset, ComfortPolicy policy, ILocomotionProviderSurface surface)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            var diagnostics = new List<BindingDiagnostic>();
            if (asset == null)
            {
                diagnostics.Add(new BindingDiagnostic(AssetMissing, null, string.Empty, "The binding asset is null."));
                return new BindingReport(diagnostics);
            }

            var seenModes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < asset.Providers.Count; index++)
            {
                var entry = asset.Providers[index];
                var path = $"providers.Array.data[{index}]";
                if (entry == null)
                {
                    diagnostics.Add(new BindingDiagnostic(EntryMissing, asset, path, "A provider binding entry is empty."));
                    continue;
                }
                var mode = ModeId.TryCreate(entry.ModeId);
                if (!mode.Succeeded)
                {
                    diagnostics.Add(new BindingDiagnostic(mode.Code, asset, path + ".modeId", mode.Message));
                    continue;
                }
                if (seenModes.TryGetValue(mode.Value.Value, out var first))
                {
                    diagnostics.Add(new BindingDiagnostic(BindingDuplicate, asset, path + ".modeId", $"Mode '{mode.Value}' is already bound by providers.Array.data[{first}]."));
                    continue;
                }
                seenModes[mode.Value.Value] = index;

                if (!surface.HasProvider(mode.Value))
                {
                    diagnostics.Add(new BindingDiagnostic(ProviderMissing, asset, path + ".provider", $"No provider is bound for mode '{mode.Value}'."));
                    continue;
                }
                if (!surface.ProviderMatchesMode(mode.Value))
                {
                    diagnostics.Add(new BindingDiagnostic(ProviderModeMismatch, asset, path + ".provider", $"The provider bound to '{mode.Value}' is not the matching XR Interaction Toolkit provider type."));
                    continue;
                }
                if ((mode.Value == ModeId.SmoothTurn || mode.Value == ModeId.ContinuousMove) && !surface.HasBodyTransformer(mode.Value))
                {
                    diagnostics.Add(new BindingDiagnostic(BodyTransformerMissing, asset, path + ".bodyTransformer", $"Mode '{mode.Value}' resolves no body transformer, by reference or by the optional lookup."));
                }
                if (!surface.HasResolvedInputAction(mode.Value))
                {
                    diagnostics.Add(new BindingDiagnostic(InputActionUnresolved, asset, path + ".activateAction", $"Mode '{mode.Value}' declares an input action reference that resolves to no action."));
                }
            }

            if (policy.VignetteEnabled && !surface.HasVignette)
            {
                diagnostics.Add(new BindingDiagnostic(VignetteMissing, asset, "vignette", "The comfort policy enables the vignette but no vignette controller is bound."));
            }

            return new BindingReport(diagnostics);
        }
    }

    /// <summary>An immutable, validated binding between a mode set and a provider surface.</summary>
    public sealed class LocomotionProviderBinding
    {
        private LocomotionProviderBinding(ILocomotionProviderSurface surface, IReadOnlyList<ModeId> boundModes)
        {
            Surface = surface;
            BoundModes = boundModes;
        }

        public ILocomotionProviderSurface Surface { get; }
        public IReadOnlyList<ModeId> BoundModes { get; }

        public static LocomotionProviderBinding Create(LocomotionProviderBindingAsset asset, ComfortPolicy policy, ILocomotionProviderSurface surface)
        {
            var report = LocomotionBindingValidation.Validate(asset, policy, surface);
            if (!report.IsValid) throw new LocomotionBindingException(report);
            var modes = new List<ModeId>();
            foreach (var entry in asset.Providers)
            {
                var mode = ModeId.TryCreate(entry.ModeId);
                if (mode.Succeeded && !modes.Contains(mode.Value)) modes.Add(mode.Value);
            }
            return new LocomotionProviderBinding(surface, modes);
        }
    }

    /// <summary>An explicit report of a by-name or optional provider resolution that did not succeed at runtime (LESSON-004).</summary>
    public sealed class ProviderDiagnostic
    {
        public ProviderDiagnostic(string code, LocomotionIntent intent, string message)
        {
            Code = code;
            Intent = intent;
            Message = message ?? string.Empty;
        }

        /// <summary>Stable code: provider.missing, provider.turn.write_failed, provider.move.write_failed, provider.vignette.write_failed, provider.teleport.request_failed.</summary>
        public string Code { get; }
        public LocomotionIntent Intent { get; }
        public string Message { get; }
    }

    public static class ProviderDiagnosticCodes
    {
        public const string ProviderMissing = "provider.missing";
        public const string TurnWriteFailed = "provider.turn.write_failed";
        public const string MoveWriteFailed = "provider.move.write_failed";
        public const string VignetteWriteFailed = "provider.vignette.write_failed";
        public const string TeleportRequestFailed = "provider.teleport.request_failed";
    }

    /// <summary>
    /// Owns one <see cref="LocomotionState"/> over one binding. The Core decides whether
    /// an intent is accepted; this runtime then applies an accepted comfort policy to the
    /// bound providers and forwards accepted teleport/turn/move intents in the order they
    /// were accepted. Every by-name or optional resolution that fails becomes a
    /// <see cref="ProviderDiagnostic"/>; the runtime never reports a provider write it did
    /// not confirm. Plain class: a consumer's composition root decides its lifetime, and
    /// two runtimes share nothing (no singleton, no static instance, no scene search, no
    /// reflection discovery).
    /// </summary>
    public sealed class LocomotionProviderRuntime
    {
        private readonly List<LocomotionIntentOutcome> _outcomes = new List<LocomotionIntentOutcome>();
        private readonly List<ProviderDiagnostic> _diagnostics = new List<ProviderDiagnostic>();

        public LocomotionProviderRuntime(LocomotionProviderBinding binding, LocomotionState initialState)
        {
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
            ApplyPolicyToProviders(State.Policy, null);
        }

        // previousPolicy is accepted (but not yet compared) so a future revision can push
        // only the options that changed instead of the whole policy on every acceptance.

        public LocomotionProviderBinding Binding { get; }
        public LocomotionState State { get; private set; }

        /// <summary>Every intent this runtime saw, accepted or rejected, in order.</summary>
        public IReadOnlyList<LocomotionIntentOutcome> Outcomes => _outcomes;
        public IReadOnlyList<ProviderDiagnostic> Diagnostics => _diagnostics;

        public int AcceptedCount
        {
            get
            {
                var count = 0;
                foreach (var outcome in _outcomes)
                {
                    if (outcome.Accepted) count++;
                }
                return count;
            }
        }

        public int RejectedCount => _outcomes.Count - AcceptedCount;

        public LocomotionIntentOutcome Apply(LocomotionIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            var index = _outcomes.Count;
            var previousPolicy = State.Policy;
            var result = State.Apply(intent);
            LocomotionIntentOutcome outcome;
            if (!result.Succeeded)
            {
                outcome = new LocomotionIntentOutcome(index, intent, false, result.Code, result.Message);
                _outcomes.Add(outcome);
                return outcome;
            }
            State = result.Value;
            outcome = new LocomotionIntentOutcome(index, intent, true, LocomotionFailure.None, string.Empty);
            _outcomes.Add(outcome);
            switch (intent)
            {
                case SetComfortOptionIntent _:
                    ApplyPolicyToProviders(State.Policy, previousPolicy, intent);
                    break;
                case TeleportIntent teleport:
                    ForwardTeleport(teleport);
                    break;
                case TurnIntent turn:
                    ForwardTurn(turn);
                    break;
                case MoveIntent move:
                    ForwardMove(move);
                    break;
            }
            return outcome;
        }

        /// <summary>Applies each intent in order and returns the outcomes of this call only.</summary>
        public LocomotionSequenceResult ApplyAll(IEnumerable<LocomotionIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var outcomes = new List<LocomotionIntentOutcome>();
            foreach (var intent in intents)
            {
                outcomes.Add(Apply(intent));
            }
            return new LocomotionSequenceResult(State, outcomes);
        }

        private void ApplyPolicyToProviders(ComfortPolicy policy, ComfortPolicy previousPolicy, LocomotionIntent intent = null)
        {
            var activeTurn = policy.TurnMode == TurnMode.Smooth ? ModeId.SmoothTurn : ModeId.SnapTurn;
            var inactiveTurn = activeTurn == ModeId.SnapTurn ? ModeId.SmoothTurn : ModeId.SnapTurn;

            if (!Binding.Surface.TrySetTurnEnabled(activeTurn, true))
            {
                _diagnostics.Add(new ProviderDiagnostic(ProviderDiagnosticCodes.ProviderMissing, intent, $"No provider is bound for the active turn mode '{activeTurn}'."));
            }
            Binding.Surface.TrySetTurnEnabled(inactiveTurn, false);

            if (!Binding.Surface.TrySetTurnIncrementDegrees(activeTurn, policy.TurnIncrementDegrees))
            {
                _diagnostics.Add(new ProviderDiagnostic(ProviderDiagnosticCodes.TurnWriteFailed, intent, $"The turn increment could not be written to the '{activeTurn}' provider."));
            }
            if (!Binding.Surface.TrySetMoveSpeed(policy.MovementSpeed))
            {
                _diagnostics.Add(new ProviderDiagnostic(ProviderDiagnosticCodes.MoveWriteFailed, intent, "The movement speed could not be written to the continuous move provider."));
            }
            if (!Binding.Surface.TrySetVignetteEnabled(policy.VignetteEnabled))
            {
                _diagnostics.Add(new ProviderDiagnostic(ProviderDiagnosticCodes.VignetteWriteFailed, intent, "The vignette enabled flag could not be written to the vignette controller."));
            }
            else if (policy.VignetteEnabled && !Binding.Surface.TrySetVignetteIntensity(policy.VignetteIntensity))
            {
                _diagnostics.Add(new ProviderDiagnostic(ProviderDiagnosticCodes.VignetteWriteFailed, intent, "The vignette intensity could not be written to the vignette controller."));
            }
        }

        private void ForwardTeleport(TeleportIntent intent)
        {
            if (!Binding.Surface.TryRequestTeleport(intent.Anchor.Value))
            {
                _diagnostics.Add(new ProviderDiagnostic(ProviderDiagnosticCodes.TeleportRequestFailed, intent, $"The teleportation provider did not accept a request to anchor '{intent.Anchor}'."));
            }
        }

        private void ForwardTurn(TurnIntent intent)
        {
            var signedDegrees = State.Policy.TurnIncrementDegrees * (intent.Direction == TurnDirection.Left ? -1f : 1f);
            if (!Binding.Surface.TryApplyTurn(intent.Mode, signedDegrees))
            {
                _diagnostics.Add(new ProviderDiagnostic(ProviderDiagnosticCodes.TurnWriteFailed, intent, $"The '{intent.Mode}' provider did not accept the turn."));
            }
        }

        private void ForwardMove(MoveIntent intent)
        {
            if (!Binding.Surface.TryApplyMove(intent.Vector.X, intent.Vector.Z))
            {
                _diagnostics.Add(new ProviderDiagnostic(ProviderDiagnosticCodes.MoveWriteFailed, intent, "The continuous move provider did not accept the move."));
            }
        }
    }
}
