namespace Lingkyn.QualityTiers.Unity
{
    // The explicit, injected XRSettings-style accessor bullet 6 of the Unity adapter gate names
    // alongside the display-subsystem accessor, a render-pipeline asset reference, and an Adaptive
    // Performance provider query: a target this adapter may resolve by name or by an optional
    // lookup, reporting settings.unresolved rather than staying silent when it is missing
    // (LESSON-004).

    /// <summary>Explicit, injected access to an XRSettings-style write target beyond render scale
    /// (for example a vendor-specific eye-texture or dynamic-resolution setting some runtimes
    /// expose only by name). A real implementation resolves it once at construction; a fixed fake
    /// reports whatever a test wires it to.</summary>
    public interface IXrSettingsAccessor
    {
        bool TryResolve(out string settingName);
    }

    public sealed class FixedXrSettingsAccessor : IXrSettingsAccessor
    {
        private readonly bool _resolves;
        private readonly string _settingName;

        public FixedXrSettingsAccessor(bool resolves, string settingName = "fake_xr_setting")
        {
            _resolves = resolves;
            _settingName = settingName ?? string.Empty;
        }

        public bool TryResolve(out string settingName)
        {
            settingName = _resolves ? _settingName : string.Empty;
            return _resolves;
        }
    }

    /// <summary>Resolves an <see cref="IXrSettingsAccessor"/> without throwing, reporting
    /// <see cref="QualityUnityFailure.SettingsUnresolved"/> explicitly rather than staying silent
    /// when the target is missing (LESSON-004).</summary>
    public static class QualitySettingsResolution
    {
        public static (string SettingName, QualityDisplayDiagnostic Diagnostic) Resolve(IXrSettingsAccessor accessor)
        {
            if (accessor == null || !accessor.TryResolve(out var settingName))
            {
                return (string.Empty, new QualityDisplayDiagnostic(QualityUnityFailure.SettingsUnresolved, "xr_settings_accessor", "No live XRSettings-style write target resolved through the injected accessor."));
            }
            return (settingName, null);
        }
    }
}
