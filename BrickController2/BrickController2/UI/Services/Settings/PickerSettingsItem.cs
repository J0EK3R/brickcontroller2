// Ready-made picker row: the owner supplies the displayed value and the press action as delegates.
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using BrickController2.UI.Commands;

namespace BrickController2.UI.Services.Settings;

/// <summary>
/// A picker style settings row whose value and press action are provided by delegates.
/// </summary>
public class PickerSettingsItem : ISettingsItem
{
    private readonly Func<string> _getValue;

    /// <summary>
    /// Creates the row.
    /// </summary>
    /// <param name="title">Already translated label.</param>
    /// <param name="getValue">Returns the text for the picker button.</param>
    /// <param name="onPressed">Executed when the button is pressed.</param>
    public PickerSettingsItem(string title, Func<string> getValue, Func<Task> onPressed)
    {
        Title = title;
        _getValue = getValue;
        Command = new SafeCommand(onPressed);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public string Value => _getValue();

    /// <inheritdoc />
    public string Status => string.Empty;

    /// <inheritdoc />
    public ICommand Command { get; }

    /// <inheritdoc />
    public void Attach()
    {
        // nothing to subscribe to
    }

    /// <inheritdoc />
    public void Detach()
    {
        // nothing to unsubscribe from
    }

    /// <summary>
    /// Tells the UI that the value text changed (call after the underlying setting was changed).
    /// </summary>
    public void RaiseValueChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
