using System;
using System.Collections.Generic;
using Lingkyn.XrUiShell.Core;

namespace Lingkyn.XrUiShell.UiToolkit
{
    // A plain runtime constructed with explicit references (the layout, the initial Core state,
    // and a validated binding set) that applies every accepted placement intent to the bound
    // document subtrees, in the order the intents were accepted, and forwards a resolved routed
    // pointer event to the bound document only. No scene singleton, static instance, scene
    // search, or reflection discovery; two runtimes constructed side by side over two binding
    // sets share nothing.

    public sealed class UiToolkitShellRuntime
    {
        private readonly List<ShellIntentOutcome> _outcomes = new List<ShellIntentOutcome>();

        public UiToolkitShellRuntime(ShellLayout layout, ShellState initialState, UiToolkitPanelBindingSet bindings)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
            if (!ReferenceEquals(initialState.Layout, layout))
            {
                throw new ArgumentException("The initial state must be built over this runtime's layout.", nameof(initialState));
            }
        }

        public ShellLayout Layout { get; }
        public UiToolkitPanelBindingSet Bindings { get; }
        public ShellState State { get; private set; }

        /// <summary>Every placement intent this runtime saw, accepted or rejected, in order.</summary>
        public IReadOnlyList<ShellIntentOutcome> Outcomes => _outcomes;

        /// <summary>Applies one placement intent to the Core state. On acceptance, every bound
        /// document whose panel's open flag changed has its GameObject's active state set to
        /// match, in canonical surface order; a rejected intent touches no document.</summary>
        public ShellIntentOutcome Apply(ShellIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            var index = _outcomes.Count;
            var previous = State;
            var result = State.Apply(intent);
            if (!result.Succeeded)
            {
                var rejected = new ShellIntentOutcome(index, intent, false, result.Code, result.Message, State.Revision);
                _outcomes.Add(rejected);
                return rejected;
            }
            State = result.Value;
            ApplyChangedOpenFlags(previous, State);
            var accepted = new ShellIntentOutcome(index, intent, true, result.Code, result.Message, State.Revision);
            _outcomes.Add(accepted);
            return accepted;
        }

        private void ApplyChangedOpenFlags(ShellState previous, ShellState current)
        {
            foreach (var runtime in current.Surfaces)
            {
                var wasOpen = previous.TryGetSurfaceState(runtime.Id, out var before) && before.IsOpen;
                if (wasOpen == runtime.IsOpen) continue;
                if (Bindings.TryGet(runtime.Id, out var entry) && entry.Document != null)
                {
                    entry.Document.gameObject.SetActive(runtime.IsOpen);
                }
            }
        }

        /// <summary>Resolves a routing intent against the current Core state and, only on
        /// success, forwards it to the resolved panel's bound document through
        /// <see cref="IUiToolkitRoutedEventTarget"/>. Forwards nothing on route.ambiguous,
        /// route.none, source.unknown, or source.kind.unsupported.</summary>
        public ShellResult<SurfaceId> Route(RoutingIntent intent)
        {
            var result = ShellRouter.Resolve(State, intent);
            if (result.Succeeded && Bindings.TryGet(result.Value, out var entry))
            {
                entry.RoutedEventTarget?.OnRouted(intent.Kind, intent.Source);
            }
            return result;
        }

        /// <summary>Propagates <paramref name="skin"/> to every bound document's skin target, in
        /// canonical surface order.</summary>
        public void ApplySkin(UiToolkitShellSkin skin)
        {
            foreach (var surfaceId in Bindings.BoundSurfaces)
            {
                if (Bindings.TryGet(surfaceId, out var entry))
                {
                    entry.SkinTarget?.ApplySkin(skin);
                }
            }
        }
    }
}
