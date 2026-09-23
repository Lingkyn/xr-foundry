using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lingkyn.XrUiShell.Core
{
    // The immutable shell layout model: a closed set of declared surfaces (panels, wrist menus,
    // hand menus), each naming one home anchor kind and the closed set of anchor kinds it
    // admits, plus a closed set of registered input sources. Built only by explicit declaration
    // and registration, from any reflection, attribute scan, or scene search. A layout is built
    // once and never mutated; ShellState (in ShellState.cs) applies placement intents against it.

    /// <summary>One declared surface: its identity, its home anchor kind, and the closed set of
    /// anchor kinds it admits for dock and follow. A wrist menu's admitted set is always exactly
    /// { Wrist } and a hand menu's is always exactly { Hand }; a general panel's admitted set is
    /// whatever the declaration passed, always widened to include the home anchor kind.</summary>
    public sealed class SurfaceDeclaration
    {
        private readonly HashSet<AnchorKind> _admitted;

        internal SurfaceDeclaration(SurfaceId id, AnchorKind home, IEnumerable<AnchorKind> admitted)
        {
            Id = id;
            Home = home;
            _admitted = new HashSet<AnchorKind>(admitted) { home };
        }

        public SurfaceId Id { get; }
        public AnchorKind Home { get; }
        public IReadOnlyCollection<AnchorKind> AdmittedAnchors => _admitted;

        public bool Admits(AnchorKind anchor) => _admitted.Contains(anchor);

        internal string Fingerprint() =>
            $"{Id}:home={Home}:admits={string.Join(",", _admitted.OrderBy(a => (int)a))}";
    }

    /// <summary>One registered input source: its identity and its closed kind.</summary>
    public sealed class InputSourceRegistration
    {
        internal InputSourceRegistration(InputSourceId id, InputSourceKind kind)
        {
            Id = id;
            Kind = kind;
        }

        public InputSourceId Id { get; }
        public InputSourceKind Kind { get; }

        internal string Fingerprint() => $"{Id}:{Kind}";
    }

    /// <summary>The immutable, explicitly-declared shell layout: every declared surface and every
    /// registered input source. Enumerates in canonical id order and exposes no mutator.</summary>
    public sealed class ShellLayout
    {
        private readonly SortedDictionary<SurfaceId, SurfaceDeclaration> _surfaces;
        private readonly SortedDictionary<InputSourceId, InputSourceRegistration> _sources;
        private readonly string _fingerprint;

        private ShellLayout(SortedDictionary<SurfaceId, SurfaceDeclaration> surfaces, SortedDictionary<InputSourceId, InputSourceRegistration> sources)
        {
            _surfaces = surfaces;
            _sources = sources;
            var builder = new StringBuilder();
            foreach (var declaration in _surfaces.Values) builder.Append(declaration.Fingerprint()).Append(';');
            builder.Append('|');
            foreach (var registration in _sources.Values) builder.Append(registration.Fingerprint()).Append(';');
            _fingerprint = builder.ToString();
        }

        public int SurfaceCount => _surfaces.Count;
        public int SourceCount => _sources.Count;

        /// <summary>Every declared surface, in canonical (kind, id) order.</summary>
        public IEnumerable<SurfaceDeclaration> Surfaces => _surfaces.Values;

        /// <summary>Every registered input source, in canonical id order.</summary>
        public IEnumerable<InputSourceRegistration> Sources => _sources.Values;

        public bool TryGetSurface(SurfaceId id, out SurfaceDeclaration declaration) => _surfaces.TryGetValue(id, out declaration);
        public bool TryGetSource(InputSourceId id, out InputSourceRegistration registration) => _sources.TryGetValue(id, out registration);

        /// <summary>A canonical text covering every declaration and registration; equal layouts
        /// (built in any declaration order) have equal fingerprints.</summary>
        public string Fingerprint() => _fingerprint;

        internal static ShellLayout FromEntries(SortedDictionary<SurfaceId, SurfaceDeclaration> surfaces, SortedDictionary<InputSourceId, InputSourceRegistration> sources) =>
            new ShellLayout(surfaces, sources);
    }

    /// <summary>Collects surface declarations and input source registrations one at a time; each
    /// call validates immediately and either extends the builder or leaves it unchanged and
    /// returns a stable failure code.</summary>
    public sealed class ShellLayoutBuilder
    {
        private readonly SortedDictionary<SurfaceId, SurfaceDeclaration> _surfaces = new SortedDictionary<SurfaceId, SurfaceDeclaration>();
        private readonly SortedDictionary<InputSourceId, InputSourceRegistration> _sources = new SortedDictionary<InputSourceId, InputSourceRegistration>();

        public int SurfaceCount => _surfaces.Count;
        public int SourceCount => _sources.Count;

        public bool ContainsSurface(SurfaceId id) => _surfaces.ContainsKey(id);

        /// <summary>Declares a general panel admitting <paramref name="admittedAnchors"/> (always
        /// widened to include <paramref name="home"/>). Rejects a panel id already declared with
        /// panel.duplicate, leaving the builder unchanged.</summary>
        public ShellResult<ShellLayoutBuilder> DeclarePanel(PanelId id, AnchorKind home, params AnchorKind[] admittedAnchors) =>
            Declare(SurfaceId.OfPanel(id), home, admittedAnchors ?? Array.Empty<AnchorKind>());

        /// <summary>Declares a wrist menu. Its home anchor and its whole admitted set are always
        /// exactly <see cref="AnchorKind.Wrist"/>, regardless of any anchor kind a caller might
        /// otherwise have supplied.</summary>
        public ShellResult<ShellLayoutBuilder> DeclareWristMenu(WristMenuId id) =>
            Declare(SurfaceId.OfWristMenu(id), AnchorKind.Wrist, new[] { AnchorKind.Wrist });

        /// <summary>Declares a hand menu. Its home anchor and its whole admitted set are always
        /// exactly <see cref="AnchorKind.Hand"/>.</summary>
        public ShellResult<ShellLayoutBuilder> DeclareHandMenu(HandMenuId id) =>
            Declare(SurfaceId.OfHandMenu(id), AnchorKind.Hand, new[] { AnchorKind.Hand });

        private ShellResult<ShellLayoutBuilder> Declare(SurfaceId id, AnchorKind home, IEnumerable<AnchorKind> admittedAnchors)
        {
            if (_surfaces.ContainsKey(id))
            {
                return ShellResult<ShellLayoutBuilder>.Fail(ShellFailure.PanelDuplicate, $"Surface '{id}' is already declared.", id.ToString());
            }
            _surfaces[id] = new SurfaceDeclaration(id, home, admittedAnchors);
            return ShellResult<ShellLayoutBuilder>.Ok(this);
        }

        /// <summary>Registers one input source. A second registration for the same id replaces
        /// the first (last write wins); this is a data update, never a rejection, because the
        /// contract names no failure code for it.</summary>
        public ShellLayoutBuilder RegisterInputSource(InputSourceId id, InputSourceKind kind)
        {
            _sources[id] = new InputSourceRegistration(id, kind);
            return this;
        }

        /// <summary>Produces the immutable layout. Safe to call more than once; each call
        /// snapshots the current entries so a later successful declaration never mutates a
        /// layout already handed out.</summary>
        public ShellLayout Build() => ShellLayout.FromEntries(
            new SortedDictionary<SurfaceId, SurfaceDeclaration>(_surfaces),
            new SortedDictionary<InputSourceId, InputSourceRegistration>(_sources));
    }
}
