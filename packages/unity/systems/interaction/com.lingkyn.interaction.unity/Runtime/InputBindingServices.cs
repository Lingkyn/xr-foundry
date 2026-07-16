using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lingkyn.Interaction.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lingkyn.Interaction.Unity
{
    public readonly struct InputBindingDisplayEntry : IEquatable<InputBindingDisplayEntry>
    {
        public InputBindingDisplayEntry(
            RouteId routeId,
            Guid actionId,
            Guid bindingId,
            int bindingIndex,
            string displayString,
            string deviceLayout,
            string controlPath)
        {
            RouteId = routeId;
            ActionId = actionId;
            BindingId = bindingId;
            BindingIndex = bindingIndex;
            DisplayString = displayString ?? string.Empty;
            DeviceLayout = deviceLayout ?? string.Empty;
            ControlPath = controlPath ?? string.Empty;
        }

        public RouteId RouteId { get; }
        public Guid ActionId { get; }
        public Guid BindingId { get; }
        public int BindingIndex { get; }
        public string DisplayString { get; }
        public string DeviceLayout { get; }
        public string ControlPath { get; }

        public bool Equals(InputBindingDisplayEntry other) =>
            RouteId.Equals(other.RouteId) && ActionId.Equals(other.ActionId)
            && BindingId.Equals(other.BindingId) && BindingIndex == other.BindingIndex
            && string.Equals(DisplayString, other.DisplayString, StringComparison.Ordinal)
            && string.Equals(DeviceLayout, other.DeviceLayout, StringComparison.Ordinal)
            && string.Equals(ControlPath, other.ControlPath, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InputBindingDisplayEntry other && Equals(other);
        public override int GetHashCode() =>
            HashCode.Combine(RouteId, ActionId, BindingId, BindingIndex, DisplayString, DeviceLayout, ControlPath);
    }

    public static class InputBindingDisplayService
    {
        public static InteractionResult<IReadOnlyList<InputBindingDisplayEntry>> GetEntries(
            InputRouteBinding binding,
            InputBinding.DisplayStringOptions options = InputBinding.DisplayStringOptions.DontUseShortDisplayNames)
        {
            if (binding == null || binding.ActionReference == null || binding.ActionReference.action == null)
            {
                return InteractionResult<IReadOnlyList<InputBindingDisplayEntry>>.Fail(
                    InteractionValidationCode.InvalidDefinition,
                    "A live explicit route binding is required.");
            }

            var action = binding.ActionReference.action;
            if (action.id != binding.ActionId)
            {
                return InteractionResult<IReadOnlyList<InputBindingDisplayEntry>>.Fail(
                    InteractionValidationCode.InvalidDefinition,
                    "InputActionReference identity changed after authoring conversion.",
                    binding.RouteId.Value);
            }

            var entries = new List<InputBindingDisplayEntry>(action.bindings.Count);
            for (var index = 0; index < action.bindings.Count; index++)
            {
                var display = action.GetBindingDisplayString(
                    index,
                    out var deviceLayout,
                    out var controlPath,
                    options);
                entries.Add(new InputBindingDisplayEntry(
                    binding.RouteId,
                    action.id,
                    action.bindings[index].id,
                    index,
                    display,
                    deviceLayout,
                    controlPath));
            }
            return InteractionResult<IReadOnlyList<InputBindingDisplayEntry>>.Success(entries.AsReadOnly());
        }
    }

    [Serializable]
    public sealed class InputBindingOverrideRecord : IEquatable<InputBindingOverrideRecord>
    {
        [SerializeField] private string _bindingId;
        [SerializeField] private string _overridePath;
        [SerializeField] private string _overrideProcessors;
        [SerializeField] private string _overrideInteractions;

        private InputBindingOverrideRecord()
        {
        }

        public InputBindingOverrideRecord(
            Guid bindingId,
            string overridePath,
            string overrideProcessors,
            string overrideInteractions)
        {
            if (bindingId == Guid.Empty)
                throw new ArgumentException("Binding GUID is required.", nameof(bindingId));
            _bindingId = bindingId.ToString("D");
            _overridePath = overridePath ?? string.Empty;
            _overrideProcessors = overrideProcessors ?? string.Empty;
            _overrideInteractions = overrideInteractions ?? string.Empty;
        }

        public Guid BindingId => Guid.TryParse(_bindingId, out var value) ? value : Guid.Empty;
        public string OverridePath => _overridePath ?? string.Empty;
        public string OverrideProcessors => _overrideProcessors ?? string.Empty;
        public string OverrideInteractions => _overrideInteractions ?? string.Empty;

        public bool Equals(InputBindingOverrideRecord other) => other != null
            && BindingId.Equals(other.BindingId)
            && string.Equals(OverridePath, other.OverridePath, StringComparison.Ordinal)
            && string.Equals(OverrideProcessors, other.OverrideProcessors, StringComparison.Ordinal)
            && string.Equals(OverrideInteractions, other.OverrideInteractions, StringComparison.Ordinal);
        public override bool Equals(object obj) => Equals(obj as InputBindingOverrideRecord);
        public override int GetHashCode() =>
            HashCode.Combine(BindingId, OverridePath, OverrideProcessors, OverrideInteractions);
    }

    public sealed class InputBindingOverrideSnapshot
    {
        private readonly InputBindingOverrideRecord[] _records;

        public InputBindingOverrideSnapshot(RouteId routeId, Guid actionId, IEnumerable<InputBindingOverrideRecord> records)
        {
            if (string.IsNullOrEmpty(routeId.Value))
                throw new ArgumentException("Route identity is required.", nameof(routeId));
            if (actionId == Guid.Empty)
                throw new ArgumentException("Action GUID is required.", nameof(actionId));
            RouteId = routeId;
            ActionId = actionId;
            _records = (records ?? Array.Empty<InputBindingOverrideRecord>())
                .OrderBy(record => record == null ? Guid.Empty : record.BindingId)
                .ToArray();
        }

        public RouteId RouteId { get; }
        public Guid ActionId { get; }
        public IReadOnlyList<InputBindingOverrideRecord> Records => Array.AsReadOnly(_records);
    }

    public static class InputBindingOverrideService
    {
        private const string AdapterKind = "unity-input-system/1.14";

        [Serializable]
        private sealed class SnapshotDto
        {
            public string routeId;
            public string actionId;
            public List<InputBindingOverrideRecord> records = new List<InputBindingOverrideRecord>();
        }

        public static InteractionResult<InputBindingOverrideSnapshot> Capture(InputRouteBinding binding)
        {
            if (!TryGetLiveAction(binding, out var action, out var error))
                return InteractionResult<InputBindingOverrideSnapshot>.Fail(error.Code, error.Message, error.Subject);
            var records = new List<InputBindingOverrideRecord>();
            for (var index = 0; index < action.bindings.Count; index++)
            {
                var item = action.bindings[index];
                if (!item.hasOverrides)
                    continue;
                records.Add(new InputBindingOverrideRecord(
                    item.id,
                    item.overridePath,
                    item.overrideProcessors,
                    item.overrideInteractions));
            }
            return InteractionResult<InputBindingOverrideSnapshot>.Success(
                new InputBindingOverrideSnapshot(binding.RouteId, action.id, records));
        }

        public static InteractionResult<string> Serialize(InputBindingOverrideSnapshot snapshot)
        {
            var validation = ValidateSnapshot(snapshot);
            if (!validation.Succeeded)
                return InteractionResult<string>.Fail(validation.Error.Code, validation.Error.Message, validation.Error.Subject);
            var dto = new SnapshotDto
            {
                routeId = snapshot.RouteId.Value,
                actionId = snapshot.ActionId.ToString("D"),
                records = snapshot.Records.ToList(),
            };
            return InteractionResult<string>.Success(JsonUtility.ToJson(dto));
        }

        public static InteractionResult<InputBindingOverrideSnapshot> Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return InteractionResult<InputBindingOverrideSnapshot>.Fail(InteractionValidationCode.InvalidDefinition, "Override JSON is required.");
            SnapshotDto dto;
            try
            {
                dto = JsonUtility.FromJson<SnapshotDto>(json);
            }
            catch (ArgumentException exception)
            {
                return InteractionResult<InputBindingOverrideSnapshot>.Fail(InteractionValidationCode.InvalidDefinition, exception.Message);
            }

            var routeId = RouteId.TryCreate(dto == null ? null : dto.routeId);
            if (!routeId.Succeeded || dto == null || !Guid.TryParse(dto.actionId, out var actionId) || actionId == Guid.Empty)
                return InteractionResult<InputBindingOverrideSnapshot>.Fail(InteractionValidationCode.InvalidDefinition, "Override JSON has an invalid route or action identity.");
            var snapshot = new InputBindingOverrideSnapshot(routeId.Value, actionId, dto.records);
            var validation = ValidateSnapshot(snapshot);
            return validation.Succeeded
                ? InteractionResult<InputBindingOverrideSnapshot>.Success(snapshot)
                : InteractionResult<InputBindingOverrideSnapshot>.Fail(validation.Error.Code, validation.Error.Message, validation.Error.Subject);
        }

        public static InteractionResult Apply(
            InputRouteBinding binding,
            InputBindingOverrideSnapshot snapshot,
            bool replaceExisting = true)
        {
            if (!TryGetLiveAction(binding, out var action, out var error))
                return InteractionResult.Fail(error.Code, error.Message, error.Subject);
            var validation = ValidateSnapshot(snapshot);
            if (!validation.Succeeded)
                return validation;
            if (!snapshot.RouteId.Equals(binding.RouteId) || snapshot.ActionId != action.id)
                return InteractionResult.Fail(InteractionValidationCode.InvalidDefinition, "Override snapshot does not match the route and action GUID.", binding.RouteId.Value);

            var indices = new List<int>(snapshot.Records.Count);
            foreach (var record in snapshot.Records)
            {
                var index = FindBindingIndex(action, record.BindingId);
                if (index < 0)
                    return InteractionResult.Fail(InteractionValidationCode.InvalidDefinition, "Override references an unknown binding GUID.", record.BindingId.ToString("D"));
                indices.Add(index);
            }

            if (replaceExisting)
                action.RemoveAllBindingOverrides();
            for (var index = 0; index < snapshot.Records.Count; index++)
            {
                var record = snapshot.Records[index];
                action.ApplyBindingOverride(indices[index], new InputBinding
                {
                    overridePath = record.OverridePath,
                    overrideProcessors = record.OverrideProcessors,
                    overrideInteractions = record.OverrideInteractions,
                });
            }
            return InteractionResult.Success();
        }

        public static InteractionResult<BindingOverride> ToCoreOverride(
            InputRouteBinding binding,
            InputBindingOverrideSnapshot snapshot)
        {
            var serialized = Serialize(snapshot);
            if (!serialized.Succeeded)
                return InteractionResult<BindingOverride>.Fail(serialized.Error.Code, serialized.Error.Message, serialized.Error.Subject);
            if (binding == null || !binding.RouteId.Equals(snapshot.RouteId) || binding.ActionId != snapshot.ActionId)
                return InteractionResult<BindingOverride>.Fail(InteractionValidationCode.InvalidDefinition, "Override snapshot does not match the route binding.");
            return BindingOverride.Create(
                binding.IntentId,
                binding.RouteId,
                AdapterKind,
                Encoding.UTF8.GetBytes(serialized.Value));
        }

        private static InteractionResult ValidateSnapshot(InputBindingOverrideSnapshot snapshot)
        {
            if (snapshot == null)
                return InteractionResult.Fail(InteractionValidationCode.InvalidDefinition, "Override snapshot is required.");
            var seen = new HashSet<Guid>();
            foreach (var record in snapshot.Records)
            {
                if (record == null || record.BindingId == Guid.Empty)
                    return InteractionResult.Fail(InteractionValidationCode.InvalidDefinition, "Every override record requires a binding GUID.", snapshot.RouteId.Value);
                if (!seen.Add(record.BindingId))
                    return InteractionResult.Fail(InteractionValidationCode.DuplicateDefinition, "Override binding GUIDs must be unique.", record.BindingId.ToString("D"));
            }
            return InteractionResult.Success();
        }

        private static bool TryGetLiveAction(InputRouteBinding binding, out InputAction action, out InteractionError error)
        {
            action = binding == null || binding.ActionReference == null ? null : binding.ActionReference.action;
            if (action == null || action.id == Guid.Empty || action.id != binding.ActionId)
            {
                error = new InteractionError(InteractionValidationCode.InvalidDefinition,
                    "A live InputActionReference with the converted action GUID is required.",
                    binding == null ? string.Empty : binding.RouteId.Value);
                return false;
            }
            error = default;
            return true;
        }

        private static int FindBindingIndex(InputAction action, Guid bindingId)
        {
            for (var index = 0; index < action.bindings.Count; index++)
            {
                if (action.bindings[index].id == bindingId)
                    return index;
            }
            return -1;
        }
    }
}
