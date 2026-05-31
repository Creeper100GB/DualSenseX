namespace DSX.Core.Services.Audio;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using NAudio.Wave;
using DSX.Core.Enums;

public sealed class BTHapticsPipeline : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private AudioFileReader? _fileReader;
    private IWavePlayer? _waveOut;
    private Thread? _processingThread;
    private volatile bool _running;
    private volatile bool _disposed;

    private readonly ConcurrentQueue<AudioChunk> _sampleQueue = new();
    private const int MaxQueueSize = 80;

    private const int FftSize = 1024;
    private const int FftSizeLog2 = 10;

    private readonly float[] _fftReal = new float[FftSize];
    private readonly float[] _fftImag = new float[FftSize];
    private readonly float[] _hannWindow = new float[FftSize];
    private readonly float[] _prevMagnitudes = new float[FftSize / 2];
    private int _fftWritePos;

    private float _autoGain = 1.0f;
    private float _smoothedLeft;
    private float _smoothedRight;

    private readonly float[] _bandEnvelope = new float[5];
    private readonly float[] _bandPeak = new float[5];
    private readonly float[] _bandRms = new float[5];
    private readonly int[] _bandSampleCount = new int[5];

    private float _onsetFlux;
    private float _onsetThreshold;
    private float _beatDecay;
    private int _beatHoldFrames;
    private float _beatConfidence;

    private readonly object _startStopLock = new();

    private const float AttackTimeMs = 5f;
    private const float ReleaseTimeMs = 50f;
    private const float BandAttackTimeMs = 3f;
    private const float BandReleaseTimeMs = 80f;
    private const float PeakDecayRate = 0.97f;
    private const float OnsetAlpha = 0.1f;
    private const float BeatHoldMax = 12f;

    public AudioSourceMode AudioSourceMode { get; set; } = AudioSourceMode.SoundCapture;
    public HapticsSourceMode HapticsSourceMode { get; set; } = HapticsSourceMode.SoundWaves;
    public double LeftMotorIntensity { get; set; } = 50.0;
    public double RightMotorIntensity { get; set; } = 50.0;
    public int LatencyCompensationMs { get; set; } = 0;
    public string AudioFilePath { get; set; } = string.Empty;

    public event EventHandler<HapticsDataEventArgs>? HapticsDataReady;
    public event EventHandler<float[]>? AudioDataCaptured;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsRunning => _running;
    public string StatusText { get; private set; } = "Stopped";

    private readonly struct AudioChunk(float[] samples, int channels, int sampleRate)
    {
        public readonly float[] Samples = samples;
        public readonly int Channels = channels;
        public readonly int SampleRate = sampleRate;
    }

    public BTHapticsPipeline()
    {
        for (int i = 0; i < FftSize; i++)
            _hannWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (FftSize - 1)));
    }

    public void Start()
    {
        lock (_startStopLock)
        {
            StopInternal();

            _running = true;
            _fftWritePos = 0;
            _smoothedLeft = 0f;
            _smoothedRight = 0f;
            _autoGain = 1.0f;
            _onsetFlux = 0f;
            _onsetThreshold = 0f;
            _beatDecay = 0f;
            _beatHoldFrames = 0;
            _beatConfidence = 0f;

            Array.Clear(_fftReal);
            Array.Clear(_fftImag);
            Array.Clear(_prevMagnitudes);
            Array.Clear(_bandEnvelope);
            Array.Clear(_bandPeak);
            Array.Clear(_bandRms);
            Array.Clear(_bandSampleCount);

            switch (AudioSourceMode)
            {
                case AudioSourceMode.SoundCapture:
                    StartSoundCapture();
                    break;
                case AudioSourceMode.FilePlayback:
                    StartFilePlayback();
                    break;
                case AudioSourceMode.VDSAudio:
                case AudioSourceMode.MixAndMatch:
                    StatusText = "Waiting for vDS Audio...";
                    break;
            }

            _processingThread = new Thread(ProcessAudioLoop)
            {
                IsBackground = true,
                Name = "DSX-BTHaptics",
                Priority = ThreadPriority.AboveNormal
            };
            _processingThread.Start();

            if (StatusText == "Stopped")
                StatusText = $"Running ({AudioSourceMode})";
        }
    }

    public void Stop()
    {
        lock (_startStopLock)
        {
            StopInternal();
        }
    }

    private void StopInternal()
    {
        _running = false;

        StopSoundCapture();
        StopFilePlayback();

        while (_sampleQueue.TryDequeue(out _)) { }

        _processingThread?.Join(3000);
        _processingThread = null;

        StatusText = "Stopped";
    }

    private void StartSoundCapture()
    {
        try
        {
            _capture = new WasapiLoopbackCapture();
            _capture.DataAvailable += OnCaptureDataAvailable;
            _capture.RecordingStopped += OnCaptureStopped;
            _capture.StartRecording();
            StatusText = $"Running (SoundCapture @ {_capture.WaveFormat.SampleRate}Hz)";
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to start capture: {ex.Message}");
            StatusText = "Error";
        }
    }

    private void StopSoundCapture()
    {
        if (_capture == null) return;

        try
        {
            _capture.DataAvailable -= OnCaptureDataAvailable;
            _capture.RecordingStopped -= OnCaptureStopped;
            _capture.StopRecording();
        }
        catch { }

        _capture.Dispose();
        _capture = null;
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (_running && !_disposed)
        {
            if (e.Exception != null)
                ErrorOccurred?.Invoke(this, $"Capture error: {e.Exception.Message}");

            try { _capture?.StartRecording(); } catch { }
        }
    }

    private void StartFilePlayback()
    {
        try
        {
            if (string.IsNullOrEmpty(AudioFilePath) || !File.Exists(AudioFilePath))
            {
                ErrorOccurred?.Invoke(this, "Audio file not found");
                return;
            }

            _fileReader = new AudioFileReader(AudioFilePath);
            _waveOut = new WaveOutEvent { DesiredLatency = 50 };
            _waveOut.Init(_fileReader);
            _waveOut.PlaybackStopped += OnFilePlaybackStopped;
            _waveOut.Play();

            StatusText = $"Running (FilePlayback @ {_fileReader.WaveFormat.SampleRate}Hz)";

            _ = Task.Run(() => ReadFileToQueue());
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"File playback error: {ex.Message}");
            StatusText = "Error";
        }
    }

    private void ReadFileToQueue()
    {
        if (_fileReader == null) return;

        var format = _fileReader.WaveFormat;
        var channels = format.Channels;
        var sampleRate = format.SampleRate;
        int blockSize = sampleRate / 100 * channels;
        var buffer = new float[blockSize];

        while (_running)
        {
            int read;
            try
            {
                read = _fileReader.Read(buffer, 0, blockSize);
            }
            catch
            {
                break;
            }

            if (read <= 0)
            {
                if (_running)
                {
                    try { _fileReader.Position = 0; } catch { break; }
                    continue;
                }
                break;
            }

            var chunk = new float[read];
            Array.Copy(buffer, chunk, read);

            EnqueueChunk(new AudioChunk(chunk, channels, sampleRate));
        }
    }

    private void OnFilePlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_running && !_disposed)
        {
            try
            {
                _fileReader!.Position = 0;
                _waveOut?.Play();
            }
            catch { }
        }
    }

    private void StopFilePlayback()
    {
        if (_waveOut != null)
        {
            try { _waveOut.Stop(); } catch { }
            if (_waveOut is IDisposable d) d.Dispose();
            _waveOut = null;
        }

        if (_fileReader != null)
        {
            _fileReader.Dispose();
            _fileReader = null;
        }
    }

    private void OnCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_running || _disposed) return;
        if (_capture == null) return;

        int bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
        int channels = _capture.WaveFormat.Channels;
        int sampleCount = e.BytesRecorded / bytesPerSample;

        if (sampleCount == 0) return;

        var samples = new float[sampleCount];
        Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);

        AudioDataCaptured?.Invoke(this, samples);

        EnqueueChunk(new AudioChunk(samples, channels, _capture.WaveFormat.SampleRate));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnqueueChunk(AudioChunk chunk)
    {
        while (_sampleQueue.Count >= MaxQueueSize)
            _sampleQueue.TryDequeue(out _);

        _sampleQueue.Enqueue(chunk);
    }

    public void FeedExternalAudio(float[] samples, int channels, int sampleRate)
    {
        if (!_running || _disposed) return;
        if (AudioSourceMode != AudioSourceMode.VDSAudio && AudioSourceMode != AudioSourceMode.MixAndMatch)
            return;

        EnqueueChunk(new AudioChunk(samples, channels, sampleRate));
    }

    private void ProcessAudioLoop()
    {
        while (_running)
        {
            if (_sampleQueue.TryDequeue(out var chunk))
            {
                ProcessChunk(chunk);
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }

    private void ProcessChunk(AudioChunk chunk)
    {
        var samples = chunk.Samples;
        int channels = chunk.Channels;
        int sampleRate = chunk.SampleRate;

        float leftRms = 0, rightRms = 0;
        int leftCount = 0, rightCount = 0;

        for (int i = 0; i < samples.Length; i += channels)
        {
            float left = Math.Abs(samples[i]);
            leftRms += left * left;
            leftCount++;

            if (channels > 1 && i + 1 < samples.Length)
            {
                float right = Math.Abs(samples[i + 1]);
                rightRms += right * right;
                rightCount++;
            }
            else
            {
                rightRms += left * left;
                rightCount++;
            }
        }

        if (leftCount == 0 || rightCount == 0) return;

        leftRms = MathF.Sqrt(leftRms / leftCount);
        rightRms = MathF.Sqrt(rightRms / rightCount);

        float maxRms = MathF.Max(leftRms, rightRms);
        float targetGain = maxRms > 0.001f ? MathF.Min(0.7f / maxRms, 8f) : 1f;
        _autoGain = _autoGain * 0.95f + targetGain * 0.05f;
        _autoGain = Math.Min(Math.Max(_autoGain, 0.5f), 8f);

        leftRms = MathF.Min(leftRms * _autoGain, 1f);
        rightRms = MathF.Min(rightRms * _autoGain, 1f);

        float sampleDuration = samples.Length / (float)(channels * sampleRate) * 1000f;
        float attackAlpha = 1f - MathF.Exp(-sampleDuration / AttackTimeMs);
        float releaseAlpha = 1f - MathF.Exp(-sampleDuration / ReleaseTimeMs);

        float leftAlpha = leftRms > _smoothedLeft ? attackAlpha : releaseAlpha;
        float rightAlpha = rightRms > _smoothedRight ? attackAlpha : releaseAlpha;
        _smoothedLeft = _smoothedLeft * (1f - leftAlpha) + leftRms * leftAlpha;
        _smoothedRight = _smoothedRight * (1f - rightAlpha) + rightRms * rightAlpha;
        leftRms = _smoothedLeft;
        rightRms = _smoothedRight;

        int monoSampleCount = samples.Length / channels;
        var mono = monoSampleCount <= 1024
            ? new float[monoSampleCount]
            : new float[1024];

        int monoLen = Math.Min(monoSampleCount, mono.Length);
        for (int i = 0; i < monoLen; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < channels && (i * channels + ch) < samples.Length; ch++)
                sum += samples[i * channels + ch];
            mono[i] = sum / channels;
        }

        FeedFftBuffer(mono, monoLen);

        var bands = new float[5];
        float spectralFlux = 0;
        bool fftReady = TryComputeFrequencyBands(sampleRate, bands, ref spectralFlux);

        UpdateBandEnvelopes(bands, sampleDuration);
        DetectOnset(spectralFlux, sampleDuration);

        if (LatencyCompensationMs > 0)
            Thread.Sleep(LatencyCompensationMs);

        var haptics = GenerateHaptics(leftRms, rightRms, bands, fftReady);

        HapticsDataReady?.Invoke(this, new HapticsDataEventArgs
        {
            LeftMotor = haptics.left,
            RightMotor = haptics.right,
            RawAmplitude = (leftRms + rightRms) * 0.5f,
            SubBassEnergy = bands[0],
            BassEnergy = bands[1],
            LowFreqEnergy = bands[2],
            MidFreqEnergy = bands[3],
            HighFreqEnergy = bands[4],
            BeatConfidence = _beatConfidence,
            FrequencyDataValid = fftReady
        });
    }

    private void FeedFftBuffer(float[] mono, int length)
    {
        int len = Math.Min(length, mono.Length);
        for (int i = 0; i < len; i++)
        {
            _fftReal[_fftWritePos] = mono[i];
            _fftImag[_fftWritePos] = 0f;
            _fftWritePos = (_fftWritePos + 1) & (FftSize - 1);
        }
    }

    private bool TryComputeFrequencyBands(int sampleRate, float[] bands, ref float spectralFlux)
    {
        float[] re = new float[FftSize];
        float[] im = new float[FftSize];

        for (int i = 0; i < FftSize; i++)
        {
            int idx = (_fftWritePos + i) & (FftSize - 1);
            re[i] = _fftReal[idx] * _hannWindow[i];
            im[i] = 0f;
        }

        Fft(re, im, FftSizeLog2);

        int binCount = FftSize / 2;
        float binWidth = (float)sampleRate / FftSize;

        float subBassSum = 0, bassSum = 0, lowMidSum = 0, highMidSum = 0, highSum = 0;
        int subBassBins = 0, bassBins = 0, lowMidBins = 0, highMidBins = 0, highBins = 0;

        for (int i = 1; i < binCount; i++)
        {
            float magnitude = MathF.Sqrt(re[i] * re[i] + im[i] * im[i]) / FftSize;
            float freq = i * binWidth;

            if (i < _prevMagnitudes.Length)
            {
                float diff = magnitude - _prevMagnitudes[i];
                if (diff > 0)
                    spectralFlux += diff;
                _prevMagnitudes[i] = magnitude;
            }

            if (freq < 80f)
            {
                subBassSum += magnitude;
                subBassBins++;
            }
            else if (freq < 250f)
            {
                bassSum += magnitude;
                bassBins++;
            }
            else if (freq < 500f)
            {
                lowMidSum += magnitude;
                lowMidBins++;
            }
            else if (freq < 2000f)
            {
                highMidSum += magnitude;
                highMidBins++;
            }
            else if (freq < 8000f)
            {
                highSum += magnitude;
                highBins++;
            }
        }

        bands[0] = subBassBins > 0 ? subBassSum / subBassBins * _autoGain : 0f;
        bands[1] = bassBins > 0 ? bassSum / bassBins * _autoGain : 0f;
        bands[2] = lowMidBins > 0 ? lowMidSum / lowMidBins * _autoGain : 0f;
        bands[3] = highMidBins > 0 ? highMidSum / highMidBins * _autoGain : 0f;
        bands[4] = highBins > 0 ? highSum / highBins * _autoGain : 0f;

        for (int i = 0; i < 5; i++)
            bands[i] = MathF.Min(bands[i], 1f);

        return true;
    }

    private void UpdateBandEnvelopes(float[] bands, float durationMs)
    {
        float bandAttack = 1f - MathF.Exp(-durationMs / BandAttackTimeMs);
        float bandRelease = 1f - MathF.Exp(-durationMs / BandReleaseTimeMs);

        for (int i = 0; i < 5; i++)
        {
            float alpha = bands[i] > _bandEnvelope[i] ? bandAttack : bandRelease;
            _bandEnvelope[i] = _bandEnvelope[i] * (1f - alpha) + bands[i] * alpha;

            _bandRms[i] = _bandRms[i] * 0.95f + bands[i] * bands[i] * 0.05f;

            if (bands[i] > _bandPeak[i])
                _bandPeak[i] = bands[i];
            else
                _bandPeak[i] *= PeakDecayRate;
        }
    }

    private void DetectOnset(float spectralFlux, float durationMs)
    {
        float onsetRelease = 1f - MathF.Exp(-durationMs / 200f);
        _onsetFlux = _onsetFlux * (1f - OnsetAlpha) + spectralFlux * OnsetAlpha;

        _onsetThreshold = _onsetThreshold * 0.995f + _onsetFlux * 0.005f;
        float adaptiveThreshold = MathF.Max(_onsetThreshold * 1.5f, 0.02f);

        bool isOnset = spectralFlux > adaptiveThreshold && spectralFlux > 0.01f;

        if (isOnset)
        {
            _beatConfidence = MathF.Min(spectralFlux / MathF.Max(_onsetThreshold, 0.01f), 3f);
            _beatHoldFrames = (int)BeatHoldMax;
            _beatDecay = 1f;
        }
        else
        {
            if (_beatHoldFrames > 0)
            {
                _beatHoldFrames--;
            }
            else
            {
                _beatDecay *= onsetRelease;
                _beatConfidence *= onsetRelease;
            }
        }

        _beatConfidence = Math.Min(Math.Max(_beatConfidence, 0f), 3f);
    }

    private static void Fft(float[] re, float[] im, int log2N)
    {
        int n = 1 << log2N;

        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, log2N);
            if (j > i)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (int size = 2; size <= n; size *= 2)
        {
            int halfSize = size / 2;
            float angleStep = -2f * MathF.PI / size;

            for (int i = 0; i < n; i += size)
            {
                for (int j = 0; j < halfSize; j++)
                {
                    float angle = j * angleStep;
                    float cos = MathF.Cos(angle);
                    float sin = MathF.Sin(angle);

                    int evenIdx = i + j;
                    int oddIdx = i + j + halfSize;

                    float tRe = cos * re[oddIdx] - sin * im[oddIdx];
                    float tIm = sin * re[oddIdx] + cos * im[oddIdx];

                    re[oddIdx] = re[evenIdx] - tRe;
                    im[oddIdx] = im[evenIdx] - tIm;
                    re[evenIdx] += tRe;
                    im[evenIdx] += tIm;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BitReverse(int value, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }

    private (byte left, byte right) GenerateHaptics(
        float leftRms, float rightRms,
        float[] bands, bool fftReady)
    {
        if (!fftReady)
            return (ToByte(leftRms), ToByte(rightRms));

        float subBass = bands[0];
        float bass = bands[1];
        float lowMid = bands[2];
        float highMid = bands[3];
        float high = bands[4];
        float beat = Math.Min(Math.Max(_beatConfidence, 0f), 1f);

        return HapticsSourceMode switch
        {
            HapticsSourceMode.SoundWaves => GenerateFromSoundWaves(leftRms, rightRms, subBass, bass, lowMid, highMid, beat),
            HapticsSourceMode.SystemCapture => GenerateFromSystemCapture(leftRms, rightRms, subBass, bass, lowMid, beat),
            HapticsSourceMode.FilePlayback => GenerateFromFilePlayback(leftRms, rightRms, subBass, bass, lowMid, beat),
            HapticsSourceMode.VDSAudio => GenerateFromVdsAudio(leftRms, rightRms, subBass, bass, lowMid, beat),
            _ => (0, 0)
        };
    }

    private (byte left, byte right) GenerateFromSoundWaves(
        float leftRms, float rightRms,
        float subBass, float bass, float lowMid, float highMid, float beat)
    {
        float leftScale = (float)(LeftMotorIntensity / 50.0);
        float rightScale = (float)(RightMotorIntensity / 50.0);

        float threshold = 0.015f;

        float leftBassComponent = subBass * 3.0f + bass * 2.0f;
        float rightBassComponent = bass * 1.5f + lowMid * 1.5f;

        float beatBoost = beat * 0.4f;

        float leftVal = leftRms > threshold
            ? Math.Clamp((leftRms * 0.4f + leftBassComponent * 0.5f + beatBoost) * leftScale, 0f, 1f)
            : 0f;
        float rightVal = rightRms > threshold
            ? Math.Clamp((rightRms * 0.3f + rightBassComponent * 0.4f + highMid * 0.3f + beatBoost * 0.6f) * rightScale, 0f, 1f)
            : 0f;

        return (ToByte(leftVal), ToByte(rightVal));
    }

    private (byte left, byte right) GenerateFromSystemCapture(
        float leftRms, float rightRms,
        float subBass, float bass, float lowMid, float beat)
    {
        float scale = (float)(LeftMotorIntensity / 50.0);
        float rightScale = RightMotorIntensity > 0 ? (float)(RightMotorIntensity / 50.0) : scale;

        float bassBoost = subBass * 3.0f + bass * 2.0f + beat * 0.3f;
        float midBoost = lowMid * 1.0f;

        float leftVal = Math.Clamp((leftRms + bassBoost) * scale, 0f, 1f);
        float rightVal = Math.Clamp((rightRms + bassBoost * 0.5f + midBoost) * rightScale, 0f, 1f);

        return (ToByte(leftVal), ToByte(rightVal));
    }

    private (byte left, byte right) GenerateFromFilePlayback(
        float leftRms, float rightRms,
        float subBass, float bass, float lowMid, float beat)
    {
        float scale = (float)(LeftMotorIntensity / 50.0);

        float bassWeight = subBass * 2.5f + bass * 2.0f;
        float beatWeight = beat * 0.5f;
        float combined = (leftRms + rightRms) * 0.4f + bassWeight * 0.4f + beatWeight;

        float leftVal = Math.Clamp(combined * scale, 0f, 1f);
        float rightVal = Math.Clamp((combined * 0.7f + lowMid * 0.3f) * scale, 0f, 1f);

        return (ToByte(leftVal), ToByte(rightVal));
    }

    private (byte left, byte right) GenerateFromVdsAudio(
        float leftRms, float rightRms,
        float subBass, float bass, float lowMid, float beat)
    {
        float leftScale = (float)(LeftMotorIntensity / 50.0);
        float rightScale = (float)(RightMotorIntensity / 50.0);

        float bassComponent = subBass * 2.5f + bass * 2.0f + beat * 0.3f;
        float midComponent = lowMid * 1.0f;

        float leftVal = Math.Clamp((leftRms * 0.3f + bassComponent * 0.6f) * leftScale, 0f, 1f);
        float rightVal = Math.Clamp((rightRms * 0.3f + bassComponent * 0.3f + midComponent * 0.4f) * rightScale, 0f, 1f);

        return (ToByte(leftVal), ToByte(rightVal));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToByte(float v) => (byte)(Math.Clamp(v, 0f, 1f) * 255f);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopInternal();
    }
}

public sealed class HapticsDataEventArgs : EventArgs
{
    public byte LeftMotor { get; init; }
    public byte RightMotor { get; init; }
    public float RawAmplitude { get; init; }
    public float SubBassEnergy { get; init; }
    public float BassEnergy { get; init; }
    public float LowFreqEnergy { get; init; }
    public float MidFreqEnergy { get; init; }
    public float HighFreqEnergy { get; init; }
    public float BeatConfidence { get; init; }
    public bool FrequencyDataValid { get; init; }
}
