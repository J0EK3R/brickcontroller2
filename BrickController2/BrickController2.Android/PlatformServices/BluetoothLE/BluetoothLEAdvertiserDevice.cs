using System;
using System.Threading.Tasks;
using System.Threading;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using BrickController2.PlatformServices.BluetoothLE;
using Android.Runtime;

namespace BrickController2.Droid.PlatformServices.BluetoothLE
{
  internal class BluetoothLEAdvertiserDevice : AdvertisingSetCallback,
      IBluetoothLEAdvertiserDevice
  {
    private readonly BluetoothLeAdvertiser _advertiser;
    private AdvertisingSet _advertisingSet;

    public BluetoothLEAdvertiserDevice(BluetoothLeAdvertiser advertiser)
    {
      this._advertiser = advertiser;
    }

    public async Task<bool> StartAdvertiseAsync(AdvertisingInterval advertisingIterval, TxPowerLevel txPowerLevel, ushort manufacturerId, byte[] rawData)
    {
      #region convert advertisingIterval
      int advertisingIterval_value = AdvertisingSetParameters.IntervalMax;
      switch (advertisingIterval)
      {
        case AdvertisingInterval.Min:
          advertisingIterval_value = AdvertisingSetParameters.IntervalMin;
          break;
        case AdvertisingInterval.High:
          advertisingIterval_value = AdvertisingSetParameters.IntervalHigh;
          break;
        case AdvertisingInterval.Medium:
          advertisingIterval_value = AdvertisingSetParameters.IntervalMedium;
          break;
        case AdvertisingInterval.Low:
          advertisingIterval_value = AdvertisingSetParameters.IntervalLow;
          break;
        case AdvertisingInterval.Max:
        default:
          advertisingIterval_value = AdvertisingSetParameters.IntervalMax;
          break;
      }
      #endregion
      #region convert txPowerLevel
      AdvertiseTxPower advertiseTxPower;
      switch (txPowerLevel)
      {
        case TxPowerLevel.UltraLow:
          advertiseTxPower = AdvertiseTxPower.UltraLow;
          break;
        case TxPowerLevel.Low:
          advertiseTxPower = AdvertiseTxPower.Low;
          break;
        case TxPowerLevel.Medium:
          advertiseTxPower = AdvertiseTxPower.Medium;
          break;
        case TxPowerLevel.High:
          advertiseTxPower = AdvertiseTxPower.High;
          break;
        case TxPowerLevel.Max:
          advertiseTxPower = AdvertiseTxPower.Max;
          break;
        case TxPowerLevel.Min:
        default:
          advertiseTxPower = AdvertiseTxPower.Min;
          break;
      }
      #endregion

      AdvertisingSetParameters settings = new AdvertisingSetParameters.Builder()
          .SetLegacyMode(true)
          .SetConnectable(true)
          .SetScannable(true)
          .SetInterval(advertisingIterval_value)
          .SetTxPowerLevel(advertiseTxPower)
          .Build();

      AdvertiseData data = new AdvertiseData.Builder()
          .AddManufacturerData(manufacturerId, rawData)
          .Build();

      this._advertiser.StartAdvertisingSet(
          settings,
          data,
          null,
          null,
          null,
          this);

      return await Task.FromResult(true);
    }

    public async Task<bool> StopAdvertiseAsync()
    {
      this._advertiser.StopAdvertisingSet(this);


      return await Task.FromResult(true);
    }

    public bool ChangeAdvertiseAsync(ushort manufacturerId, byte[] rawData)
    {
      if (this._advertisingSet != null)
      {
        AdvertiseData data = new AdvertiseData.Builder()
            .AddManufacturerData(manufacturerId, rawData)
            .Build();

        this._advertisingSet.SetAdvertisingData(data);

        return true;
      }
      else
      {
        return false;
      }
    }

    public override void OnAdvertisingSetStarted(AdvertisingSet advertisingSet, int txPower, [GeneratedEnum] AdvertiseResult status)
    {
      base.OnAdvertisingSetStarted(advertisingSet, txPower, status);

      this._advertisingSet = advertisingSet;
    }
  }
}