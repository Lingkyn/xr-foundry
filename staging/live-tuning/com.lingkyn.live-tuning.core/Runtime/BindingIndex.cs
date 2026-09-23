using System;
using System.Collections.Generic;

namespace Lingkyn.LiveTuning.Core
{
    // "Point at it, tune it": a BindingIndex answers, for one opaque target path, exactly the
    // validated binding records that path names or contains. It is the only lookup an adapter
    // uses to go from a selected thing back to its tunables, so selecting a thing changes which
    // slots are shown and never which editor a slot holds. No reflection, no engine type: a
    // target path enters and leaves this file as an opaque string, split only on the segment
    // separator below.

    /// <summary>Immutable index from an opaque target path to the validated binding records it
    /// names or contains. Built once from a validated <see cref="BindingSet"/>; nothing rebuilds
    /// or mutates it afterwards, and it holds no reference to a registry, a state, or a
    /// registration: only the target paths and the records already validated against one.</summary>
    public sealed class BindingIndex
    {
        /// <summary>The separator every target path in this index is split on to compare
        /// segment-wise rather than as raw text: "a/b" is a prefix of "a/b/c" (segments
        /// ["a","b"] vs ["a","b","c"]) but not of "a/bc" (segments ["a","b"] vs ["a","bc"]). A
        /// target path with no separator is a single segment and can only ever match by exact
        /// equality; the Unity adapter's own "asset_key#member" convention has no '/' in it and
        /// is unaffected by this splitting.</summary>
        public const char PathSegmentSeparator = '/';

        private static readonly IReadOnlyList<BindingRecord> EmptyMatches = Array.Empty<BindingRecord>();

        private readonly List<Entry> _entries;

        private BindingIndex(List<Entry> entries)
        {
            _entries = entries;
        }

        /// <summary>Builds an index over every record <paramref name="bindings"/> holds, in the
        /// order the validated set enumerates them (canonical tunable-id order). Building never
        /// fails: every target path, however malformed, is just a sequence of segments to
        /// compare against.</summary>
        public static BindingIndex Build(BindingSet bindings)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            var entries = new List<Entry>();
            foreach (var record in bindings.Records)
            {
                entries.Add(new Entry(Split(record.TargetPath), record));
            }
            return new BindingIndex(entries);
        }

        /// <summary>Exactly the binding records whose target path equals <paramref name="targetPath"/>
        /// or whose target path has it as a segment-wise prefix, in the order this index was
        /// built (registration order). An empty list when no record targets the path.</summary>
        public IReadOnlyList<BindingRecord> Lookup(string targetPath)
        {
            var querySegments = Split(targetPath ?? string.Empty);
            List<BindingRecord> matches = null;
            foreach (var entry in _entries)
            {
                if (!IsSegmentWisePrefix(querySegments, entry.Segments)) continue;
                (matches ??= new List<BindingRecord>()).Add(entry.Record);
            }
            return matches ?? EmptyMatches;
        }

        private static bool IsSegmentWisePrefix(string[] prefix, string[] full)
        {
            if (prefix.Length > full.Length) return false;
            for (var index = 0; index < prefix.Length; index++)
            {
                if (!string.Equals(prefix[index], full[index], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static string[] Split(string targetPath) => (targetPath ?? string.Empty).Split(PathSegmentSeparator);

        private readonly struct Entry
        {
            public Entry(string[] segments, BindingRecord record)
            {
                Segments = segments;
                Record = record;
            }

            public string[] Segments { get; }
            public BindingRecord Record { get; }
        }
    }
}
