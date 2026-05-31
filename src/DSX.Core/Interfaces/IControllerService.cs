using System.Collections.Generic;
using DSX.Core.Enums;
using DSX.Core.Models;

namespace DSX.Core.Interfaces;

public interface IControllerService
{
    event EventHandler<ControllerDeviceInfo>? ControllerConnected;
    event EventHandler<string>? ControllerDisconnected;
    event EventHandler<ControllerInputState>? InputReceived;
    IReadOnlyList<ControllerDeviceInfo> ConnectedControllers { get; }
    ControllerDeviceInfo? ActiveController { get; }
    void StartPolling();
    void StopPolling();
    void ConnectController(string deviceId);
    void DisconnectController(string deviceId);
    void SetActiveController(string deviceId);
    void WriteOutputReport(byte[] data);
    void SetAdaptiveTrigger(TriggerSide side, TriggerConfig config);
    void SetLEDConfig(TouchpadLEDConfig config);
    void SetPlayerLEDConfig(PlayerLEDConfig config);
    void SetMuteLEDConfig(MuteLEDConfig config);
    void SetRumble(double largeMotor, double smallMotor);
    void SetMicMute(bool muted);
    void SetSpeakerVolume(byte volume);
    void SetHeadphoneVolume(byte volume);
    void SetMicVolume(byte volume);
    void RouteAudioToSpeaker();
    void RouteAudioToHeadphones();
}

public enum TriggerSide
{
    Left,
    Right,
    Both
}
