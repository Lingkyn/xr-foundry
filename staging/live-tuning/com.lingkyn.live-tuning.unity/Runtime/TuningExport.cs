using System;
using System.Collections.Generic;
using System.Linq;
using Lingkyn.LiveTuning.Core;
using UnityEngine;

namespace Lingkyn.LiveTuning.Unity
{
    // An injectable ITuningExportSink with two implementations: a device sink that writes under
    // the persistent data path, and an Editor-only sink (see EditorAssetExportSink below, guarded
    // by #if UNITY_EDITOR) that writes into a bound asset. A sink is never chosen by platform
    // detection inside the family, only by the explicit reference the consumer passes.

    /// <summary>The structured outcome of one export: the resolved path, platform, byte count,
    /// and the ids actually written (only the ids the state overrides), or a stable failure code.</summary>
    public sealed class TuningExportResult
    {
        private TuningExportResult(bool succeeded, string code, string path, string platform, int byteCount, IReadOnlyList<TunableId> idsWritten, string message)
        {
            Succeeded = succeeded;
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Platform = platform ?? string.Empty;
            ByteCount = byteCount;
            IdsWritten = idsWritten ?? Array.Empty<TunableId>();
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Path { get; }
        public string Platform { get; }
        public int ByteCount { get; }
        public IReadOnlyList<TunableId> IdsWritten { get; }
        public string Message { get; }

        public static TuningExportResult Ok(string path, string platform, int byteCount, IReadOnlyList<TunableId> idsWritten) =>
            new TuningExportResult(true, string.Empty, path, platform, byteCount, idsWritten, string.Empty);

        public static TuningExportResult Failed(string code, string path, string message) =>
            new TuningExportResult(false, code, path, string.Empty, 0, Array.Empty<TunableId>(), message);
    }

    public interface ITuningExportSink
    {
        /// <summary>Writes only the ids <paramref name="document"/> overrides. An override-free
        /// document still writes (an empty override list), rather than being skipped.</summary>
        TuningExportResult Write(TokenOverrideDocument document, string label);
    }

    /// <summary>The file-write seam a device sink goes through, so an EditMode test can inject a
    /// fake that fails deterministically instead of depending on real disk permissions.</summary>
    public interface ITuningFileSystem
    {
        void WriteAllText(string path, string contents);
    }

    public sealed class RealTuningFileSystem : ITuningFileSystem
    {
        public void WriteAllText(string path, string contents) => System.IO.File.WriteAllText(path, contents);
    }

    /// <summary>Writes the override document under <c>Application.persistentDataPath</c> at a
    /// path this sink builds explicitly from the relative file name the consumer passes.</summary>
    public sealed class DeviceTokenExportSink : ITuningExportSink
    {
        private readonly string _relativeFileName;
        private readonly ITuningFileSystem _fileSystem;

        public DeviceTokenExportSink(string relativeFileName, ITuningFileSystem fileSystem = null)
        {
            if (string.IsNullOrEmpty(relativeFileName)) throw new ArgumentException("A device export sink needs an explicit relative file name.", nameof(relativeFileName));
            _relativeFileName = relativeFileName;
            _fileSystem = fileSystem ?? new RealTuningFileSystem();
        }

        public TuningExportResult Write(TokenOverrideDocument document, string label)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var path = System.IO.Path.Combine(Application.persistentDataPath, _relativeFileName);
            var json = document.ToJson();
            try
            {
                _fileSystem.WriteAllText(path, json);
            }
            catch (Exception error)
            {
                return TuningExportResult.Failed(LiveTuningUnityFailure.ExportWriteFailed, path, $"Could not write '{path}': {error.Message}");
            }
            var ids = document.Overrides.Select(entry => entry.Id).ToList();
            return TuningExportResult.Ok(path, Application.platform.ToString(), System.Text.Encoding.UTF8.GetByteCount(json), ids);
        }
    }

    /// <summary>A plain asset an Editor sink writes overrides into. Holds no product token value
    /// of its own; <see cref="OverrideJson"/> is only ever the last document written to it.</summary>
    [CreateAssetMenu(menuName = "Lingkyn/Live Tuning/Override Asset", fileName = "LiveTuningOverrides")]
    public sealed class LiveTuningOverrideAsset : ScriptableObject
    {
        [SerializeField] private string overrideJson = string.Empty;

        public string OverrideJson { get => overrideJson; set => overrideJson = value ?? string.Empty; }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only export sink: writes the override document's JSON into a bound
    /// <see cref="LiveTuningOverrideAsset"/>, marks it dirty, and saves it. This type is compiled
    /// out of every player build by the surrounding <c>#if UNITY_EDITOR</c>, so it can never be
    /// reached from one.</summary>
    public sealed class EditorAssetExportSink : ITuningExportSink
    {
        private readonly LiveTuningOverrideAsset _asset;

        public EditorAssetExportSink(LiveTuningOverrideAsset asset)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        public TuningExportResult Write(TokenOverrideDocument document, string label)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var path = UnityEditor.AssetDatabase.GetAssetPath(_asset);
            if (string.IsNullOrEmpty(path))
            {
                return TuningExportResult.Failed(LiveTuningUnityFailure.ExportWriteFailed, string.Empty, "The bound override asset is not saved on disk.");
            }
            var json = document.ToJson();
            _asset.OverrideJson = json;
            UnityEditor.EditorUtility.SetDirty(_asset);
            UnityEditor.AssetDatabase.SaveAssets();
            var ids = document.Overrides.Select(entry => entry.Id).ToList();
            return TuningExportResult.Ok(path, "Editor", System.Text.Encoding.UTF8.GetByteCount(json), ids);
        }
    }
#endif
}
