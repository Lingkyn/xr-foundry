namespace Lingkyn.XrUiShell.Core
{
    // The generic panel-content surface seam: the one thing a client family adapts to in order to
    // add its own content inside a shell panel. The shell (Core and both renderer adapters) never
    // references any client family's slot type or any client family's assembly; a client family
    // implements this interface against its own slot type instead, in its own package, exactly
    // like the verb-registration entry point on VerbRegistryBuilder (VerbRegistry.cs) it uses
    // alongside this seam. The shell knows nothing about what a TSlot is beyond that it can be
    // added and cleared: it never inspects, resolves, or dispatches on a slot's contents.

    /// <summary>One generic content surface a shell panel exposes to exactly one client family's
    /// slot type <typeparamref name="TSlot"/>. <see cref="Panel"/> names which declared panel the
    /// slots belong to; the shell resolves that panel's identity, anchor, open, close, focus,
    /// dock, and follow state, and a client implementing this interface never needs to read any
    /// of that itself.</summary>
    public interface IShellPanelContent<TSlot>
    {
        SurfaceId Panel { get; }

        /// <summary>Adds one typed content slot. A client family defines what a slot is; the
        /// shell never inspects it.</summary>
        void AddSlot(TSlot slot);

        /// <summary>Removes every slot this surface currently holds.</summary>
        void RemoveAllSlots();
    }
}
