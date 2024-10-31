using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BrickController2.PlatformServices.GameController
{
    public class GameControllerEventArgs : EventArgs
    {
        public readonly string ControllerDeviceId;

        public GameControllerEventArgs(string controllerDeviceId, GameControllerEventType eventType, string eventCode, float value)
        {
            this.ControllerDeviceId = controllerDeviceId;
            var events = new Dictionary<(GameControllerEventType, string), float>();
            events[(eventType, eventCode)] = value;
            ControllerEvents = events;
        }

        public GameControllerEventArgs(string controllerDeviceId, IDictionary<(GameControllerEventType, string), float> events)
        {
            this.ControllerDeviceId = controllerDeviceId;
            ControllerEvents = new ReadOnlyDictionary<(GameControllerEventType, string), float>(events);
        }

        public IReadOnlyDictionary<(GameControllerEventType EventType, string EventCode), float> ControllerEvents { get; }
    }
}
