// Abstractions of a platform BLE advertising publisher and its factory.
// Implemented by WinUI (WinRT BluetoothLEAdvertisementPublisher) and by fakes in unit tests.

using System;

namespace BrickController2.PlatformServices.BluetoothLE
{
    /// <summary>
    /// A single platform advertising publisher (one advertising instance on the controller).
    /// </summary>
    public interface IAdvertisingPublisher : IDisposable
    {
        /// <summary>Current status of the publisher.</summary>
        AdvertisingPublisherStatus Status { get; }

        /// <summary>Raised when <see cref="Status"/> changes. May be raised on any thread.</summary>
        event Action<AdvertisingPublisherStatus>? StatusChanged;

        /// <summary>Requests the publisher to go on air (asynchronous inside the stack).</summary>
        void Start();

        /// <summary>Requests the publisher to go off air (asynchronous inside the stack).</summary>
        void Stop();
    }

    /// <summary>
    /// Creates publishers and optionally updates their payload in place.
    /// </summary>
    public interface IAdvertisingPublisherFactory
    {
        /// <summary>Creates a new, not yet started publisher advertising the given manufacturer data.</summary>
        IAdvertisingPublisher Create(ushort manufacturerId, byte[] rawData, AdvertisingInterval interval, TxPowerLevel power);

        /// <summary>
        /// Tries to replace the payload of an existing (possibly running) publisher.
        /// Returns false if the platform rejects the modification.
        /// </summary>
        bool TryUpdateInPlace(IAdvertisingPublisher publisher, ushort manufacturerId, byte[] rawData);
    }
}
