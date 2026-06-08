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
    private double _smoothLeft;
    private double _smoothRight;
    private int _frameCount;
    private const int MinIntervalMs = 15;
    private readonly System.Diagnostics.Stopwatch _throttle = System.Diagnostics.Stopwatch.StartNew();
    private double _lastSentLeft = -1;
    private double _lastSentRight = -1;
    private const double ChangeThreshold = 0.5;
    private bool _wasSilent = true;
    private const double SilenceThreshold = 0.02;
    private const double AttackAlpha = 0.4;
    private const double ReleaseAlpha = 0.08;
    private const double BassBoost = 1.5;

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
            {
                _smoothLeft = 0;
                _smoothRight = 0;
                _lastSentLeft = -1;
                _lastSentRight = -1;
                _throttle.Restart();
                _controllerService.SetRumble(0, 0);
            }
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
                int offsetL = i * channels * 4;
                if (offsetL + 4 > bytesRecorded) break;
                float sample = System.BitConverter.ToSingle(buffer, offsetL);
                sumL += sample * sample;
                countL++;

                if (channels > 1)
                {
                    int offsetR = offsetL + 4;
                    if (offsetR + 4 > bytesRecorded) break;
                    sample = System.BitConverter.ToSingle(buffer, offsetR);
                    sumR += sample * sample;
                    countR++;
                }
            }
        }
        else if (bytesPerSample == 2)
        {
            for (int i = 0; i < frameCount; i++)
            {
                int offsetL = i * channels * 2;
                if (offsetL + 2 > bytesRecorded) break;
                float sample = System.BitConverter.ToInt16(buffer, offsetL) / 32768f;
                sumL += sample * sample;
                countL++;

                if (channels > 1)
                {
                    int offsetR = offsetL + 2;
                    if (offsetR + 2 > bytesRecorded) break;
                    sample = System.BitConverter.ToInt16(buffer, offsetR) / 32768f;
                    sumR += sample * sample;
                    countR++;
                }
            }
        }

        double rmsL = System.Math.Sqrt(sumL / System.Math.Max(1, countL));
        double rmsR = channels > 1
            ? System.Math.Sqrt(sumR / System.Math.Max(1, countR))
            : rmsL;

        bool isSilent = rmsL < SilenceThreshold && rmsR < SilenceThreshold;

        if (isSilent)
        {
            if (!_wasSilent)
            {
                _wasSilent = true;
                _smoothLeft = 0;
                _smoothRight = 0;
                _lastSentLeft = -1;
                _lastSentRight = -1;
                _controllerService.SetRumble(0, 0);
            }
            return;
        }

        _wasSilent = false;

        double motorL, motorR;
        switch (_syncMode)
        {
            case VolumeSyncMode.Mono:
                double mono = (rmsL + rmsR) / 2;
                motorL = mono;
                motorR = mono;
                break;
            case VolumeSyncMode.LeftOnly:
                motorL = rmsL;
                motorR = rmsL;
                break;
            case VolumeSyncMode.RightOnly:
                motorL = rmsR;
                motorR = rmsR;
                break;
            case VolumeSyncMode.Swap:
                motorL = rmsR;
                motorR = rmsL;
                break;
            default:
                motorL = rmsL;
                motorR = rmsR;
                break;
        }

        motorL *= _leftMotorVolume / 100.0;
        motorR *= _rightMotorVolume / 100.0;

        double leftAlpha = motorL > _smoothLeft ? AttackAlpha : ReleaseAlpha;
        double rightAlpha = motorR > _smoothRight ? AttackAlpha : ReleaseAlpha;
        _smoothLeft += leftAlpha * (motorL - _smoothLeft);
        _smoothRight += rightAlpha * (motorR - _smoothRight);

        double leftPct = System.Math.Min(100, System.Math.Max(0, _smoothLeft * 100));
        double rightPct = System.Math.Min(100, System.Math.Max(0, _smoothRight * 100));
        leftPct = System.Math.Min(100, leftPct * (1.0 + BassBoost * 0.3));
        rightPct = System.Math.Min(100, rightPct * 1.0);

        long elapsed = _throttle.ElapsedMilliseconds;
        double deltaL = System.Math.Abs(leftPct - _lastSentLeft);
        double deltaR = System.Math.Abs(rightPct - _lastSentRight);

        if (elapsed < MinIntervalMs && deltaL < ChangeThreshold && deltaR < ChangeThreshold)
            return;

        _lastSentLeft = leftPct;
        _lastSentRight = rightPct;
        _throttle.Restart();

        _frameCount++;
        if (_frameCount % 20 == 0)
        {
            ControllerService.LogAction?.Invoke(
                $"[SimpleHaptics] L={leftPct:F0}%/R={rightPct:F0}% rmsL={rmsL:F3} rmsR={rmsR:F3}");
        }

        _controllerService.SetRumble(leftPct, rightPct);
    }

    public void Stop()
    {
        _enabled = false;
        _smoothLeft = 0;
        _smoothRight = 0;
        _lastSentLeft = -1;
        _lastSentRight = -1;
        _throttle.Restart();
        _controllerService.SetRumble(0, 0);
    }
}
