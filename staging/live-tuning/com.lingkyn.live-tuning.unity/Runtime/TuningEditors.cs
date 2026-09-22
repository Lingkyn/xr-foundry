using System;
using Lingkyn.LiveTuning.Core;

namespace Lingkyn.LiveTuning.Unity
{
    // Exactly one editor implementation per EditorKind: a slider with the registered range and
    // step for float_editor and integer_editor, a toggle for bool_editor, a picker over the
    // registered value set for enumerated_editor, a colour control for colour_editor, and
    // per-axis sliders for vector2_editor and vector3_editor. A corner radius, a spacing value, a
    // type-scale value, and a hit-target size are all float or integer tunables edited by the
    // same float or integer editor; a colour anywhere in any package, skin, or scene is edited by
    // the same colour editor. These are plain data-holding controls: how each one is drawn is a
    // later, device-facing concern; what every test here proves is which one control type
    // attaches per kind, and that a value change raises exactly one Core set intent.

    /// <summary>One attached editor control. <see cref="Attach"/> is called once, at attach time,
    /// with the tunable's current effective value and a callback the control invokes with a new
    /// value whenever its own input changes. <see cref="SetValue"/> lets the host push a value
    /// back onto the control (for example after a rejected intent) without re-raising the callback.</summary>
    public interface ITuningEditorControl
    {
        EditorKind Kind { get; }
        void Attach(TunableRegistration registration, TunableValue initialValue, Action<TunableValue> onChanged);
        void SetValue(TunableValue value);
        /// <summary>The value this control currently displays, for a test or a host to read back.</summary>
        TunableValue CurrentValue { get; }
    }

    /// <summary>Base with the callback plumbing every editor control shares, so each closed-set
    /// member differs only in its declared <see cref="Kind"/>.</summary>
    public abstract class TuningEditorControlBase : ITuningEditorControl
    {
        private Action<TunableValue> _onChanged;

        public abstract EditorKind Kind { get; }
        public TunableRegistration Registration { get; private set; }
        public TunableValue CurrentValue { get; private set; }

        public void Attach(TunableRegistration registration, TunableValue initialValue, Action<TunableValue> onChanged)
        {
            Registration = registration ?? throw new ArgumentNullException(nameof(registration));
            CurrentValue = initialValue;
            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        }

        public void SetValue(TunableValue value) => CurrentValue = value;

        /// <summary>A test (or a future real widget) calls this to simulate the person changing
        /// the control's own input; it raises exactly one Core set intent through the callback.</summary>
        public void RaiseChange(TunableValue value)
        {
            CurrentValue = value;
            _onChanged?.Invoke(value);
        }
    }

    public sealed class FloatEditorControl : TuningEditorControlBase { public override EditorKind Kind => EditorKind.FloatEditor; }
    public sealed class IntegerEditorControl : TuningEditorControlBase { public override EditorKind Kind => EditorKind.IntegerEditor; }
    public sealed class BoolEditorControl : TuningEditorControlBase { public override EditorKind Kind => EditorKind.BoolEditor; }
    public sealed class EnumeratedEditorControl : TuningEditorControlBase { public override EditorKind Kind => EditorKind.EnumeratedEditor; }
    public sealed class ColourEditorControl : TuningEditorControlBase { public override EditorKind Kind => EditorKind.ColourEditor; }
    public sealed class Vector2EditorControl : TuningEditorControlBase { public override EditorKind Kind => EditorKind.Vector2Editor; }
    public sealed class Vector3EditorControl : TuningEditorControlBase { public override EditorKind Kind => EditorKind.Vector3Editor; }

    /// <summary>Resolves a tunable kind to the one editor control type the adapter implements for
    /// it, through <see cref="EditorKindResolver.ResolveEditorKind"/>. A kind with no implemented
    /// editor (an <see cref="EditorKind"/> value this switch has no case for, which given the
    /// closed set can only be a defect) fails closed with tunable.kind.unsupported and creates no
    /// control, never an empty or placeholder one.</summary>
    public static class TuningEditorFactory
    {
        public static LiveTuningResult<ITuningEditorControl> Create(TunableKind kind)
        {
            var resolved = EditorKindResolver.ResolveEditorKind(kind);
            if (!resolved.Succeeded) return resolved.As<ITuningEditorControl>();
            switch (resolved.Value)
            {
                case EditorKind.FloatEditor: return LiveTuningResult<ITuningEditorControl>.Ok(new FloatEditorControl());
                case EditorKind.IntegerEditor: return LiveTuningResult<ITuningEditorControl>.Ok(new IntegerEditorControl());
                case EditorKind.BoolEditor: return LiveTuningResult<ITuningEditorControl>.Ok(new BoolEditorControl());
                case EditorKind.EnumeratedEditor: return LiveTuningResult<ITuningEditorControl>.Ok(new EnumeratedEditorControl());
                case EditorKind.ColourEditor: return LiveTuningResult<ITuningEditorControl>.Ok(new ColourEditorControl());
                case EditorKind.Vector2Editor: return LiveTuningResult<ITuningEditorControl>.Ok(new Vector2EditorControl());
                case EditorKind.Vector3Editor: return LiveTuningResult<ITuningEditorControl>.Ok(new Vector3EditorControl());
                default:
                    return LiveTuningResult<ITuningEditorControl>.Fail(LiveTuningFailure.TunableKindUnsupported, $"No editor control is implemented for editor kind '{resolved.Value}'.");
            }
        }
    }
}
