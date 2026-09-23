namespace Lingkyn.XrUiShell.Core
{
    // The closed set of anchor kinds a declared surface can admit, and the closed set of input
    // source kinds pointer and gaze routing accepts. Adding a member to either is a breaking
    // change; until then an adapter or intent that names a kind outside these sets fails closed.

    /// <summary>The closed set of places a declared surface can be placed. A general panel may
    /// admit any subset; a wrist menu admits only <see cref="Wrist"/> and a hand menu admits only
    /// <see cref="Hand"/>.</summary>
    public enum AnchorKind
    {
        World,
        HeadLocked,
        Wrist,
        Hand,
    }

    /// <summary>The closed set of registered input source kinds pointer and gaze routing accepts.</summary>
    public enum InputSourceKind
    {
        Ray,
        Poke,
        Gaze,
    }

    /// <summary>The closed set of declared surface kinds: a general panel, a wrist menu (admits
    /// only <see cref="AnchorKind.Wrist"/>), or a hand menu (admits only
    /// <see cref="AnchorKind.Hand"/>). Used only to key <see cref="SurfaceId"/>; it carries no
    /// visual or engine meaning of its own.</summary>
    public enum SurfaceKind
    {
        Panel,
        WristMenu,
        HandMenu,
    }
}
