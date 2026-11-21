// Pure decision helper: "is Bluetooth usable for BC2?" when a USB advertising stick may stand in for
// the OS Bluetooth radio. Kept free of platform types so it can be unit tested on every platform.

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Combines the OS radio state, the stick state and the user setting into the single answer the UI needs
/// ("do we have to ask the user to enable Bluetooth?").
/// </summary>
public static class BluetoothAvailability
{
    /// <summary>
    /// Returns true when no "please enable Bluetooth" prompt is needed.
    /// The OS radio being on always suffices. With the radio off the stick suffices only if the user
    /// setting is Auto (setting Off keeps the behaviour from before the stick existed).
    /// </summary>
    /// <param name="radioOn">The OS Bluetooth radio is available and on.</param>
    /// <param name="stickAvailable">A stick was detected and is connectable (HELLO probe succeeded).</param>
    /// <param name="mode">The user setting for the USB advertiser.</param>
    public static bool IsBluetoothUsable(bool radioOn, bool stickAvailable, UsbAdvertiserMode mode)
        => radioOn || (mode == UsbAdvertiserMode.Auto && stickAvailable);
}
