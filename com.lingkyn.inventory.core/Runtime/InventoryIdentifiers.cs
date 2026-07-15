using System;

namespace Lingkyn.Inventory.Core
{
    internal static class IdentifierGuard
    {
        public static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An identifier cannot be empty or whitespace.", parameterName);
            }

            return value;
        }
    }

    public readonly struct ItemDefinitionId : IEquatable<ItemDefinitionId>
    {
        public ItemDefinitionId(string value) => Value = IdentifierGuard.Require(value, nameof(value));
        public string Value { get; }
        public bool Equals(ItemDefinitionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ItemDefinitionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ItemDefinitionId left, ItemDefinitionId right) => left.Equals(right);
        public static bool operator !=(ItemDefinitionId left, ItemDefinitionId right) => !left.Equals(right);
    }

    public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>
    {
        public ItemInstanceId(string value) => Value = IdentifierGuard.Require(value, nameof(value));
        public string Value { get; }
        public bool Equals(ItemInstanceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ItemInstanceId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ItemInstanceId left, ItemInstanceId right) => left.Equals(right);
        public static bool operator !=(ItemInstanceId left, ItemInstanceId right) => !left.Equals(right);
    }

    public readonly struct InventoryId : IEquatable<InventoryId>
    {
        public InventoryId(string value) => Value = IdentifierGuard.Require(value, nameof(value));
        public string Value { get; }
        public bool Equals(InventoryId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InventoryId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(InventoryId left, InventoryId right) => left.Equals(right);
        public static bool operator !=(InventoryId left, InventoryId right) => !left.Equals(right);
    }

    public readonly struct ContainerId : IEquatable<ContainerId>
    {
        public ContainerId(string value) => Value = IdentifierGuard.Require(value, nameof(value));
        public string Value { get; }
        public bool Equals(ContainerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ContainerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ContainerId left, ContainerId right) => left.Equals(right);
        public static bool operator !=(ContainerId left, ContainerId right) => !left.Equals(right);
    }

    public readonly struct SlotAddress : IEquatable<SlotAddress>
    {
        public SlotAddress(ContainerId containerId, int index)
        {
            IdentifierGuard.Require(containerId.Value, nameof(containerId));
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "A slot index cannot be negative.");
            }

            ContainerId = containerId;
            Index = index;
        }

        public ContainerId ContainerId { get; }
        public int Index { get; }
        public bool Equals(SlotAddress other) => ContainerId.Equals(other.ContainerId) && Index == other.Index;
        public override bool Equals(object obj) => obj is SlotAddress other && Equals(other);
        public override int GetHashCode() => (ContainerId.GetHashCode() * 397) ^ Index;
        public override string ToString() => $"{ContainerId}[{Index}]";
        public static bool operator ==(SlotAddress left, SlotAddress right) => left.Equals(right);
        public static bool operator !=(SlotAddress left, SlotAddress right) => !left.Equals(right);
    }
}
