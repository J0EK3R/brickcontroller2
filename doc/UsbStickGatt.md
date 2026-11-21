# Scanning and GATT devices through the USB stick (Windows)

Since protocol 1.1 of the `ble-usb-advertiser` firmware the USB stick (M5Stack AtomS3U) is not only a BLE
advertiser, it is also a BLE scanner and GATT central. BrickController2 for Windows can therefore scan for,
connect to and drive **all GATT devices** (LEGO hubs, DUPLO, WeDo 2.0, BuWizz, SBrick, CircuitCubes,
PFx Brick, MouldKing DIY, ...) through the stick, completely **without Windows Bluetooth**. Android and
iOS are not affected. The advertising part is described in [UsbAdvertiser.md](UsbAdvertiser.md).

## Setting

Settings page, row **"Bluetooth LE devices (GATT)"** (Windows only; independent of the "USB advertiser" row):

| Value | Scan and GATT devices use | Without stick |
|-------|---------------------------|---------------|
| `Auto` (default) | **the stick whenever it is plugged in** (also when Windows Bluetooth is on), otherwise Windows Bluetooth | Windows Bluetooth if it is on, otherwise nothing |
| `WindowsOnly` | Windows Bluetooth, the stick is never used for scanning or GATT | as above |
| `StickOnly` | only the stick | scanning and connecting fail cleanly |

The line below the picker shows `USB stick connections (used/max): N/MAX` (and "Scanning through the USB
stick"). `MAX` is the value the firmware reports in its HELLO_RSP (`max_conns`, measured by the firmware for
5 advertising sets plus a running scan); BC2 contains no connection limit of its own.

Rules in detail (`GattPathSelector`, unit tested for every combination):

* The path is decided **per scan and per device connect**. An existing connection is never moved.
* `Auto` with a stick whose connection slots are all in use: the next device **overflows to Windows
  Bluetooth** if it is on (so "as many connections as possible" is not capped by the stick); with the radio
  off or with `StickOnly` that device fails with the normal connection error.
