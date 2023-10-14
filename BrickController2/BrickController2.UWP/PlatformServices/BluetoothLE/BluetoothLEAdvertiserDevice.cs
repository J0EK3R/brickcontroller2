using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.Windows.Extensions;
using Windows.ApplicationModel.Background;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.Background;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Notifications;
using Xamarin.Forms;

namespace BrickController2.Windows.PlatformServices.BluetoothLE
{
    internal class BluetoothLEAdvertiserDevice :
        IBluetoothLEAdvertiserDevice
    {
        private readonly BluetoothLEAdvertisementPublisherTrigger trigger;
        private readonly BluetoothLEAdvertisementPublisher publisher;


        private bool usePublisher = true;

        // The background task registration for the background advertisement publisher
        private IBackgroundTaskRegistration taskRegistration;

        // A name is given to the task in order for it to be identifiable across context.
        private string taskName = "Scenario4_BackgroundTask";

        private string taskEntryPoint = "BackgroundTasks.AdvertisementPublisherTask";


        private BluetoothLEManufacturerData bluetoothLEManufacturerData = new BluetoothLEManufacturerData();

        public BluetoothLEAdvertiserDevice()
        {
            this.trigger = new BluetoothLEAdvertisementPublisherTrigger();
            this.publisher = new BluetoothLEAdvertisementPublisher();
            this.publisher.StatusChanged += Publisher_StatusChanged;
        }

        public void Dispose()
        {
        }

        public async Task<bool> StartAdvertiseAsync(AdvertisingInterval advertisingIterval, TxPowerLevel txPowerLevel, ushort manufacturerId, byte[] rawData)
        {
            // Registering a background trigger if it is not already registered. It will start background advertising.
            // First get the existing tasks to see if we already registered for it
            if (taskRegistration != null)
            {
                return await Task.FromResult(false);
            }
            else
            {
                #region convert advertisingIterval
                //int advertisingIterval_value = AdvertisingSetParameters.IntervalMax;
                //switch (advertisingIterval)
                //{
                //    case AdvertisingInterval.Min:
                //        advertisingIterval_value = AdvertisingSetParameters.IntervalMin;
                //        break;
                //    case AdvertisingInterval.High:
                //        advertisingIterval_value = AdvertisingSetParameters.IntervalHigh;
                //        break;
                //    case AdvertisingInterval.Medium:
                //        advertisingIterval_value = AdvertisingSetParameters.IntervalMedium;
                //        break;
                //    case AdvertisingInterval.Low:
                //        advertisingIterval_value = AdvertisingSetParameters.IntervalLow;
                //        break;
                //    case AdvertisingInterval.Max:
                //    default:
                //        advertisingIterval_value = AdvertisingSetParameters.IntervalMax; 
                //        break;
                //}
                #endregion
                #region convert txPowerLevel
                //AdvertiseTxPower advertiseTxPower;
                //switch (txPowerLevel)
                //{
                //    case TxPowerLevel.UltraLow:
                //        advertiseTxPower = AdvertiseTxPower.UltraLow;
                //        break;
                //    case TxPowerLevel.Low:
                //        advertiseTxPower = AdvertiseTxPower.Low;
                //        break;
                //    case TxPowerLevel.Medium:
                //        advertiseTxPower = AdvertiseTxPower.Medium;
                //        break;
                //    case TxPowerLevel.High:
                //        advertiseTxPower = AdvertiseTxPower.High;
                //        break;
                //    case TxPowerLevel.Max:
                //        advertiseTxPower = AdvertiseTxPower.Max;
                //        break;
                //    case TxPowerLevel.Min:
                //    default:
                //        advertiseTxPower = AdvertiseTxPower.Min;
                //        break;
                //}
                #endregion

                this.CreateData(manufacturerId, rawData);

                if (this.usePublisher)
                {

                    if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 10))
                    {
                        //publisher.UseExtendedAdvertisement = true;
                        //publisher.IsAnonymous = true;
                        //publisher.IncludeTransmitPowerLevel = includeTxPower;
                        //publisher.PreferredTransmitPowerLevelInDBm = txPower;
                    }


                    //this.publisher.UseExtendedAdvertisement = true;
                    //this.publisher.IsAnonymous = true;

                    //this.publisher.Advertisement.Flags = BluetoothLEAdvertisementFlags.None;
                    //this.publisher.Advertisement.ManufacturerData.Add(this.bluetoothLEManufacturerData);

                    // From old code (Which is not commented)
                    var data = new BluetoothLEAdvertisementDataSection
                    { 
                        DataType = 0x02,
                        Data = new byte[] { 0x01, 0x02 }.AsBuffer()
                    };
                    this.publisher.Advertisement.DataSections.Add(data);

                    this.publisher.Start();
                }
                else
                {
                    //this.trigger.UseExtendedFormat = true;
                    //this.trigger.IncludeTransmitPowerLevel = true;
                    //this.trigger.IsAnonymous = true;
                    this.trigger.Advertisement.ManufacturerData.Add(this.bluetoothLEManufacturerData);


                    // Applications registering for background trigger must request for permission.
                    BackgroundAccessStatus backgroundAccessStatus = await BackgroundExecutionManager.RequestAccessAsync();
                    // Here, we do not fail the registration even if the access is not granted. Instead, we allow 
                    // the trigger to be registered and when the access is granted for the Application at a later time,
                    // the trigger will automatically start working again.

                    // At this point we assume we haven't found any existing tasks matching the one we want to register
                    // First, configure the task entry point, trigger and name
                    var builder = new BackgroundTaskBuilder();
                    builder.TaskEntryPoint = taskEntryPoint;
                    builder.SetTrigger(trigger);
                    builder.Name = taskName;

                    // Now perform the registration.
                    taskRegistration = builder.Register();

                    // For this scenario, attach an event handler to display the result processed from the background task
                    taskRegistration.Completed += OnBackgroundTaskCompleted;

                    // Even though the trigger is registered successfully, it might be blocked. Notify the user if that is the case.
                    if ((backgroundAccessStatus == BackgroundAccessStatus.AlwaysAllowed) || (backgroundAccessStatus == BackgroundAccessStatus.AllowedSubjectToSystemPolicy))
                    {
                        //rootPage.NotifyUser("Background publisher registered.", NotifyType.StatusMessage);
                    }
                    else
                    {
                        //rootPage.NotifyUser("Background tasks may be disabled for this app", NotifyType.ErrorMessage);
                    }
                }
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> StopAdvertiseAsync()
        {
            if (this.usePublisher)
            {
                this.publisher?.Advertisement?.ManufacturerData?.Remove(this.bluetoothLEManufacturerData);
                this.publisher.Stop();
            }
            else
            {
                this.trigger?.Advertisement?.ManufacturerData?.Remove(this.bluetoothLEManufacturerData);

                // Unregistering the background task will stop advertising if this is the only client requesting
                // First get the existing tasks to see if we already registered for it
                if (taskRegistration != null)
                {
                    taskRegistration.Unregister(true);
                    taskRegistration = null;
                }
                else
                {
                }
            }

            return await Task.FromResult(false);
        }

        public bool ChangeAdvertiseAsync(ushort manufacturerId, byte[] rawData)
        {
            if (this.bluetoothLEManufacturerData != null)
            {
                //this.publisher?.Advertisement?.ManufacturerData?.Remove(this.bluetoothLEManufacturerData);

                this.CreateData(manufacturerId, rawData);

                //this.publisher?.Advertisement?.ManufacturerData?.Add(this.bluetoothLEManufacturerData);

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Handle background task completion.
        /// </summary>
        /// <param name="task">The task that is reporting completion.</param>
        /// <param name="e">Arguments of the completion report.</param>
        private async void OnBackgroundTaskCompleted(BackgroundTaskRegistration task, BackgroundTaskCompletedEventArgs eventArgs)
        {
            // We get the status changed processed by the background task
            if (ApplicationData.Current.LocalSettings.Values.Keys.Contains(taskName))
            {
                string backgroundMessage = (string)ApplicationData.Current.LocalSettings.Values[taskName];
                // Serialize UI update to the main UI thread
                //await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                //{
                //    // Display the status change
                //    PublisherStatusBlock.Text = backgroundMessage;
                //});
            }
        }

        private void Publisher_StatusChanged(BluetoothLEAdvertisementPublisher sender, BluetoothLEAdvertisementPublisherStatusChangedEventArgs eventArgs)
        {
            if (eventArgs.Status == BluetoothLEAdvertisementPublisherStatus.Aborted)
            {
                Debug.WriteLine("Advertisement publisher aborted. Restarting.");
                //this.publisher.Start();
            }
        }

        private void CreateData(ushort manufacturerId, byte[] sourceArray)
        {
            //byte[] preinsertArray = new byte[]
            //{
            //    0x02,
            //    0x01,
            //    0x02
            //};

            //byte[] targetArray = new byte[preinsertArray.Length + sourceArray.Length];
            //preinsertArray.CopyTo(targetArray, 0);
            //sourceArray.CopyTo(targetArray, preinsertArray.Length);

            //byte[] dataArray = new byte[] 
            //{
            //    // last 2 bytes of Apple's iBeacon
            //    0x02, 0x15,
            //    // UUID e2 c5 6d b5 df fb 48 d2 b0 60 d0 f5 a7 10 96 e0
            //    0xe2, 0xc5, 0x6d, 0xb5,
            //    0xdf, 0xfb, 0x48, 0xd2,
            //    0xb0, 0x60, 0xd0, 0xf5,
            //    0xa7, 0x10, 0x96, 0xe0,
            //    // Major
            //    0x00, 0x00,
            //    // Minor
            //    0x00, 0x01,
            //    // TX power
            //    0xc5
            //};

            this.bluetoothLEManufacturerData.CompanyId = manufacturerId;
            this.bluetoothLEManufacturerData.Data = sourceArray.AsBuffer();
        }
    }
}
