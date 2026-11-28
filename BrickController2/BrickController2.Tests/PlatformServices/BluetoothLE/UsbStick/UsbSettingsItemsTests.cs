// Tests of the USB stick rows of the settings page (platform settings hook): status texts, commands,
// event subscription and the resource files.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using BrickController2.PlatformServices.BluetoothLE.UsbStick.Settings;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Services.Settings;
using BrickController2.UI.Services.Translation;
using FluentAssertions;
using Moq;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Unit tests of <see cref="UsbAdvertiserSettingsItem"/> and <see cref="UsbGattSettingsItem"/> with mocked
/// services and a fake string lookup that returns the key (plus the extra text).
/// </summary>
public class UsbSettingsItemsTests
{
    /// <summary>
    /// Fake texts: the key itself, with " extra" appended for the two-argument overload.
    /// </summary>
    private sealed class KeyStrings : IUsbStickStrings
    {
        public string Get(string key) => key;
        public string Get(string key, string extra) => key + " " + extra;
    }

    /// <summary>
    /// Mocks plus the two items under test, created fresh per test.
    /// </summary>
    private sealed class Rig
    {
        public Mock<IUsbAdvertiserService> Advertiser { get; } = new();
        public Mock<IUsbGattService> Gatt { get; } = new();
        public Mock<IDialogService> Dialog { get; } = new();
        public UsbAdvertiserSettingsItem AdvertiserItem { get; }
        public UsbGattSettingsItem GattItem { get; }

        /// <summary>
        /// Token handed to the last selection dialog.
        /// </summary>
        public CancellationToken LastDialogToken { get; private set; }

        public Rig()
        {
            Advertiser.SetupProperty(a => a.Mode, UsbAdvertiserMode.Auto);
            Advertiser.SetupGet(a => a.Status).Returns(new UsbStickStatus(UsbStickState.NotFound, null, null));
            Advertiser.Setup(a => a.RefreshAsync()).Returns(Task.CompletedTask);
            Gatt.SetupProperty(g => g.GattMode, UsbGattMode.Auto);
            Gatt.SetupGet(g => g.GattStatus).Returns(new UsbGattStatus(false, 0, 0, false));

            var translation = new Mock<ITranslationService>();
            translation.Setup(t => t.Translate(It.IsAny<string>())).Returns<string>(k => k);

            AdvertiserItem = new UsbAdvertiserSettingsItem(Advertiser.Object, Gatt.Object, Dialog.Object, translation.Object, new KeyStrings(), a => a());
            GattItem = new UsbGattSettingsItem(Advertiser.Object, Gatt.Object, Dialog.Object, translation.Object, new KeyStrings(), a => a());
        }

        /// <summary>
        /// Makes the selection dialog answer with <paramref name="selected"/> (null = cancelled).
        /// </summary>
        public void DialogReturns(string? selected) =>
            Dialog.Setup(d => d.ShowSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<string>, string, string, CancellationToken>((_, _, _, token) => LastDialogToken = token)
                .ReturnsAsync(new SelectionDialogResult<string>(selected != null, selected ?? string.Empty));

        public void SetStatus(UsbStickState state, string? port = null, string? fw = null) =>
            Advertiser.SetupGet(a => a.Status).Returns(new UsbStickStatus(state, port, fw));
    }

    [Fact]
    public void Titles_AndValues_FollowStringsAndModes()
    {
        var rig = new Rig();
        rig.Advertiser.Object.Mode = UsbAdvertiserMode.Off;
        rig.Gatt.Object.GattMode = UsbGattMode.StickOnly;

        rig.AdvertiserItem.Title.Should().Be("UsbAdvertiser");
        rig.AdvertiserItem.Value.Should().Be("Off");
        rig.GattItem.Title.Should().Be("UsbGatt");
        rig.GattItem.Value.Should().Be("StickOnly");
    }

