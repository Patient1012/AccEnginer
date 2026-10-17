using System.Net;
using System.Net.Sockets;

namespace AccEngineer.Telemetry.Broadcasting;

/// <summary>
/// Transport used by <see cref="AccBroadcastClient"/>. Abstracted so the client's
/// registration/aggregation logic can be driven deterministically in tests
/// without a real socket.
/// </summary>
public interface IAccBroadcastTransport : IDisposable
{
    void Send(byte[] datagram);

    /// <summary>Returns the next datagram, or false when none is currently available.</summary>
    bool TryReceive(out byte[] datagram);
}

/// <summary>
/// Real UDP transport. Binds an ephemeral local port (ACC replies to the sending
/// socket) and sends to the configured ACC broadcasting endpoint.
/// </summary>
public sealed class UdpAccBroadcastTransport : IAccBroadcastTransport
{
    private readonly UdpClient _client;

    public UdpAccBroadcastTransport(string host, int port)
    {
        _client = new UdpClient(AddressFamily.InterNetwork);
        _client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        _client.Connect(host, port);
        _client.Client.Blocking = true;
    }

    public void Send(byte[] datagram) => _client.Send(datagram, datagram.Length);

    public bool TryReceive(out byte[] datagram)
    {
        datagram = [];
        try
        {
            if (_client.Available == 0)
            {
                return false;
            }

            IPEndPoint? remote = null;
            datagram = _client.Receive(ref remote);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public void Dispose() => _client.Dispose();
}
