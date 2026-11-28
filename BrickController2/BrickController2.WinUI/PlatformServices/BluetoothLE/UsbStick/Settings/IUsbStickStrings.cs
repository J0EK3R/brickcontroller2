// Lookup of the USB stick UI texts, which live in the WinUI project's own resource files.
namespace BrickController2.PlatformServices.BluetoothLE.UsbStick.Settings;

/// <summary>
/// Provides the translated texts of the USB stick settings rows (English neutral + German).
/// Kept as an interface so the settings items can be unit tested with a fake.
/// </summary>
public interface IUsbStickStrings
{
    /// <summary>
    /// Returns the text for <paramref name="key"/> in the current app language (the key itself when missing).
    /// </summary>
    string Get(string key);

    /// <summary>
    /// Returns the text for <paramref name="key"/> followed by a blank and <paramref name="extra"/>.
    /// </summary>
    string Get(string key, string extra);
}
