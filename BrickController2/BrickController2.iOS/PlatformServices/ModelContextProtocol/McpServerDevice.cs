using BrickController2.iOS.PlatformServices.GameController;
using BrickController2.PlatformServices.InputDevice;
using BrickController2.PlatformServices.InputDeviceService;
using BrickController2.PlatformServices.ModelContextProtocol;
using System.Collections.Generic;
using System.Linq;

namespace BrickController2.iOS.PlatformServices.ModelContextProtocol;
internal class McpServerDevice : InputDeviceBase<McpServer>
{
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="service">reference to GameControllerService</param>
    /// <param name="_mcpServer">reference to UWP's Gamepad</param>
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
