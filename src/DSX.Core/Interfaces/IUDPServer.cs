namespace DSX.Core.Interfaces;

public interface IUDPServer
{
    event EventHandler<string>? PacketReceived;
    int Port { get; }
    void Start(int port);
    void Stop();
    void Send(byte[] data, string host, int port);
}