* A device first seen by a Windows scan and connected after the stick was plugged in uses the stick (stick
  priority). The stick does not know the address type then (CONNECT with `addr_type` 0xFF = look it up in
  the stick's scan cache, public if unknown). That can fail for devices with a random address until a scan
  through the stick has seen them: scan once more.
* A device found by a stick scan keeps using the stick (the address type is remembered). If the stick was
  unplugged in the meantime, `Auto` falls back to Windows (if the radio is on).
* A stick with an old (1.0) firmware, without GATT support or with `max_conns` 0, counts as "not available"
  for scanning and GATT.

### "Enable Bluetooth" prompt

`BluetoothAvailability.IsBluetoothUsable(radioOn, stickAvailable, advertiserMode, gattMode)`:
with `StickOnly` BC2 is usable exactly when a GATT capable stick is available; otherwise the radio being
on is enough, and with the radio off the stick suffices when the advertiser setting is `Auto` (advertising)
or the GATT setting is `Auto` (scanning and GATT). With the radio off and a stick present BC2 therefore
does not ask to enable Bluetooth, and scanning and GATT really work through the stick.
`StickOnly` without a stick reports "Bluetooth not usable" (the normal message) and scanning returns an error;
there is no retry loop.

## How it works

* One stick connection (`UsbStickConnection`) serves advertising sets, GATT links and the scan. The serial port
  stays open while any set, link or scan exists and closes 5 s after the last one is gone.
* **Scan**: `StickScanner` starts an *active* scan on the stick (needed for names in scan responses) and merges
  advertising PDU and scan response per address. Like the Windows `BleScanner` it forwards only manufacturer
  data (0xFF), 128 bit service UUIDs (0x06/0x07) and the name, and adds an AD 0x09 entry from the cached
  name (BuWizz 3 requires it, DUPLO and WeDo 2.0 are only recognised with a name). The address type of every
  device is remembered for CONNECT (`StickAddressBook`). Several scanners share one stick scan.
* **Connect**: one connection slot (`conn_id`, `0 .. max_conns-1`) is reserved when BC2 asks for the device
  (`GetKnownDeviceAsync`). `autoConnect` (LEGO ControlPlus devices such as the DUPLO train hub) becomes a
  *background connect* on the stick; all other devices use a normal connect (10 s timeout of the stick).
  Cancelling while connecting sends DISCONNECT to the stick.
* **Discovery**: the stick reports the complete service tree (`DISCOVER`); characteristics are assigned to
  services by handle range, 16 bit UUIDs are expanded to the Bluetooth base UUID.
* **Operations**: `WriteAsync` = WRITE, `WriteNoResponseAsync` = WRITE_CMD, `ReadAsync` = READ,
  `Enable/DisableNotificationAsync` = SUBSCRIBE (mode 1 / 0, only for characteristics with the notify property).
  Data larger than `min(max_value_len, mtu - 3)` (BC2 never requests an MTU; the stick negotiates 64, WeDo 2.0
  stays at 23) is rejected locally without touching the wire.
* **No retransmission**: GATT requests are sent exactly once (a repeated write could be harmful). A missing
  answer is a failed operation (`false` / `null`), like on the Windows device. Only a WRITE_CMD answered with
  `BUSY` (not queued by the stick) is repeated a few times after 6 ms.
* **Notifications** arrive on the USB read thread and are handed to a per-link queue with one reader task:
  the read loop never blocks and the order per device is kept. The queue runs from the connection on, so
  notifications that follow the SUBSCRIBE ACK immediately (DUPLO attached-IO messages) are not lost.
* **Stick loss** (unplug, keep-alive loss, button): the stick drops all links itself. BC2 reports
  `onDeviceDisconnected` exactly once per connected device and the normal reconnect logic of BC2 applies.
  GATT links are **never resumed behind the device's back**. Advertising sets and a running scan are
  stateless and resume after the stick returns. After the button the session stays; nothing is restarted.
* **Keep-alive**: the PING (every 200 ms) is independent of traffic, so the stick drops every link within about
  1 s when BC2 crashes or hangs. BC2 does not use `SET_FAILSAFE` ("motors off" before the stick drops a link) yet:
  whether the test hubs stop by themselves is recorded in the hardware pass (H6/H7) and decides a later task.
  `StickGattLink.SetFailsafeAsync(slot, handle, data)` is implemented and tested for that follow-up.
* Code: `BrickController2/PlatformServices/BluetoothLE/UsbStick/` (shared project, unit tested on Linux):
  `StickGattLink`, `StickGattDevice`, `StickScanner`, `UsbGattService`, `GattAwareBleService` (the decorator
  with all decisions; the WinUI `StickAwareBleService` only derives from it), `GattPathSelector`.
  Protocol: `ble-usb-advertiser/docs/protocol.md` sections 13-19.

## Limitations

* Maximum simultaneous GATT devices = `max_conns` of the firmware (see the settings line); further devices
  use Windows Bluetooth in `Auto` if it is on.
* `WRITE` with response is limited by the connection interval (about 33-66 per second at 15-30 ms).
* After a replug a device must be reconnected by BC2 (no resume); right after a stick reset the stick may
  still refuse the same `conn_id` for a moment (its old link is torn down first): the connect fails and BC2's
  normal reconnect logic retries.
* Devices found with a random address need one scan through the stick before they can be connected through it
  (see above).
* Hardware tests are pending (see the checklist below); the firmware is not part of this repository.

## Manual test checklist (Windows, AtomS3U with firmware >= protocol 1.1)

Prerequisites: the stick is flashed with the GATT firmware (task 009), a WinUI build of the branch
`feature/usbgatt` (GitHub workflow `J0EK3R-build-windows-release.yml`), the test hubs LEGO DUPLO train hub,
MouldKing DIY, LEGO WeDo 2.0 and a MouldKing MK4.0 (advertising). Without any toy: a phone with nRF Connect as
GATT server can be used for H2-H3. Record the results in `tasks/010-bc2-usb-stick-gatt/hw-results.md`.

* [ ] **H1** Settings page shows the stick (firmware, COM port), the GATT picker (default `Auto`) and
  `USB stick connections (used/max): 0/MAX` with the real maximum of the firmware.
* [ ] **H2** Windows Bluetooth **off**: no "enable Bluetooth" prompt; the scan finds the DUPLO train hub (by its
  name), WeDo 2.0 and MouldKing DIY; the MK4.0 can be driven through an advertising set.
* [ ] **H3** Connect each hub alone and drive it from BC2:
  * DUPLO: background connect, battery and firmware shown, motor runs, attached-IO notifications arrive;
  * MouldKing DIY: all 4 channels at 100 writes/s;
  * WeDo 2.0: both motor ports, firmware revision, battery voltage notification;
  * disconnect from BC2 and by switching the hub off (BC2 shows the disconnect).
* [ ] **H4** All three hubs at once plus the MK4.0 through advertising at the same time. If more peripherals
  are available: fill all `MAX` slots; the next device uses Windows Bluetooth in `Auto` (radio on) and fails
  cleanly in `StickOnly`.
* [ ] **H5** Priority: Windows Bluetooth **on** and `Auto` -> the stick is used (stick LED, Windows shows no
  connection to the hub); unplug the stick -> `Auto` uses Windows; `WindowsOnly` uses Windows; `StickOnly`
  without stick -> clean error, no prompt loop.
* [ ] **H6** Unplug the stick while driving: BC2 shows a disconnect for every hub. **Observe and note for each hub
  (DUPLO, MK DIY, WeDo 2.0) whether it stops its motors by itself and after how many seconds** (feeds the
  failsafe decision). Replug: advertising resumes, hubs are reconnected by hand.
* [ ] **H7** Kill the BC2 process: hub links and MK advertising sets stop within about 1 s (same observation per hub).
* [ ] **H8** Press the button on the stick: everything stops, BC2 shows the disconnects, nothing restarts by itself.
* [ ] **H9** Write latency subjectively comparable to the Windows path (DUPLO steering response, MK DIY);
  10 minute soak with all hubs connected without drops.

## Automated tests

`dotnet test BrickController2/BrickController2.Tests/BrickController2.Tests.csproj` (no hardware needed):
simulated stick (`FakeStick*.cs`) with scriptable GATT peripherals and a fake clock. Covered: golden vectors
of the firmware (`FirmwareVectorTests`), every operation and failure, no retransmission, `conn_id` allocation for
`max_conns` 1/4/9/16, idle close rules, stick loss / button / replug, notification order and PING gaps under
load, scan merge for DUPLO / WeDo 2.0 / MK DIY / SBrick / BuWizz 3 / CircuitCubes with the real device
managers, the complete policy table, availability table and the decorator with a fake Windows service.
