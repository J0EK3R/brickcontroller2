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
        => IsBluetoothUsable(radioOn, stickAvailable, mode, UsbGattMode.WindowsOnly);

    /// <summary>
    /// Extended decision that also covers scanning and GATT through the stick (task 010).
    /// <list type="bullet">
    /// <item><b>StickOnly</b>: usable exactly when the stick is available (the radio is irrelevant).</item>
    /// <item>Otherwise: the radio being on suffices; with the radio off the stick suffices when it may be used,
    /// i.e. the advertiser setting is Auto (advertising) or the GATT setting is Auto (scan and GATT).</item>
    /// </list>
    /// With <c>gattMode = WindowsOnly</c> this is the original three argument behaviour.
    /// </summary>
    /// <param name="radioOn">The OS Bluetooth radio is available and on.</param>
    /// <param name="stickAvailable">A stick was detected and is connectable; for StickOnly it must be GATT capable.</param>
    /// <param name="advertiserMode">The user setting for the USB advertiser.</param>
    /// <param name="gattMode">The user setting for scanning and GATT devices.</param>
    public static bool IsBluetoothUsable(bool radioOn, bool stickAvailable, UsbAdvertiserMode advertiserMode, UsbGattMode gattMode)
        => gattMode == UsbGattMode.StickOnly
            ? stickAvailable
            : radioOn || (stickAvailable && (advertiserMode == UsbAdvertiserMode.Auto || gattMode == UsbGattMode.Auto));
}
