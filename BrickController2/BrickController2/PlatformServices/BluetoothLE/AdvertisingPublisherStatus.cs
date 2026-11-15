// Platform independent status/strategy enums used by the advertising publisher coordinator.
// See AdvertisingPublisherCoordinator for the rationale (task 003-win-advertising).

namespace BrickController2.PlatformServices.BluetoothLE
{
    /// <summary>
    /// Lifecycle status of a BLE advertising publisher.
    /// Mirrors Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementPublisherStatus
    /// so that the state machine can be unit tested without WinRT.
    /// </summary>
    public enum AdvertisingPublisherStatus
    {
        /// <summary>Publisher object exists but was never started.</summary>
        Created,

        /// <summary>Start was requested, the stack is still preparing (e.g. radio off).</summary>
        Waiting,

        /// <summary>The publisher is on air.</summary>
        Started,

        /// <summary>Stop was requested, the stack is still tearing down.</summary>
        Stopping,

        /// <summary>The publisher is off air.</summary>
        Stopped,

        /// <summary>The publisher was aborted by the stack (error, resource in use, ...).</summary>
        Aborted,
    }

    /// <summary>
    /// Strategy to bring new advertising data on air.
    /// </summary>
    public enum AdvertisingUpdateStrategy
    {
        /// <summary>
        /// Modify the payload of the running publisher in place (fastest, but unverified on hardware:
        /// the platform may ignore the change). Falls back to <see cref="MakeBeforeBreak"/> when the
        /// in-place update is rejected.
        /// </summary>
        InPlace,

        /// <summary>
        /// Start a new publisher, wait until it is on air, then stop the old one (no gap on air).
        /// Falls back to <see cref="StopStartAwaited"/> if the new publisher cannot start.
        /// </summary>
        MakeBeforeBreak,

        /// <summary>
        /// Stop the old publisher, wait until it is stopped, then start the new one.
        /// Serialised, so at most one stop/start cycle is in flight.
        /// </summary>
        StopStartAwaited,
    }
}
