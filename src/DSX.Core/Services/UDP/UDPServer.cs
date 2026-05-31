using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DSX.Core.Constants;
using DSX.Core.Interfaces;
using DSX.Core.Models;

namespace DSX.Core.Services.UDP;

public sealed class UDPServer : IUDPServer, IDisposable
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly object _lock = new();
    private volatile bool _disposed;

    public event EventHandler<string>? PacketReceived;

    public int Port { get; private set; } = AppConstants.DefaultUDPPort;

    public void Start(int port)
    {
        lock (_lock)
        {
            StopInternal();

            Port = port;
            _cts = new CancellationTokenSource();
            _udpClient = new UdpClient(port);

            SavePortToFile(port);

            _receiveTask = Task.Run(ReceiveLoop);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            StopInternal();
        }
    }

    public void Send(byte[] data, string host, int port)
    {
        lock (_lock)
        {
            if (_udpClient == null || _disposed)
                return;

            try
            {
                _udpClient.Send(data, data.Length, host, port);
            }
            catch
            {
                // Ignore send errors
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopInternal();
    }

    private void StopInternal()
    {
        _cts?.Cancel();

        try
        {
            _udpClient?.Close();
        }
        catch { }

        if (_receiveTask != null)
        {
            try
            {
                _receiveTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch { }
        }

        _udpClient?.Dispose();
        _udpClient = null;
        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;
    }

    private async Task ReceiveLoop()
    {
        var token = _cts?.Token ?? CancellationToken.None;

        while (!token.IsCancellationRequested && !_disposed)
        {
            try
            {
                if (_udpClient == null)
                    break;

                var result = await _udpClient.ReceiveAsync().ConfigureAwait(false);
                var json = Encoding.UTF8.GetString(result.Buffer, 0, result.Buffer.Length);

                if (string.IsNullOrWhiteSpace(json))
                    continue;

                try
                {
                    var packet = JsonSerializer.Deserialize<DSXPacket>(json);
                    if (packet != null)
                        PacketReceived?.Invoke(this, json);
                }
                catch (JsonException)
                {
                    // Not valid JSON, ignore
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted
                || ex.SocketErrorCode == SocketError.Interrupted)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (token.IsCancellationRequested)
                    break;

                await Task.Delay(10, token).ConfigureAwait(false);
            }
        }
    }

    private static void SavePortToFile(int port)
    {
        try
        {
            var dir = Path.GetDirectoryName(AppConstants.UdpPortFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(AppConstants.UdpPortFilePath, port.ToString());
        }
        catch
        {
            // Non-critical
        }
    }
}