    [Fact]
    public void AdvertiserStatus_ModeOff_ShowsDisabledRegardlessOfState()
    {
        var rig = new Rig();
        rig.Advertiser.Object.Mode = UsbAdvertiserMode.Off;
        rig.SetStatus(UsbStickState.Connected, "COM7", "0.1.0");

        rig.AdvertiserItem.Status.Should().Be("UsbStickDisabled");
    }

    [Theory]
    [InlineData(UsbStickState.NotFound, "UsbStickNotFound")]
    [InlineData(UsbStickState.Lost, "UsbStickLost")]
    [InlineData(UsbStickState.Incompatible, "UsbStickIncompatible")]
    [InlineData(UsbStickState.Connected, "UsbStickConnected 0.1.0, COM7")]
    [InlineData(UsbStickState.Idle, "UsbStickConnected 0.1.0, COM7")]
    public void AdvertiserStatus_ModeAuto_ShowsStateText(UsbStickState state, string expected)
    {
        var rig = new Rig();
        rig.SetStatus(state, "COM7", "0.1.0");

        rig.AdvertiserItem.Status.Should().Be(expected);
    }

    [Fact]
    public void AdvertiserStatus_OmitsMissingDetails()
    {
        var rig = new Rig();
        rig.SetStatus(UsbStickState.Connected, null, "0.1.0");

        rig.AdvertiserItem.Status.Should().Be("UsbStickConnected 0.1.0");
    }

    [Fact]
    public void GattStatus_WindowsOnly_IsEmpty()
    {
        var rig = new Rig();
        rig.Gatt.Object.GattMode = UsbGattMode.WindowsOnly;
        rig.Gatt.SetupGet(g => g.GattStatus).Returns(new UsbGattStatus(true, 9, 2, true));

        rig.GattItem.Status.Should().BeEmpty();
    }

    [Theory]
    [InlineData(UsbStickState.Connected, "UsbStickNoGatt")]
    [InlineData(UsbStickState.NotFound, "")]
    [InlineData(UsbStickState.Lost, "")]
    public void GattStatus_Unsupported_MentionsMissingGattOnlyWithConnectedStick(UsbStickState state, string expected)
    {
        var rig = new Rig();
        rig.SetStatus(state);

        rig.GattItem.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, "UsbStickLinks 2/9")]
    [InlineData(true, "UsbStickLinks 2/9, UsbStickScanning")]
    public void GattStatus_Supported_ShowsLinksAndScanning(bool scanning, string expected)
    {
        var rig = new Rig();
        rig.Gatt.SetupGet(g => g.GattStatus).Returns(new UsbGattStatus(true, 9, 2, scanning));

        rig.GattItem.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData("Off", UsbAdvertiserMode.Off, 1)]   // Attach refreshes once; Off does not refresh again
    [InlineData("Auto", UsbAdvertiserMode.Auto, 2)] // Auto refreshes the stick state again
    public async Task AdvertiserCommand_AppliesTheSelectedMode(string selected, UsbAdvertiserMode expected, int refreshCalls)
    {
        var rig = new Rig();
        rig.DialogReturns(selected);
        rig.Advertiser.Object.Mode = selected == "Off" ? UsbAdvertiserMode.Auto : UsbAdvertiserMode.Off;
        rig.AdvertiserItem.Attach();

        await ExecuteAsync(rig.AdvertiserItem);

        rig.Advertiser.Object.Mode.Should().Be(expected);
        rig.Advertiser.Verify(a => a.RefreshAsync(), Times.Exactly(refreshCalls));
    }

    [Fact]
    public async Task AdvertiserCommand_Cancelled_KeepsTheMode()
    {
        var rig = new Rig();
        rig.DialogReturns(null);
        rig.AdvertiserItem.Attach();

        await ExecuteAsync(rig.AdvertiserItem);

        rig.Advertiser.Object.Mode.Should().Be(UsbAdvertiserMode.Auto);
    }

