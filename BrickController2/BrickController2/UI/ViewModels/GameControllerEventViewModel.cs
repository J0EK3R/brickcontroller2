﻿using BrickController2.PlatformServices.GameController;
using BrickController2.Helpers;

namespace BrickController2.UI.ViewModels
{
    public class GameControllerEventViewModel : NotifyPropertyChangedSource
    {
        private float _value;

        public GameControllerEventViewModel(string controllerDeviceId, GameControllerEventType eventType, string eventCode, float value)
        {
            ControllerDeviceId = controllerDeviceId;
            EventType = eventType;
            EventCode = eventCode;
            Value = value;
        }

        public GameControllerEventType EventType { get; }
        public string EventCode { get; }

        public string ControllerDeviceId { get; }

        public float Value
        {
            get => _value;
            set { _value = value; RaisePropertyChanged(); }
        }
    }
}
