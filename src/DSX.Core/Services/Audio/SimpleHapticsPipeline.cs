using DSX.Core.Enums;
using DSX.Core.Interfaces;
using DSX.Core.Services.Controller;

namespace DSX.Core.Services.Audio;

public sealed class SimpleHapticsPipeline
{
    private readonly IControllerService _controllerService;
    private volatile bool _enabled;
    private double _leftMotorVolume = 50;
    private double _rightMotorVolume = 50;
    private VolumeSyncMode _syncMode = VolumeSyncMode.Stereo;
    private int _frameCount;

    public SimpleHapticsPipeline(IControllerService controllerService)
    {
        _controllerService = controllerService;
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
                _controllerService.SetRumble(0, 0);
        }
    }

    public double LeftMotorVolume { get => _leftMotorVolume; set => _leftMotorVolume = value; }
    public double RightMotorVolume { get => _rightMotorVolume; set => _rightMotorVolume = value; }
    public VolumeSyncMode SyncMode { get => _syncMode; set => _syncMode = value; }

    public void Process(byte[] buffer, int bytesRecorded, int bytesPerSample, int channels)
    {
        if (!_enabled || buffer == null || bytesRecorded == 0 || channels == 0)
            return;

        int frameCount = bytesRecorded / (bytesPerSample * channels);
        if (frameCount == 0) return;

        double sumL = 0, sumR = 0;
        int countL = 0, countR = 0;

        if (bytesPerSample == 4)
        {
            for (int i = 0; i < frameCount; i++)
            {
                int baseOffset = i * channels * 4;
                if (baseOffset + 4 > bytesRecorded) break;
                float s = System.BitConverter.ToSingle(buffer, baseOffset);
                sumL += s * s;
                countL++;
                if (channels > 1)
                {
                    int ro = baseOffset + 4;
                    if (ro + 4 > bytesRecorded) break;
                    s = System.BitConverter.ToSingle(buffer, ro);
                    sumR += s * s;
                    countR++;
                }
            }
        }
        else if (bytesPerSample == 2)
        {
            for (int i = 0; i < frameCount; i++)
            {
                int baseOffset = i * channels * 2;
                if (baseOffset + 2 > bytesRecorded) break;
                float s = System.BitConverter.ToInt16(buffer, baseOffset) / 32768f;
                sumL += s * s;
                countL++;
                if (channels > 1)
                {
                    int ro = baseOffset + 2;
                    if (ro + 2 > bytesRecorded) break;
                    s = System.BitConverter.ToInt16(buffer, ro) / 32768f;
                    sumR += s * s;
                    countR++;
                }
            }
        }

        double rmsL = System.Math.Sqrt(sumL / System.Math.Max(1, countL));
        double rmsR = channels > 1
            ? System.Math.Sqrt(sumR / System.Math.Max(1, countR))
            : rmsL;

        if (rmsL < 0.01 && rmsR < 0.01)
        {
            _controllerService.SetRumble(0, 0);
            return;
        }

        double motorL = rmsL * (_leftMotorVolume / 100.0);
        double motorR = rmsR * (_rightMotorVolume / 100.0);

        double leftPct = System.Math.Min(100, motorL * 100);
        double rightPct = System.Math.Min(100, motorR * 100);

        _frameCount++;
        if (_frameCount % 30 == 0)
        {
            ControllerService.LogAction?.Invoke(
                $"[SimpleHaptics] L={leftPct:F1}%/R={rightPct:F1}% rmsL={rmsL:F3} rmsR={rmsR:F3}");
        }

        _controllerService.SetRumble(leftPct, rightPct);
    }

    public void Stop()
    {
        _enabled = false;
        _controllerService.SetRumble(0, 0);
    }
}
