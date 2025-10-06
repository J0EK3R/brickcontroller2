using BrickController2.InputDeviceManagement;
using BrickController2.PlatformServices.InputDeviceService;
using BrickController2.PlatformServices.ModelContextProtocol;
using BrickController2.UI.Services.Preferences;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Dispatching;
using System;
using System.Threading.Tasks;

namespace BrickController2.Windows.PlatformServices.ModelContextProtocol;

internal class McpServerService : InputDeviceServiceBase<McpServerDevice>, IMcpServerService, IDisposable
{
    private readonly object _Lock = new object();
    private readonly IPreferencesService _preferencesService;
    private readonly IDispatcher _dispatcher;
    private McpServer? _server;

    public McpServerService(IInputDeviceManagerService inputDeviceManagerService,
        ILogger<McpServerService> logger,
        IPreferencesService preferencesService,
        IDispatcher dispatcher)
        : base(inputDeviceManagerService, logger)
    {
        _preferencesService = preferencesService;
        _dispatcher = dispatcher;
        _server = null;

        ApplyMcpServer();
    }

    public bool IsMcpServerAvailable => true;

    public bool IsMcpServerEnabled
    {
        get => _preferencesService.Get("McpServerEnabled", false);

        set
        {
            if (IsMcpServerEnabled != value)
            {
                _preferencesService.Set("McpServerEnabled", value);
                ApplyMcpServer();
            }
        }
    }

    public int McpServerPort
    {
        get => _preferencesService.Get("McpServerPort", IMcpServerService.PortDefault);

        set
        {
            if (McpServerPort != value)
            {
                _preferencesService.Set("McpServerPort", value);
                ApplyMcpServer();
            }
        }
    }

    public string McpServerAuthToken
    {
        get => _preferencesService.Get("McpServerAuthToken", IMcpServerService.AuthTokenDefault);

        set
        {
            if (McpServerAuthToken != value)
            {
                _preferencesService.Set("McpServerAuthToken", value);
                ApplyMcpServer();
            }
        }
    }

    public void Dispose()
    {
        lock (_Lock)
        {
            _server?.Dispose();
            _server = null;
        }
    }

    public override void Initialize()
    {
        // add MCP server device if server is available
        if (_server != null)
        {
            AddInputDevice(new McpServerDevice(InputDeviceEventService, _server, _dispatcher));
        }
    }

    public override void Stop()
    {
        // remove MCP server device if present
        if (_server != null)
        {
            TryRemoveInputDevice(x => x.InputDeviceDevice == _server, out var _);
        }
    }

    private void ApplyMcpServer()
    {
        lock (_Lock)
        {
            if (_server != null)
            {
                _server?.Dispose();
                _server = null;
            }

            // create new server with McpServerPort and McpServerAuthToken if enabled
            if (IsMcpServerEnabled)
            {
                _server = new McpServer(McpServerPort, McpServerAuthToken);
                Start();
            }
        }
    }

    private void Start()
    {
        // start non-blocking
        Task.Run(() => _server?.StartAsync());
    }
}
