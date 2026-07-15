using System;
using System.Collections.Generic;

namespace Lingkyn.Inventory.Core
{
    public readonly struct ItemStateFragmentTypeId : IEquatable<ItemStateFragmentTypeId>
    {
        public ItemStateFragmentTypeId(string value)
        {
            Value = IdentifierGuard.Require(value, nameof(value));
        }

        public string Value { get; }
        public bool Equals(ItemStateFragmentTypeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ItemStateFragmentTypeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(ItemStateFragmentTypeId left, ItemStateFragmentTypeId right) => left.Equals(right);
        public static bool operator !=(ItemStateFragmentTypeId left, ItemStateFragmentTypeId right) => !left.Equals(right);
    }

    public sealed class ItemStateFragment : IEquatable<ItemStateFragment>
    {
        internal ItemStateFragment(ItemStateFragmentTypeId typeId, int schemaVersion, string payload)
        {
            IdentifierGuard.Require(typeId.Value, nameof(typeId));
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "A fragment schema version must be positive.");
            }

            TypeId = typeId;
            SchemaVersion = schemaVersion;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public ItemStateFragmentTypeId TypeId { get; }
        public int SchemaVersion { get; }
        public string Payload { get; }

        public bool Equals(ItemStateFragment other)
        {
            return other != null
                && TypeId == other.TypeId
                && SchemaVersion == other.SchemaVersion
                && string.Equals(Payload, other.Payload, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as ItemStateFragment);
        public override int GetHashCode()
        {
            var hash = (TypeId.GetHashCode() * 397) ^ SchemaVersion;
            return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Payload);
        }
    }

    public abstract class ItemStateFragmentCodec<T>
    {
        protected ItemStateFragmentCodec(ItemStateFragmentTypeId typeId, int currentSchemaVersion)
        {
            IdentifierGuard.Require(typeId.Value, nameof(typeId));
            if (currentSchemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(currentSchemaVersion));
            }

            TypeId = typeId;
            CurrentSchemaVersion = currentSchemaVersion;
        }

        public ItemStateFragmentTypeId TypeId { get; }
        public int CurrentSchemaVersion { get; }
        public abstract string Encode(T value);
        public abstract T Decode(int schemaVersion, string payload);
    }

    public sealed class ItemStateFragmentRegistry
    {
        private readonly Dictionary<ItemStateFragmentTypeId, ICodecAdapter> _codecs =
            new Dictionary<ItemStateFragmentTypeId, ICodecAdapter>();

        public void Register<T>(ItemStateFragmentCodec<T> codec)
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }

            if (_codecs.ContainsKey(codec.TypeId))
            {
                throw new ArgumentException($"A codec is already registered for '{codec.TypeId}'.", nameof(codec));
            }

            _codecs.Add(codec.TypeId, new CodecAdapter<T>(codec));
        }

        public ItemStateFragment Create<T>(ItemStateFragmentCodec<T> codec, T value)
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }

            if (!_codecs.TryGetValue(codec.TypeId, out var registered)
                || registered.ValueType != typeof(T)
                || !ReferenceEquals(registered.Codec, codec))
            {
                throw new InvalidOperationException($"The codec for '{codec.TypeId}' is not registered for '{typeof(T).FullName}'.");
            }

            var payload = codec.Encode(value);
            if (payload == null)
            {
                throw new InvalidOperationException($"Codec '{codec.TypeId}' returned a null payload.");
            }

            var fragment = new ItemStateFragment(codec.TypeId, codec.CurrentSchemaVersion, payload);
            registered.Validate(fragment);
            return fragment;
        }

        public T Read<T>(ItemStateFragmentCodec<T> codec, ItemStateFragment fragment)
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }

            if (fragment == null)
            {
                throw new ArgumentNullException(nameof(fragment));
            }

            if (fragment.TypeId != codec.TypeId)
            {
                throw new ArgumentException($"Fragment '{fragment.TypeId}' cannot be read by codec '{codec.TypeId}'.", nameof(fragment));
            }

            if (!_codecs.TryGetValue(fragment.TypeId, out var registered)
                || registered.ValueType != typeof(T)
                || !ReferenceEquals(registered.Codec, codec))
            {
                throw new InvalidOperationException($"No compatible codec is registered for '{fragment.TypeId}'.");
            }

            return (T)registered.Decode(fragment);
        }

        public bool TryValidate(ItemStateFragment fragment, out string message)
        {
            message = string.Empty;
            if (fragment == null)
            {
                message = "An instance-state fragment cannot be null.";
                return false;
            }

            if (!_codecs.TryGetValue(fragment.TypeId, out var codec))
            {
                message = $"No codec is registered for instance-state fragment '{fragment.TypeId}'.";
                return false;
            }

            try
            {
                codec.Validate(fragment);
                return true;
            }
            catch (Exception exception)
            {
                message = $"Instance-state fragment '{fragment.TypeId}' is invalid: {exception.Message}";
                return false;
            }
        }

        internal ItemStateFragment Rehydrate(
            ItemStateFragmentTypeId typeId,
            int schemaVersion,
            string payload)
        {
            var fragment = new ItemStateFragment(typeId, schemaVersion, payload);
            if (!_codecs.TryGetValue(typeId, out var codec))
            {
                throw new InvalidOperationException($"No codec is registered for instance-state fragment '{typeId}'.");
            }

            return codec.Normalize(fragment);
        }

        private interface ICodecAdapter
        {
            Type ValueType { get; }
            object Codec { get; }
            object Decode(ItemStateFragment fragment);
            ItemStateFragment Normalize(ItemStateFragment fragment);
            void Validate(ItemStateFragment fragment);
        }

        private sealed class CodecAdapter<T> : ICodecAdapter
        {
            private readonly ItemStateFragmentCodec<T> _codec;

            public CodecAdapter(ItemStateFragmentCodec<T> codec)
            {
                _codec = codec;
            }

            public Type ValueType => typeof(T);
            public object Codec => _codec;
            public object Decode(ItemStateFragment fragment) => _codec.Decode(fragment.SchemaVersion, fragment.Payload);

            public ItemStateFragment Normalize(ItemStateFragment fragment)
            {
                Validate(fragment);
                var value = (T)Decode(fragment);
                var payload = _codec.Encode(value);
                if (payload == null)
                {
                    throw new InvalidOperationException($"Codec '{_codec.TypeId}' returned a null payload.");
                }

                return new ItemStateFragment(_codec.TypeId, _codec.CurrentSchemaVersion, payload);
            }

            public void Validate(ItemStateFragment fragment)
            {
                if (fragment.TypeId != _codec.TypeId)
                {
                    throw new ArgumentException("The fragment type does not match the codec.");
                }

                if (fragment.SchemaVersion > _codec.CurrentSchemaVersion)
                {
                    throw new InvalidOperationException(
                        $"Fragment schema {fragment.SchemaVersion} is newer than supported schema {_codec.CurrentSchemaVersion}.");
                }

                if (Decode(fragment) == null)
                {
                    throw new InvalidOperationException("The codec returned no typed state.");
                }
            }
        }
    }
}
