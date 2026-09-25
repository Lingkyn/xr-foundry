using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Lingkyn.QualityTiers.Core
{
    // Immutable quality state over one tier registry and one device capability set: at most one
    // selected tier and one set of field overrides per device profile (QC-07). Every accepted
    // intent returns a new state; the prior state stays intact and readable (QC-08). The same
    // intent sequence over the same registry, capability set, and initial state always produces an
    // equal final state and an equal fingerprint (QC-09).

    /// <summary>One device profile's active selection: the selected tier id and any field
    /// overrides, keyed by the closed <see cref="QualityOverrideField"/> set in canonical field
    /// order.</summary>
    public readonly struct DeviceSelection
    {
        public DeviceSelection(TierId tierId, SortedDictionary<QualityOverrideField, float> overrides)
        {
            TierId = tierId;
            Overrides = overrides;
        }

        public TierId TierId { get; }
        public IReadOnlyDictionary<QualityOverrideField, float> Overrides { get; }
    }

    /// <summary>
    /// Immutable quality state: at most one selected tier and one set of overrides per device
    /// profile. Every accepted intent returns a new state; the prior state stays intact and
    /// readable. The same intent sequence over the same registry, capability set, and initial
    /// state always produces an equal final state and an equal fingerprint.
    /// </summary>
    public sealed class QualityState : IEquatable<QualityState>
    {
        private readonly SortedDictionary<DeviceProfileId, DeviceSelection> _selections;

        private QualityState(
            QualityTierRegistry registry,
            DeviceCapabilitySet capabilities,
            SortedDictionary<DeviceProfileId, DeviceSelection> selections,
            long revision)
        {
            Registry = registry;
            Capabilities = capabilities;
            _selections = selections;
            Revision = revision;
        }

        public QualityTierRegistry Registry { get; }
        public DeviceCapabilitySet Capabilities { get; }

        /// <summary>Monotonically increasing: 0 for <see cref="Initial"/>, and one higher on every
        /// accepted intent, whether or not the net selection actually changed. Not part of
        /// <see cref="Fingerprint"/> (LESSON-011): two states with the same net per-device
        /// selections still have equal fingerprints even when they reached different revisions.</summary>
        public long Revision { get; }

        public static QualityState Initial(QualityTierRegistry registry, DeviceCapabilitySet capabilities)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));
            return new QualityState(registry, capabilities, new SortedDictionary<DeviceProfileId, DeviceSelection>(), 0);
        }

        public bool TryGetSelection(DeviceProfileId deviceProfileId, out DeviceSelection selection) => _selections.TryGetValue(deviceProfileId, out selection);

        /// <summary>Every device profile with an active selection, in canonical device-profile
        /// order.</summary>
        public IReadOnlyList<DeviceProfileId> SelectedDevices => new List<DeviceProfileId>(_selections.Keys);

        /// <summary>Applies one intent. Checked in exactly two steps, in this order, for every
        /// intent regardless of <see cref="QualityIntent.Actor"/> (LESSON-011, QC-14): first, a
        /// non-null <see cref="QualityIntent.ExpectedRevision"/> that does not match
        /// <see cref="Revision"/> is rejected with <see cref="QualityFailure.StateStale"/> and
        /// changes nothing — the intent's own rule never runs (QC-13); second, the intent's own
        /// rule applies.</summary>
        public QualityResult<QualityState> Apply(QualityIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (intent.ExpectedRevision.HasValue && intent.ExpectedRevision.Value != Revision)
            {
                return QualityResult<QualityState>.Fail(
                    QualityFailure.StateStale,
                    $"Intent expected revision {intent.ExpectedRevision.Value} but the state is at revision {Revision}; nothing changed.");
            }
            return intent.ApplyTo(this);
        }

        /// <summary>Applies every intent in order. A rejected intent is recorded and the state it
        /// found is kept for the next intent in the sequence. The outcomes are the replay log
        /// (QC-15): each carries its intent's actor and the state's revision immediately after it
        /// settled.</summary>
        public QualitySequenceResult ApplyAll(IEnumerable<QualityIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException(nameof(intents));
            var state = this;
            var outcomes = new List<QualityIntentOutcome>();
            var index = 0;
            foreach (var intent in intents)
            {
                var result = state.Apply(intent);
                if (result.Succeeded)
                {
                    state = result.Value;
                    outcomes.Add(new QualityIntentOutcome(index, intent, true, result.Code, result.Message, state.Revision));
                }
                else
                {
                    outcomes.Add(new QualityIntentOutcome(index, intent, false, result.Code, result.Message, state.Revision));
                }
                index++;
            }
            return new QualitySequenceResult(state, outcomes);
        }

        internal QualityResult<QualityState> ApplySelectTier(DeviceProfileId deviceProfileId, TierId tierId)
        {
            if (!Registry.TryGet(tierId, out var tier))
            {
                return QualityResult<QualityState>.Fail(QualityFailure.TierUnknown, $"Tier '{tierId}' is not registered.");
            }
            if (!Capabilities.TryGet(deviceProfileId, out var capability))
            {
                return QualityResult<QualityState>.Fail(QualityFailure.DeviceUnknown, $"Device profile '{deviceProfileId}' has no registered capability descriptor.");
            }
            if (!capability.SupportsTier(tier))
            {
                return QualityResult<QualityState>.Fail(QualityFailure.CapabilityUnsupported, $"Device '{deviceProfileId}' does not support tier '{tierId}' (refresh rate, render scale, foveation level, or MSAA).");
            }
            var selections = new SortedDictionary<DeviceProfileId, DeviceSelection>(_selections)
            {
                [deviceProfileId] = new DeviceSelection(tierId, new SortedDictionary<QualityOverrideField, float>())
            };
            return QualityResult<QualityState>.Ok(new QualityState(Registry, Capabilities, selections, Revision + 1));
        }

        internal QualityResult<QualityState> ApplySetOverride(DeviceProfileId deviceProfileId, string rawFieldName, float value)
        {
            if (!QualityOverrideFieldNames.TryParse(rawFieldName, out var field))
            {
                return QualityResult<QualityState>.Fail(QualityFailure.ValueOutOfRange, $"'{rawFieldName}' is not one of the closed override fields (refresh_rate, render_scale, foveation_level, msaa, shadow_budget, post_processing_budget).");
            }
            if (!_selections.TryGetValue(deviceProfileId, out var selection))
            {
                return QualityResult<QualityState>.Fail(QualityFailure.TierUnknown, $"Device '{deviceProfileId}' has no tier selected to override.");
            }
            if (!Registry.TryGet(selection.TierId, out var tier))
            {
                return QualityResult<QualityState>.Fail(QualityFailure.TierUnknown, $"Device '{deviceProfileId}' selected tier '{selection.TierId}' is no longer registered.");
            }
            if (!tier.GuardRailContains(field, value))
            {
                return QualityResult<QualityState>.Fail(QualityFailure.ValueOutOfRange, $"Value {value} for field '{rawFieldName}' lies outside tier '{tier.Id}''s own guard rail.");
            }
            if (QualityOverrideFieldNames.IsCapabilityGated(field))
            {
                if (!Capabilities.TryGet(deviceProfileId, out var capability))
                {
                    return QualityResult<QualityState>.Fail(QualityFailure.DeviceUnknown, $"Device profile '{deviceProfileId}' has no registered capability descriptor.");
                }
                if (!capability.SupportsOverrideValue(field, value))
                {
                    return QualityResult<QualityState>.Fail(QualityFailure.CapabilityUnsupported, $"Device '{deviceProfileId}' does not support value {value} for field '{rawFieldName}'.");
                }
            }
            var overrides = new SortedDictionary<QualityOverrideField, float>();
            foreach (var existing in selection.Overrides)
            {
                overrides[existing.Key] = existing.Value;
            }
            overrides[field] = value;
            var selections = new SortedDictionary<DeviceProfileId, DeviceSelection>(_selections)
            {
                [deviceProfileId] = new DeviceSelection(selection.TierId, overrides)
            };
            return QualityResult<QualityState>.Ok(new QualityState(Registry, Capabilities, selections, Revision + 1));
        }

        internal QualityResult<QualityState> ApplyReset(DeviceProfileId deviceProfileId)
        {
            if (!_selections.TryGetValue(deviceProfileId, out var selection))
            {
                // Nothing to reset: a no-op success naming no tier as selected.
                return QualityResult<QualityState>.Ok(new QualityState(Registry, Capabilities, new SortedDictionary<DeviceProfileId, DeviceSelection>(_selections), Revision + 1));
            }
            if (selection.Overrides.Count == 0)
            {
                // No override was present: a no-op success.
                return QualityResult<QualityState>.Ok(new QualityState(Registry, Capabilities, new SortedDictionary<DeviceProfileId, DeviceSelection>(_selections), Revision + 1));
            }
            var selections = new SortedDictionary<DeviceProfileId, DeviceSelection>(_selections)
            {
                [deviceProfileId] = new DeviceSelection(selection.TierId, new SortedDictionary<QualityOverrideField, float>())
            };
            return QualityResult<QualityState>.Ok(new QualityState(Registry, Capabilities, selections, Revision + 1));
        }

        /// <summary>A canonical text of the whole state: the tier-registry fingerprint, the
        /// capability-descriptor-set fingerprint, and the per-device-profile selected-tier-and-
        /// override entries in canonical device-profile order. <see cref="Revision"/> is excluded
        /// (LESSON-011): two states reached by different intent sequences with the same net
        /// per-device selections have equal fingerprints.</summary>
        public string Fingerprint()
        {
            var builder = new StringBuilder();
            builder.Append("registry[").Append(Registry.Fingerprint()).Append(']');
            builder.Append(" capabilities[").Append(Capabilities.Fingerprint()).Append(']');
            builder.Append(" selections[");
            foreach (var pair in _selections)
            {
                builder.Append(pair.Key).Append('=').Append(pair.Value.TierId).Append('{');
                foreach (var over in pair.Value.Overrides)
                {
                    builder.Append(QualityOverrideFieldNames.ToRawName(over.Key)).Append(':').Append(over.Value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                }
                builder.Append('}').Append(';');
            }
            builder.Append(']');
            return builder.ToString();
        }

        public bool Equals(QualityState other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Fingerprint(), other.Fingerprint(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is QualityState other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Fingerprint());
        public override string ToString() => Fingerprint();
    }
}
