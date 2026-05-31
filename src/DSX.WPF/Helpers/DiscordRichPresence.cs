using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace DSX.WPF.Helpers;

public class DiscordRichPresence : IDisposable
{
    private const string AppId = "870460775223050250";
    private const string DiscordPipeName = "discord-ipc-0";
    private const int PipeTimeout = 3000;
    private const int SteamCheckInterval = 2000;
    private const int DisconnectAfterSteamSeconds = 5;

    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _cts;
    private int _pipeCounter;
    private bool _isConnected;
    private bool _disposed;
    private DateTime? _steamDetectedAt;
    private string _currentTab = "Home";
    private double _batteryPercentage = 100;
    private bool _steamDetected;

    public event EventHandler<string>? LogMessage;

    public bool IsActive => _isConnected && !_steamDetected;

    public async Task InitializeAsync()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ConnectionLoop(_cts.Token));
        _ = Task.Run(() => SteamMonitorLoop(_cts.Token));
    }

    public void UpdatePresence(string tab, double batteryPercentage)
    {
        _currentTab = tab;
        _batteryPercentage = batteryPercentage;

        if (_isConnected && !_steamDetected)
        {
            SendPresence();
        }
    }

    private async Task ConnectionLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_isConnected)
                {
                    await TryConnectAsync(ct);
                }

                if (_isConnected && _pipe != null && _pipe.IsConnected)
                {
                    var buffer = new byte[4096];
                    var bytesRead = await _pipe.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0)
                    {
                        Disconnect();
                    }
                    else
                    {
                        var opCode = BitConverter.ToUInt32(buffer, 0);
                        if (opCode == 2)
                        {
                            Log("Discord connection closed by server");
                            Disconnect();
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log($"Connection error: {ex.Message}");
                Disconnect();
                await Task.Delay(5000, ct);
            }
        }
    }

    private async Task TryConnectAsync(CancellationToken ct)
    {
        for (int i = 0; i < 10; i++)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                var pipeName = $"discord-ipc-{i}";
                _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                await _pipe.ConnectAsync(PipeTimeout, ct);

                SendHandshake();
                _isConnected = true;
                _pipeCounter = i;
                Log($"Connected to Discord (pipe {i})");
                SendPresence();
                return;
            }
            catch { }
        }

        Log("Discord not found, will retry...");
    }

    private void SendHandshake()
    {
        if (_pipe == null || !_pipe.IsConnected) return;

        var json = $"{{\"v\":1,\"client_id\":\"{AppId}\"}}";
        SendPacket(0, json);
    }

    private void SendPresence()
    {
        if (_pipe == null || !_pipe.IsConnected) return;

        var batteryDisplay = _batteryPercentage >= 0 ? $"{_batteryPercentage:0}%" : "N/A";
        var json = $"{{\"pid\":{Environment.ProcessId},\"activity\":{{\"state\":\"Battery: {batteryDisplay}\",\"details\":\"{_currentTab}\",\"assets\":{{\"large_image\":\"dsx_logo\",\"large_text\":\"DualSenseX\"}}}}}}";

        SendPacket(1, json);
    }

    private void SendPacket(int opCode, string json)
    {
        if (_pipe == null || !_pipe.IsConnected) return;

        try
        {
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            var header = new byte[8];
            BitConverter.GetBytes((uint)opCode).CopyTo(header, 0);
            BitConverter.GetBytes((uint)jsonBytes.Length).CopyTo(header, 4);

            _pipe.Write(header, 0, 8);
            _pipe.Write(jsonBytes, 0, jsonBytes.Length);
            _pipe.Flush();
        }
        catch (Exception ex)
        {
            Log($"Send error: {ex.Message}");
            Disconnect();
        }
    }

    private async Task SteamMonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var isSteamRunning = IsSteamRunning();

                if (isSteamRunning && !_steamDetected)
                {
                    _steamDetected = true;
                    _steamDetectedAt = DateTime.UtcNow;
                    Log("Steam detected, scheduling Discord disconnect");
                }
                else if (isSteamRunning && _steamDetected && _steamDetectedAt.HasValue)
                {
                    if ((DateTime.UtcNow - _steamDetectedAt.Value).TotalSeconds >= DisconnectAfterSteamSeconds)
                    {
                        if (_isConnected)
                        {
                            Disconnect();
                            Log("Disconnected from Discord (Steam active)");
                        }
                    }
                }
                else if (!isSteamRunning && _steamDetected)
                {
                    _steamDetected = false;
                    _steamDetectedAt = null;
                    Log("Steam no longer running");
                }

                await Task.Delay(SteamCheckInterval, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private static bool IsSteamRunning()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("steam"))
            {
                process.Dispose();
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    private void Disconnect()
    {
        _isConnected = false;
        try
        {
            _pipe?.Close();
        }
        catch { }
        _pipe = null;
    }

    private void Log(string message)
    {
        LogMessage?.Invoke(this, message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        Disconnect();
        _cts?.Dispose();
    }
}
