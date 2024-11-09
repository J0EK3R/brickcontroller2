using BrickController2.PlatformServices.GameController;

namespace BrickController2.UI.Services.Dialog
{
    public class GameControllerEventDialogResult
    {
        public const string NoControllerDeviceId = "NoControllerDeviceId";

        public GameControllerEventDialogResult(bool isOk, string controllerDeviceId, GameControllerEventType eventType, string eventCode)
        {
            IsOk = isOk;
            EventType = eventType;
            EventCode = eventCode;
            ControllerDeviceId = controllerDeviceId;
        }

        public bool IsOk { get; }
        public GameControllerEventType EventType { get; }
        public string EventCode { get; }
        public string ControllerDeviceId { get; }
    }
}
