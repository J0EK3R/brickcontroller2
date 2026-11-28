# USB BLE advertising stick (Windows)

BrickController2 for Windows can send its Bluetooth advertising data (MouldKing, JieStar, PowerBox, CaDA)
through a small USB stick (M5Stack AtomS3U, firmware in the `ble-usb-advertiser` project) instead of the
Windows Bluetooth stack. The stick is found automatically; without a stick, or with the setting Off,
BC2 uses the normal Windows advertiser. Android and iOS are not affected. Scanning and GATT devices
(LEGO, BuWizz, ...) can also go through the stick with protocol 1.1 firmware, see
[UsbStickGatt.md](UsbStickGatt.md); with an old firmware or the setting `WindowsOnly` they use the Windows
Bluetooth stack.

## How it works

* Detection: the COM ports of all devices with USB id `303A:1001` (registry
  `HKLM\SYSTEM\CurrentControlSet\Enum\USB\VID_303A&PID_1001*`) are opened one after the other (DTR/RTS off) and
  probed with a HELLO frame. The port whose answer starts with the magic `BC2ADV` is kept, every other port is
  closed immediately.
* One advertising device in BC2 uses one advertising set on the stick (5 sets). The 6th device and further
  ones use the Windows advertiser.
* Keep-alive: BC2 sends a PING every 200 ms. If BC2 crashes or the port is closed, the stick stops advertising
  within 1 s (the toys stop).
* Unplug / replug: the sets are remembered and started again (with the latest data) when the stick is plugged
  in again, also on another COM port. There is no live switch to the Windows advertiser.
* Button on the stick: stops all sets on purpose; BC2 does NOT restart them automatically. Disconnect and
  reconnect the device in BC2 to start it again.
* The protocol is described in `ble-usb-advertiser/docs/protocol.md`; the code is in
  `BrickController2.WinUI/PlatformServices/BluetoothLE/UsbStick/` (WinUI project only, registered in
  `PlatformServices/DI/UsbStickModule.cs`; the platform neutral files are linked into `BrickController2.Tests`).
* Payload: the stick receives the Android layout (no 3 dummy bytes of the Windows publisher, at most 24 bytes,
  PowerBox 18 bytes). The WinUI MouldKing/JieStar/PowerBox services are unchanged; `StickPayloadNormalizer`
  strips the offset, recomputes the Android filler bytes and cuts the PowerBox filler.

## Settings

Settings page, row "USB advertiser" (Windows only): `Auto` (default) or `Off`. The line below shows the
state: connected (firmware, COM port) / not found / lost / incompatible / not used. The page probes for the
stick when it opens. Changing the setting affects devices created afterwards.
The row is contributed by the WinUI project through the generic `IPlatformSettingsItem` hook of the shared
settings page (`UsbStick/Settings/`); its texts are in `BrickController2.WinUI/Resources/UsbStickStrings(.de).resx`
(Hungarian shows the English texts).

## Test with the AtomS3U on Windows (manual, not yet verified on hardware)

1. Flash the stick (see `ble-usb-advertiser/README.md`, "Build and flash"; `COMx` on Windows).
2. Plug it in. In Device Manager a "USB Serial Device (COMx)" with hardware id `VID_303A&PID_1001` appears.
3. Build the WinUI app with the GitHub workflow `J0EK3R-build-windows-release.yml` and install the artifact.
4. Start BC2, open Settings: the USB advertiser row shows `Auto` and "USB stick connected: 0.1.0, COMx".
5. Open a device that advertises (MouldKing, JieStar, PowerBox or CaDA) and drive it. Expected:
   the stick LED shows ADVERTISING (cyan pulse), the toy reacts, the log contains
   `USB stick found on COMx ...` and `Advertising through USB stick set 0`.
6. Checklist:
   * [ ] toy reacts exactly like with the Windows advertiser / an Android phone (check all four vendors)
   * [ ] 5 devices at the same time work; the 6th still works through the Windows advertiser
   * [ ] unplug the stick while driving: toy stops, status "lost"; plug in again: driving resumes without
         restarting BC2 (also when the port number changes)
   * [ ] close or kill BC2: the stick goes silent within 1 s (LED TIMEOUT)
   * [ ] setting `Off`: new devices use the Windows advertiser (log: no `USB stick` lines, stick LED idle)
   * [ ] button on the stick stops the toy; reconnecting the device in BC2 starts it again
   * [ ] no stick plugged in: BC2 behaves exactly as before
7. Run the unit tests: `dotnet test BrickController2/BrickController2.Tests/BrickController2.Tests.csproj`
   (tests with a simulated stick and a fake clock, no hardware needed).

## Known limitations

* With the Windows radio off and a stick present (setting Auto) BC2 no longer asks to enable Bluetooth
  (`GattAwareBleService.IsBluetoothOnAsync` = radio on OR stick reachable, decided by
  `BluetoothAvailability`). With a stick with protocol 1.1 firmware and the GATT setting `Auto`, scanning and GATT
  devices work through the stick in this state ([UsbStickGatt.md](UsbStickGatt.md)). With a 1.0 firmware (or
  the GATT setting `WindowsOnly`) only advertising devices work: BLE scanning completes without results
  (advertising scan still runs) and GATT devices fail to connect with the normal connection error.
  With the advertiser setting Off (and GATT not `Auto`) the behaviour is unchanged (prompt when the radio is off).
* Hardware tests are pending (the stick was not available during development).
* Device creation is synchronous, so the stick probe waits at most 1 s. With two or more silent ESP32
  boards plugged in (each probed with 3 HELLOs of 200 ms) the wait can time out; that device is then
  created on the Windows advertiser although the probe may find the stick right afterwards. The same
  happens for the first device right after a replug within the 10 s negative probe cache, or while an
  idle stick is being woken up. Devices created later use the stick; open the settings page (forces a
  probe) before starting the toys to avoid this.
