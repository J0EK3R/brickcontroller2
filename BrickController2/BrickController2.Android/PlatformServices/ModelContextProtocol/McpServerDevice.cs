using BrickController2.PlatformServices.InputDevice;
using BrickController2.PlatformServices.InputDeviceService;
using BrickController2.PlatformServices.ModelContextProtocol;
using System.Collections.Generic;
using System.Linq;

namespace BrickController2.Droid.PlatformServices.ModelContextProtocol
{
    internal class McpServerDevice : InputDeviceBase<McpServer>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="service">reference to GameControllerService</param>
        /// <param name="mcpServer">reference to InputDevice</param>
        public McpServerDevice(IInputDeviceEventServiceInternal service, McpServer mcpServer)
            : base(service, mcpServer)
        {
            InputDeviceDevice.ChannelStatesUpdated += mcpServer_ChannelStatesUpdated;

            // initialize properties
            Name = "McpServer";
        }

        private void mcpServer_ChannelStatesUpdated(List<McpServer.ChannelValue> obj)
        {
            // grab all changed axis event
            var currentEvents = obj
                .Where(x => HasValueChanged($"{x.Channel}", (float)x.Value))
                .ToDictionary(x => (InputDeviceEventType.Axis, $"{x.Channel}"), x => (float)x.Value);

            RaiseEvent(currentEvents);
        }
    }
}