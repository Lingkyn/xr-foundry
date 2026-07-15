using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lingkyn.Inventory.Core;
using UnityEngine;

namespace Lingkyn.Inventory.Unity
{
    public enum InventoryAuthoringSeverity
    {
        Error,
        Warning,
    }

    public sealed class InventoryAuthoringDiagnostic
    {
        internal InventoryAuthoringDiagnostic(
            InventoryAuthoringSeverity severity,
            string code,
            UnityEngine.Object source,
            string fieldPath,
            string message)
        {
            Severity = severity;
            Code = code;
            Source = source;
            FieldPath = fieldPath;
            Message = message;
        }

        public InventoryAuthoringSeverity Severity { get; }
        public string Code { get; }
        public UnityEngine.Object Source { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    public sealed class InventoryAuthoringReport
    {
        internal InventoryAuthoringReport(IEnumerable<InventoryAuthoringDiagnostic> diagnostics)
        {
            Diagnostics = new ReadOnlyCollection<InventoryAuthoringDiagnostic>(diagnostics.ToArray());
        }

        public IReadOnlyList<InventoryAuthoringDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.All(item => item.Severity != InventoryAuthoringSeverity.Error);
    }

    public sealed class InventoryAuthoringException : InvalidOperationException
    {
        internal InventoryAuthoringException(InventoryAuthoringReport report)
            : base(string.Join("; ", report.Diagnostics.Select(item => $"{item.Code} [{item.FieldPath}]: {item.Message}")))
        {
            Report = report;
        }

        public InventoryAuthoringReport Report { get; }
    }

    public static class InventoryAuthoringValidation
    {
        public static InventoryAuthoringReport Validate(ItemDefinitionAsset asset)
        {
            var diagnostics = new List<InventoryAuthoringDiagnostic>();
            if (asset == null)
            {
                diagnostics.Add(Error("item.null", null, "item", "Item definition asset is required."));
                return new InventoryAuthoringReport(diagnostics);
            }

            ValidateId(asset, asset.StableId, "stableId", "item.id.empty", diagnostics);
            if (asset.MaximumStack < 1)
            {
                diagnostics.Add(Error("item.maximumStack.invalid", asset, "maximumStack", "Maximum stack must be at least one."));
            }

            if (asset.InstanceMode == ItemInstanceMode.Unique && asset.MaximumStack != 1)
            {
                diagnostics.Add(Error("item.unique.stack", asset, "maximumStack", "A unique item must have a maximum stack of one."));
            }

            return new InventoryAuthoringReport(diagnostics);
        }

        public static InventoryAuthoringReport Validate(ItemCatalogAsset catalog)
        {
            var diagnostics = new List<InventoryAuthoringDiagnostic>();
            if (catalog == null)
            {
                diagnostics.Add(Error("catalog.null", null, "catalog", "Item catalog asset is required."));
                return new InventoryAuthoringReport(diagnostics);
            }

            var seen = new Dictionary<string, ItemDefinitionAsset>(StringComparer.Ordinal);
            for (var index = 0; index < catalog.Items.Count; index++)
            {
                var item = catalog.Items[index];
                if (item == null)
                {
                    diagnostics.Add(Error("catalog.item.missing", catalog, $"items.Array.data[{index}]", "Assign an item definition asset."));
                    continue;
                }

                diagnostics.AddRange(Validate(item).Diagnostics);
                if (!string.IsNullOrWhiteSpace(item.StableId))
                {
                    if (seen.TryGetValue(item.StableId, out var first))
                    {
                        diagnostics.Add(Error(
                            "catalog.item.duplicateId",
                            item,
                            "stableId",
                            $"Stable ID '{item.StableId}' is already used by '{first.name}'."));
                    }
                    else
                    {
                        seen.Add(item.StableId, item);
                    }
                }
            }

            return new InventoryAuthoringReport(diagnostics);
        }

        public static InventoryAuthoringReport Validate(ContainerDefinitionAsset asset)
        {
            var diagnostics = new List<InventoryAuthoringDiagnostic>();
            if (asset == null)
            {
                diagnostics.Add(Error("container.null", null, "container", "Container definition asset is required."));
                return new InventoryAuthoringReport(diagnostics);
            }

            ValidateId(asset, asset.StableId, "stableId", "container.id.empty", diagnostics);
            if (asset.Capacity < 1)
            {
                diagnostics.Add(Error("container.capacity.invalid", asset, "capacity", "Capacity must be at least one."));
            }

            return new InventoryAuthoringReport(diagnostics);
        }

        public static InventoryAuthoringReport Validate(InventoryDefinitionAsset asset)
        {
            var diagnostics = new List<InventoryAuthoringDiagnostic>();
            if (asset == null)
            {
                diagnostics.Add(Error("inventory.null", null, "inventory", "Inventory definition asset is required."));
                return new InventoryAuthoringReport(diagnostics);
            }

            ValidateId(asset, asset.StableId, "stableId", "inventory.id.empty", diagnostics);
            var seen = new Dictionary<string, ContainerDefinitionAsset>(StringComparer.Ordinal);
            for (var index = 0; index < asset.Containers.Count; index++)
            {
                var container = asset.Containers[index];
                if (container == null)
                {
                    diagnostics.Add(Error("inventory.container.missing", asset, $"containers.Array.data[{index}]", "Assign a container definition asset."));
                    continue;
                }

                diagnostics.AddRange(Validate(container).Diagnostics);
                if (!string.IsNullOrWhiteSpace(container.StableId))
                {
                    if (seen.TryGetValue(container.StableId, out var first))
                    {
                        diagnostics.Add(Error(
                            "inventory.container.duplicateId",
                            container,
                            "stableId",
                            $"Stable ID '{container.StableId}' is already used by '{first.name}'."));
                    }
                    else
                    {
                        seen.Add(container.StableId, container);
                    }
                }
            }

            if (asset.Containers.Count == 0)
            {
                diagnostics.Add(Error("inventory.containers.empty", asset, "containers", "Assign at least one container."));
            }

            return new InventoryAuthoringReport(diagnostics);
        }

        public static void ThrowIfInvalid(ItemDefinitionAsset asset) => Throw(Validate(asset));
        public static void ThrowIfInvalid(ItemCatalogAsset asset) => Throw(Validate(asset));
        public static void ThrowIfInvalid(ContainerDefinitionAsset asset) => Throw(Validate(asset));
        public static void ThrowIfInvalid(InventoryDefinitionAsset asset) => Throw(Validate(asset));

        private static void Throw(InventoryAuthoringReport report)
        {
            if (!report.IsValid)
            {
                throw new InventoryAuthoringException(report);
            }
        }

        private static void ValidateId(
            UnityEngine.Object source,
            string value,
            string fieldPath,
            string code,
            ICollection<InventoryAuthoringDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                diagnostics.Add(Error(code, source, fieldPath, "Enter an explicit stable persistence ID."));
            }
        }

        private static InventoryAuthoringDiagnostic Error(
            string code,
            UnityEngine.Object source,
            string fieldPath,
            string message) =>
            new InventoryAuthoringDiagnostic(InventoryAuthoringSeverity.Error, code, source, fieldPath, message);
    }
}
