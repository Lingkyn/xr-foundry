namespace Lingkyn.PlatformServices.Unity
{
    // An explicit connectivity signal the runtime consults once per write call, never a value it
    // polls or infers internally on a per-frame loop. A real implementation observes the platform's
    // own online/offline callback or reachability check; a fixed fake reports whatever a test wires
    // it to.

    public interface IPlatformServicesConnectivitySignal
    {
        bool IsOnline { get; }
    }

    public sealed class FixedConnectivitySignal : IPlatformServicesConnectivitySignal
    {
        public FixedConnectivitySignal(bool isOnline)
        {
            IsOnline = isOnline;
        }

        public bool IsOnline { get; }
    }
}
