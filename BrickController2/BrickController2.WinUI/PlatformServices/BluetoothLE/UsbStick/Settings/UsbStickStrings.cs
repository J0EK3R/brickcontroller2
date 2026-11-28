// Resource based implementation of IUsbStickStrings (resx files in BrickController2.WinUI/Resources).
using System.Resources;
using BrickController2.PlatformServices.Localization;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick.Settings;

/// <summary>
/// Reads the USB stick texts from <c>Resources/UsbStickStrings(.de).resx</c>. The culture comes from the same
/// platform <see cref="ILocalizationService"/> as the shared translations, so the stick texts follow the app
/// language selected in the settings. Languages without a resource file (e.g. Hungarian) fall back to English.
/// </summary>
public sealed class UsbStickStrings(ILocalizationService localization) : IUsbStickStrings
{
    /// <summary>
    /// Manifest resource base name: RootNamespace (BrickController2.Windows) + folder + file name.
    /// </summary>
    private const string ResourceBaseName = "BrickController2.Windows.Resources.UsbStickStrings";

    private static readonly ResourceManager Resources = new(ResourceBaseName, typeof(UsbStickStrings).Assembly);

    /// <inheritdoc />
    public string Get(string key) =>
        Resources.GetString(key, localization.CurrentCultureInfo) ?? key;   // culture is read on every call

    /// <inheritdoc />
    /// <remarks>Same combination rule as the shared TranslationServiceExtensions.Translate(key, extra).</remarks>
    public string Get(string key, string extra) => Get(key) + " " + extra;
}