    [Theory]
    [InlineData("WindowsOnly", UsbGattMode.WindowsOnly, 0)]
    [InlineData("StickOnly", UsbGattMode.StickOnly, 1)]
    [InlineData("Auto", UsbGattMode.Auto, 1)]
    public async Task GattCommand_AppliesTheSelectedMode_AndRefreshesUnlessWindowsOnly(string selected, UsbGattMode expected, int refreshCalls)
    {
        var rig = new Rig();
        rig.DialogReturns(selected);
        rig.Gatt.Object.GattMode = UsbGattMode.StickOnly == expected ? UsbGattMode.Auto : UsbGattMode.StickOnly;
        rig.GattItem.Attach();   // the GATT row does not refresh on Attach

        await ExecuteAsync(rig.GattItem);

        rig.Gatt.Object.GattMode.Should().Be(expected);
        rig.Advertiser.Verify(a => a.RefreshAsync(), Times.Exactly(refreshCalls));
    }

    [Fact]
    public void Attach_SubscribesBothServices_AndStatusChangeRaisesAllProperties()
    {
        var rig = new Rig();
        var changed = new List<string?>();
        rig.GattItem.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        rig.GattItem.Attach();

        rig.Advertiser.Raise(a => a.StatusChanged += null, EventArgs.Empty);
        changed.Should().Equal("Title", "Value", "Status");

        changed.Clear();
        rig.Gatt.Raise(g => g.StatusChanged += null, EventArgs.Empty);
        changed.Should().Equal("Title", "Value", "Status");
    }

    [Fact]
    public async Task Detach_Unsubscribes_AndCancelsTheDialogToken()
    {
        var rig = new Rig();
        rig.DialogReturns("Off");
        var changed = 0;
        rig.AdvertiserItem.PropertyChanged += (_, _) => changed++;
        rig.AdvertiserItem.Attach();
        await ExecuteAsync(rig.AdvertiserItem);   // remember the token used by the dialog
        var token = rig.LastDialogToken;
        token.IsCancellationRequested.Should().BeFalse();

        rig.AdvertiserItem.Detach();

        token.IsCancellationRequested.Should().BeTrue();
        rig.Advertiser.Raise(a => a.StatusChanged += null, EventArgs.Empty);
        rig.Gatt.Raise(g => g.StatusChanged += null, EventArgs.Empty);
        changed.Should().Be(0);
    }

    [Fact]
    public void AdvertiserAttach_RefreshesTheStick_GattAttachDoesNot()
    {
        var rig = new Rig();

        rig.GattItem.Attach();
        rig.Advertiser.Verify(a => a.RefreshAsync(), Times.Never);

        rig.AdvertiserItem.Attach();
        rig.Advertiser.Verify(a => a.RefreshAsync(), Times.Once);
    }

    [Fact]
    public void ResourceFiles_EnglishAndGerman_HaveTheSameNonEmptyKeys()
    {
        var english = ReadResx("UsbStickStrings.resx");
        var german = ReadResx("UsbStickStrings.de.resx");

        english.Keys.Should().BeEquivalentTo(german.Keys);
        english.Keys.Should().BeEquivalentTo(new[]
        {
            "UsbAdvertiser", "UsbStickDisabled", "UsbStickConnected", "UsbStickNotFound", "UsbStickLost",
            "UsbStickIncompatible", "UsbGatt", "UsbStickLinks", "UsbStickScanning", "UsbStickNoGatt",
        });
        english.Values.Concat(german.Values).Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v));
    }

    /// <summary>
    /// Executes the item's command (SafeCommand.Execute is async void) and waits until the command is executable
    /// again, i.e. until the handler finished.
    /// </summary>
    private static async Task ExecuteAsync(ISettingsItem item)
    {
        item.Command.Execute(null);
        for (var i = 0; i < 200 && !item.Command.CanExecute(null); i++)
        {
            await Task.Delay(10);
        }

        item.Command.CanExecute(null).Should().BeTrue("the command handler must have finished");
    }

    /// <summary>
    /// Reads name/value pairs of a resx file copied next to the test assembly.
    /// </summary>
    private static Dictionary<string, string> ReadResx(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "UsbStickLinked", "Resources", fileName);
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);
    }
}
