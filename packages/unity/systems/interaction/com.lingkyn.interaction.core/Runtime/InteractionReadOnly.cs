using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Lingkyn.Interaction.Core
{
    internal static class InteractionReadOnly
    {
        public static IReadOnlyList<T> FreezeList<T>(IList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            var array = new T[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                array[i] = items[i];
            }

            return Array.AsReadOnly(array);
        }

        public static IReadOnlyList<T> FreezeList<T>(IEnumerable<T> items)
        {
            if (items == null)
            {
                return Array.Empty<T>();
            }

            if (items is IList<T> list)
            {
                return FreezeList(list);
            }

            return Array.AsReadOnly(items.ToArray());
        }

        public static IReadOnlyDictionary<TKey, TValue> FreezeDictionary<TKey, TValue>(
            IDictionary<TKey, TValue> source)
        {
            if (source == null || source.Count == 0)
            {
                return new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>());
            }

            var copy = new Dictionary<TKey, TValue>();
            foreach (var pair in source)
            {
                copy[pair.Key] = pair.Value;
            }

            return new ReadOnlyDictionary<TKey, TValue>(copy);
        }

        public static byte[] CopyBytes(byte[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }
    }
}
