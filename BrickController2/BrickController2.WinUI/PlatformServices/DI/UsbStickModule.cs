// Autofac registrations of the USB stick feature (advertising, GATT/scan, settings rows, texts).
// Kept in its own module so the shared PlatformServicesModule stays identical to upstream.
using System;
using Autofac;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using BrickController2.PlatformServices.BluetoothLE.UsbStick.Settings;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Services.PlatformSettings;
using BrickController2.UI.Services.Translation;
using BrickController2.Windows.PlatformServices.BluetoothLE.UsbStick;
using Microsoft.Maui.ApplicationModel;

namespace BrickController2.Windows.PlatformServices.DI;

/// <summary>
/// Registers the USB stick services and decorates the Windows <see cref="IBluetoothLEService"/> with
/// <see cref="GattAwareBleService"/>, which redirects advertiser devices and (depending on the setting)
/// scanning/GATT devices to the stick and falls back to the Windows Bluetooth service otherwise.
/// </summary>
public class UsbStickModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RegistryStickLocator>().As<IStickLocator>().SingleInstance();
        builder.RegisterType<UsbStickConnection>().AsSelf().SingleInstance();
        // Presence poll (registry read once per second while a settings row watches or the stick is lost). The enabled
        // check resolves the mode services lazily because UsbAdvertiserService itself receives the poller.
        builder.Register(c =>
            {
                var advertiser = c.Resolve<Lazy<IUsbAdvertiserService>>();
                var gatt = c.Resolve<Lazy<IUsbGattService>>();
                return new UsbStickPresencePoller(
                    c.Resolve<UsbStickConnection>(),
                    () => advertiser.Value.Mode != UsbAdvertiserMode.Off || gatt.Value.GattMode != UsbGattMode.WindowsOnly);
            })
            .AsSelf()
            .SingleInstance();
        builder.RegisterType<UsbAdvertiserService>().AsSelf().As<IUsbAdvertiserService>().SingleInstance();
        builder.RegisterType<StickAddressBook>().AsSelf().SingleInstance();
        builder.RegisterType<UsbGattService>().AsSelf().As<IUsbGattService>().SingleInstance();

        // Decorator: resolved at resolve time, so the registration order of the modules does not matter and the
        // decorated BleService keeps its SingleInstance lifetime. The inner IBluetoothLEService is the Windows BleService.
        builder.RegisterDecorator<GattAwareBleService, IBluetoothLEService>();

        // USB arrival watcher: probes the stick right after it is re-plugged (nothing else reacts to the USB arrival).
        // AutoActivate creates the singleton when the container is built; the container disposes it (stops the watcher).
        builder.RegisterType<WinRtUsbArrivalWatcher>().As<IUsbArrivalWatcher>().SingleInstance();
        builder.Register(c =>
            {
                var advertiser = c.Resolve<IUsbAdvertiserService>();
                var gatt = c.Resolve<IUsbGattService>();
                var connection = c.Resolve<UsbStickConnection>();
                return new UsbArrivalProbe(
                    c.Resolve<IUsbArrivalWatcher>(),
                    // The stick is only used when the advertiser is not Off or GATT is not restricted to Windows.
                    () => advertiser.Mode != UsbAdvertiserMode.Off || gatt.GattMode != UsbGattMode.WindowsOnly,
                    () => connection.IsConnected,
                    connection.ProbeAfterArrivalAsync,
                    onArrived: connection.OnUsbDeviceArrived,
                    onRemoved: connection.OnUsbDeviceRemoved);
            })
            .AsSelf()
            .SingleInstance()
            .AutoActivate();

        builder.RegisterType<UsbStickStrings>().As<IUsbStickStrings>().SingleInstance();

        // The two rows of the settings page (instance per dependency = one pair per settings page). Autofac delivers
        // IEnumerable<IPlatformSettingsItem> in registration order: advertiser first, GATT second.
        // The stick events come from background threads, so notifications are dispatched to the UI thread.
        builder.Register(c => new UsbAdvertiserSettingsItem(
                c.Resolve<IUsbAdvertiserService>(), c.Resolve<IUsbGattService>(), c.Resolve<IDialogService>(),
                c.Resolve<ITranslationService>(), c.Resolve<IUsbStickStrings>(), MainThread.BeginInvokeOnMainThread))
            .As<IPlatformSettingsItem>()
            .InstancePerDependency();
        builder.Register(c => new UsbGattSettingsItem(
                c.Resolve<IUsbAdvertiserService>(), c.Resolve<IUsbGattService>(), c.Resolve<IDialogService>(),
                c.Resolve<ITranslationService>(), c.Resolve<IUsbStickStrings>(), MainThread.BeginInvokeOnMainThread))
            .As<IPlatformSettingsItem>()
            .InstancePerDependency();
    }
}
