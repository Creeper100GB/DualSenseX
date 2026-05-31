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
    private const int MaxQueueSize = 60;
    private const int FftSize = 256;
    private const int FftSizeLog2 = 8;

    private readonly float[] _fftReal = new float[FftSize];
    private readonly float[] _fftImag = new float[FftSize];
    private readonly float[] _hannWindow = new float[FftSize];
    private int _fftWritePos;
    private float _autoGain = 1.0f;
    private float _smoothedLeft;
    private float _smoothedRight;
    private float _runningMax;

    private readonly object _startStopLock = new();

    public AudioSourceMode AudioSourceMode { get; set; } = AudioSourceMode.SoundCapture;
    public HapticsSourceMode HapticsSourceMode { get; set; } = HapticsSourceMode.SoundWaves;
    public double LeftMotorIntensity { get; set; } = 50.0;
    public double RightMotorIntensity { get; set; } = 255.0;
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
            _runningMax = 0.01f;
            Array.Clear(_fftReal);
            Array.Clear(_fftImag);

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

        while (_sampleQueue.TryDequeue(out var chunk)) { }

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
            _waveOut = new WaveOutEvent { DesiredLatency = 100 };
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
            int read = _fileReader.Read(buffer, 0, blockSize);
            if (read <= 0)
            {
                if (_running)
                {
                    _fileReader.Position = 0;
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
        _runningMax = MathF.Max(_runningMax * 0.999f, maxRms);
        _autoGain = MathF.Max(1f, 0.5f / _runningMax);

        leftRms = MathF.Min(leftRms * _autoGain, 1f);
        rightRms = MathF.Min(rightRms * _autoGain, 1f);

        const float smoothUp = 0.8f;
        const float smoothDown = 0.2f;
        float leftAlpha = leftRms > _smoothedLeft ? smoothUp : smoothDown;
        float rightAlpha = rightRms > _smoothedRight ? smoothUp : smoothDown;
        _smoothedLeft = _smoothedLeft * (1f - leftAlpha) + leftRms * leftAlpha;
        _smoothedRight = _smoothedRight * (1f - rightAlpha) + rightRms * rightAlpha;
        leftRms = _smoothedLeft;
        rightRms = _smoothedRight;

        int monoSampleCount = samples.Length / channels;
        var mono = new float[monoSampleCount];

        for (int i = 0; i < monoSampleCount; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < channels && (i * channels + ch) < samples.Length; ch++)
                sum += samples[i * channels + ch];
            mono[i] = sum / channels;
        }

        FeedFftBuffer(mono);

        float lowEnergy = 0, midEnergy = 0, highEnergy = 0;
        bool fftReady = TryComputeFrequencyBands(chunk.SampleRate, ref lowEnergy, ref midEnergy, ref highEnergy);

        if (LatencyCompensationMs > 0)
            Thread.Sleep(LatencyCompensationMs);

        var haptics = GenerateHaptics(leftRms, rightRms, lowEnergy, midEnergy, highEnergy, fftReady);

        HapticsDataReady?.Invoke(this, new HapticsDataEventArgs
        {
            LeftMotor = haptics.left,
            RightMotor = haptics.right,
            LowFreqEnergy = lowEnergy,
            MidFreqEnergy = midEnergy,
            HighFreqEnergy = highEnergy,
            RawAmplitude = (leftRms + rightRms) * 0.5f,
            FrequencyDataValid = fftReady
        });
    }

    private void FeedFftBuffer(float[] mono)
    {
        for (int i = 0; i < mono.Length; i++)
        {
            _fftReal[_fftWritePos] = mono[i];
            _fftImag[_fftWritePos] = 0f;
            _fftWritePos = (_fftWritePos + 1) & (FftSize - 1);
        }
    }

    private bool TryComputeFrequencyBands(int sampleRate, ref float low, ref float mid, ref float high)
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

        float lowSum = 0, midSum = 0, highSum = 0;
        int lowBins = 0, midBins = 0, highBins = 0;

        for (int i = 1; i < binCount; i++)
        {
            float magnitude = MathF.Sqrt(re[i] * re[i] + im[i] * im[i]) / FftSize;
            float freq = i * binWidth;

            if (freq < 250f)
            {
                lowSum += magnitude;
                lowBins++;
            }
            else if (freq < 2000f)
            {
                midSum += magnitude;
                midBins++;
            }
            else if (freq < 8000f)
            {
                highSum += magnitude;
                highBins++;
            }
        }

        low = lowBins > 0 ? lowSum / lowBins * _autoGain : 0f;
        mid = midBins > 0 ? midSum / midBins * _autoGain : 0f;
        high = highBins > 0 ? highSum / highBins * _autoGain : 0f;

        return true;
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
        float lowEnergy, float midEnergy, float highEnergy,
        bool fftReady)
    {
        return HapticsSourceMode switch
        {
            HapticsSourceMode.SoundWaves => GenerateFromSoundWaves(leftRms, rightRms, lowEnergy, midEnergy, fftReady),
            HapticsSourceMode.SystemCapture => GenerateFromSystemCapture(leftRms, rightRms, lowEnergy, fftReady),
            HapticsSourceMode.FilePlayback => GenerateFromFilePlayback(leftRms, rightRms, lowEnergy, fftReady),
            HapticsSourceMode.VDSAudio => GenerateFromVdsAudio(leftRms, rightRms, lowEnergy, fftReady),
            _ => (0, 0)
        };
    }

    private (byte left, byte right) GenerateFromSoundWaves(
        float leftRms, float rightRms, float lowEnergy, float midEnergy, bool fftReady)
    {
        float leftScale = (float)(LeftMotorIntensity / 50.0);
        float rightScale = (float)(RightMotorIntensity / 50.0);

        float threshold = 0.02f;

        float leftVal = leftRms > threshold ? Math.Clamp(leftRms * leftScale, 0f, 1f) : 0f;
        float rightVal = rightRms > threshold ? Math.Clamp(rightRms * rightScale, 0f, 1f) : 0f;

        return ((byte)(leftVal * 255), (byte)(rightVal * 255));
    }

    private (byte left, byte right) GenerateFromSystemCapture(float leftRms, float rightRms, float lowEnergy, bool fftReady)
    {
        float scale = (float)(LeftMotorIntensity / 50.0);
        float rightScale = RightMotorIntensity > 0 ? (float)(RightMotorIntensity / 50.0) : scale;

        float bassBoost = fftReady ? lowEnergy * 2.5f : 0f;
        float leftVal = Math.Clamp((leftRms + bassBoost) * scale, 0f, 1f);
        float rightVal = Math.Clamp((rightRms + bassBoost) * rightScale, 0f, 1f);

        return (ToByte(leftVal), ToByte(rightVal));
    }

    private (byte left, byte right) GenerateFromFilePlayback(float leftRms, float rightRms, float lowEnergy, bool fftReady)
    {
        float scale = (float)(LeftMotorIntensity / 50.0);
        float bassBoost = fftReady ? lowEnergy * 2.0f : 0f;
        float combined = (leftRms + rightRms) * 0.5f + bassBoost;
        byte level = ToByte(Math.Clamp(combined * scale, 0f, 1f));
        return (level, level);
    }

    private (byte left, byte right) GenerateFromVdsAudio(float leftRms, float rightRms, float lowEnergy, bool fftReady)
    {
        float leftScale = (float)(LeftMotorIntensity / 50.0);
        float rightScale = (float)(RightMotorIntensity / 50.0);
        float bassBoost = fftReady ? lowEnergy * 2.0f : 0f;

        float leftVal = Math.Clamp((leftRms + bassBoost) * leftScale, 0f, 1f);
        float rightVal = Math.Clamp((rightRms + bassBoost) * rightScale, 0f, 1f);

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
    public float LowFreqEnergy { get; init; }
    public float MidFreqEnergy { get; init; }
    public float HighFreqEnergy { get; init; }
    public bool FrequencyDataValid { get; init; }
}
