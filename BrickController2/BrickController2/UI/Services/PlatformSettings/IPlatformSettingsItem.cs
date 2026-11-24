// Hook that lets a platform project add its own rows to the shared settings page
// without the shared project knowing anything about the feature behind it.
using System.ComponentModel;
using System.Windows.Input;

namespace BrickController2.UI.Services.PlatformSettings;

/// <summary>
/// One extra row of the settings page contributed by a platform project.
/// Platforms register implementations in Autofac; platforms that register none show nothing extra.
/// </summary>
public interface IPlatformSettingsItem : INotifyPropertyChanged
{
    /// <summary>
    /// Label shown in the left column.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Text of the picker button shown in the right column.
    /// </summary>
    string Value { get; }

    /// <summary>
    /// Small status line below the row; may be empty.
    /// </summary>
    string Status { get; }

    /// <summary>
    /// Executed when the button is pressed (typically opens a selection dialog).
    /// </summary>
    ICommand Command { get; }

    /// <summary>
    /// Called when the settings page appeared: subscribe to platform events and refresh.
    /// </summary>
    void Attach();

    /// <summary>
    /// Called when the settings page disappeared: unsubscribe and cancel open dialogs.
    /// </summary>
    void Detach();
}
