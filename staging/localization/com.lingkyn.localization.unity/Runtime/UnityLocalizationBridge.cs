using System;
using System.Collections.Generic;
using Lingkyn.Localization.Core;

namespace Lingkyn.Localization.Unity
{
    /// <summary>
    /// Outcome of a bridge call. <see cref="Code"/> is a stable machine code:
    /// bridge.package.missing, bridge.table.missing, bridge.locale.invalid,
    /// bridge.entry.invalid, or empty on success.
    /// </summary>
    public readonly struct BridgeResult
    {
        /// <summary>The Unity Localization package is not installed in the consuming project.</summary>
        public const string PackageMissing = "bridge.package.missing";

        /// <summary>The reader returned no table data for the requested collection.</summary>
        public const string TableMissing = "bridge.table.missing";

        /// <summary>A locale key returned by the reader is not a valid language[-script][-region] tag.</summary>
        public const string LocaleInvalid = "bridge.locale.invalid";

        /// <summary>A message id or template returned by the reader was rejected by the Core.</summary>
        public const string EntryInvalid = "bridge.entry.invalid";

        private BridgeResult(bool succeeded, string code, string message)
        {
            Succeeded = succeeded;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }

        public static BridgeResult Ok() => new BridgeResult(true, string.Empty, string.Empty);

        public static BridgeResult Fail(string code, string message) => new BridgeResult(false, code, message);
    }

    /// <summary>
    /// Optional, fail-closed bridge from Unity Localization string tables to Core
    /// <see cref="MessageTable"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The adapter never links the Unity Localization assembly. Its presence is detected
    /// through the <c>LINGKYN_UNITY_LOCALIZATION</c> version define that the Runtime asmdef
    /// declares for <c>com.unity.localization</c>; the assembly itself is not listed in the
    /// asmdef references, so no <c>UnityEngine.Localization</c> type is named here even when
    /// the define is set. That keeps the package optional and this assembly compiling in
    /// projects that do not install it.
    /// </para>
    /// <para>
    /// The <c>tableReader</c> delegate is the seam a consumer fills with data from Unity
    /// Localization: it receives the string table collection name and returns
    /// locale tag -> message id -> ICU template, typically by enumerating the collection's
    /// <c>StringTable</c> instances and their entries. The bridge validates every key and
    /// value through the Core and reports the first failure with a stable code instead of
    /// building a partial catalog (LESSON-004: by-name resolution fails closed).
    /// </para>
    /// </remarks>
    public static class UnityLocalizationBridge
    {
        /// <summary>
        /// True only when the Runtime assembly was compiled with the
        /// <c>LINGKYN_UNITY_LOCALIZATION</c> version define, that is, when
        /// <c>com.unity.localization</c> 1.0.0 or newer is installed in the project.
        /// </summary>
        public static bool IsPackagePresent
        {
            get
            {
#if LINGKYN_UNITY_LOCALIZATION
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Imports one string table collection through <paramref name="tableReader"/>.
        /// When the Unity Localization package is absent the reader is never invoked and
        /// the result carries <see cref="BridgeResult.PackageMissing"/>. When the reader
        /// returns null or no locales the result carries <see cref="BridgeResult.TableMissing"/>.
        /// </summary>
        /// <param name="tableCollectionName">The Unity Localization string table collection to read.</param>
        /// <param name="tableReader">Consumer-supplied seam returning locale tag -> message id -> template.</param>
        /// <param name="tables">One immutable table per locale on success; empty otherwise.</param>
        public static BridgeResult TryImport(
            string tableCollectionName,
            Func<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> tableReader,
            out IReadOnlyList<MessageTable> tables)
        {
            tables = Array.Empty<MessageTable>();

            if (!IsPackagePresent)
            {
                return BridgeResult.Fail(
                    BridgeResult.PackageMissing,
                    "The Unity Localization package (com.unity.localization) is not installed, so string table collection '"
                    + (tableCollectionName ?? string.Empty) + "' cannot be read.");
            }

            if (tableReader == null) throw new ArgumentNullException(nameof(tableReader));

            var locales = tableReader(tableCollectionName);
            if (locales == null || locales.Count == 0)
            {
                return BridgeResult.Fail(
                    BridgeResult.TableMissing,
                    "String table collection '" + (tableCollectionName ?? string.Empty) + "' returned no locales.");
            }

            var built = new List<MessageTable>(locales.Count);
            foreach (var localePair in locales)
            {
                var locale = LocaleId.TryParse(localePair.Key);
                if (!locale.Succeeded)
                {
                    return BridgeResult.Fail(
                        BridgeResult.LocaleInvalid,
                        "String table collection '" + (tableCollectionName ?? string.Empty) + "': " + locale.Message);
                }

                var builder = new MessageTableBuilder(locale.Value);
                if (localePair.Value != null)
                {
                    foreach (var entry in localePair.Value)
                    {
                        var added = builder.Add(entry.Key, entry.Value);
                        if (!added.Succeeded)
                        {
                            return BridgeResult.Fail(
                                BridgeResult.EntryInvalid,
                                "String table collection '" + (tableCollectionName ?? string.Empty) + "' (" + locale.Value.Tag + "): " + added.Message);
                        }
                    }
                }
                built.Add(builder.Build());
            }

            tables = built;
            return BridgeResult.Ok();
        }
    }
}
