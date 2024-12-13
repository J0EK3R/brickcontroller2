using Android.Views;
using BrickController2.Droid.Extensions;
using BrickController2.Helpers;

namespace BrickController2.Droid.PlatformServices.GameController
{
    internal class GamepadController
    {
        private readonly GameControllerService _controllerService;
        private readonly InputDevice _gamepad;

        private readonly int _controllerIndex;
        private readonly string _controllerName;
        private readonly string _uniquePersistantDeviceId;

        public string UniquePersistantDeviceId => _uniquePersistantDeviceId;
        public int ControllerIndex => _controllerIndex;
        public string ControllerName => _controllerName;

        public GamepadController(GameControllerService service, InputDevice gamePad, int controllerIndex)
        {
            _controllerService = service;
            _gamepad = gamePad;
            _controllerIndex = controllerIndex;
            _uniquePersistantDeviceId = gamePad.GetUniquePersistentDeviceId();
            _controllerName = GameControllerHelper.GetControllerDeviceId(controllerIndex);
        }
    }
}