using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using PartyLink;

namespace AquaMai.Mods.WorldsLink;

public class FutariSocket
{
    private int _bindPort = -1;
    private readonly FutariClient _client;
    private readonly ProtocolType _proto;
    private int _streamId = -1;

    // ConnectSocket.Enter_Active (doesn't seem to be actually used)
    public EndPoint LocalEndPoint => new IPEndPoint(_client.StubIP, 0);

    // ConnectSocket.Enter_Active, ListenSocket.acceptClient (TCP)
    // Each client's remote endpoint must be different
    public EndPoint RemoteEndPoint { get; private set; }

    private FutariSocket(FutariClient client, ProtocolType proto)
    {
        _client = client;
        _proto = proto;
        RemoteEndPoint = new IPEndPoint(_client.StubIP, 0);
    }

    // Compatibility constructor
    public FutariSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, int mockID) :
        this(FutariClient.Instance, protocolType) { }

    // ListenSocket.open (TCP)
    public void Listen(int backlog) { }

    // ListenSocket.open, UdpRecvSocket.open
    public void Bind(EndPoint localEndP)
    {
        if (localEndP is IPEndPoint ep) _client.Bind(_bindPort = ep.Port, _proto);
    }

    // Only used in BroadcastSocket
    public void SetSocketOption(SocketOptionLevel l, SocketOptionName n, bool o) { }

    // SocketBase.checkRecvEnable, checkSendEnable
    // This is the Select step called before blocking calls (e.g. Accept)
    public static bool Poll(FutariSocket socket, SelectMode mode)
    {
        if (mode == SelectMode.SelectRead)
        {
            return (socket._proto == ProtocolType.Udp)  // Check is UDP
                ? !socket._client.udpRecvQ.Get(socket._bindPort)?.IsEmpty ?? false
                : (socket._streamId == -1)  // Check is TCP stream or TCP server
                    ? !socket._client.acceptQ.Get(socket._bindPort)?.IsEmpty ?? false
                    : !socket._client.tcpRecvQ.Get(socket._streamId + socket._bindPort)?.IsEmpty ?? false;
        }
        // Write is always ready?
        return mode == SelectMode.SelectWrite;
    }
    
    private static readonly FieldInfo CompletedField = typeof(SocketAsyncEventArgs)
        .GetField("Completed", BindingFlags.Instance | BindingFlags.NonPublic);

    // ConnectSocket.Enter_Connect (TCP)
    // The destination address is obtained from a RecruitInfo packet sent by the host.
    // The host should patch their PartyLink.Util.MyIpAddress() to return a mock address instead of the real one
    // Returns true if IO is pending and false if the operation completed synchronously
    // When it's false, e will contain the result of the operation
    public bool ConnectAsync(SocketAsyncEventArgs e, int mockID)
    {
        if (e.RemoteEndPoint is not IPEndPoint remote) return false;
        var addr = remote.Address.ToU32();

        // Change Localhost to the local keychip address
        if (addr is 2130706433 or 16777343) addr = _client.StubIP.ToU32();
        
        // Random stream ID and port
        _streamId = new Random().Next();
        _bindPort = new Random().Next(55535, 65535);
        
        _client.tcpRecvQ[_streamId + _bindPort] = new ConcurrentQueue<Msg>();
        _client.acceptCallbacks[_streamId + _bindPort] = msg =>
        {
            Log.Info("ConnectAsync: Accept callback, invoking Completed event");
            var events = (MulticastDelegate) CompletedField.GetValue(e);
            foreach (var handler in events?.GetInvocationList() ?? [])
            {
                Log.Debug($"ConnectAsync: Invoking {handler.Method.Name}");
                handler.DynamicInvoke(e, new SocketAsyncEventArgs { SocketError = SocketError.Success });
            }
        };
        _client.sendQ.Enqueue(new Msg
        {
            cmd = Cmd.CTL_TCP_CONNECT, 
            proto = _proto,
            sid = _streamId,
            src = _client.StubIP.ToU32(), sPort = _bindPort,
            dst = addr, dPort = remote.Port
        });
        RemoteEndPoint = new IPEndPoint(addr.ToIP(), remote.Port);
        return true;
    }

    // Accept is blocking
    public FutariSocket Accept()
    {
        // Check if accept queue has any pending connections
        if (!_client.acceptQ.TryGetValue(_bindPort, out var q) ||
            !q.TryDequeue(out var msg) || 
            msg.sid == null || msg.src == null || msg.sPort == null)
        {
            Log.Warn("Accept: No pending connections");
            return null;
        }

        _client.tcpRecvQ[msg.sid.Value + _bindPort] = new ConcurrentQueue<Msg>();
        _client.sendQ.Enqueue(new Msg
        {
            cmd = Cmd.CTL_TCP_ACCEPT, proto = _proto, sid = msg.sid,
            src = _client.StubIP.ToU32(), sPort = _bindPort,
            dst = msg.src, dPort = msg.sPort
        });
        
        return new FutariSocket(_client, _proto)
        {
            _streamId = msg.sid.Value,
            _bindPort = _bindPort,
            RemoteEndPoint = new IPEndPoint(msg.src.Value.ToIP(), msg.sPort.Value)
        };
    }

    public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        if (RemoteEndPoint is not IPEndPoint remote) throw new InvalidOperationException("RemoteEndPoint is not set");
        Log.Debug($"Send: {size} bytes");
        // Remote EP is not relevant here, because the stream is already established,
        // there can only be one remote endpoint
        _client.sendQ.Enqueue(new Msg
        {
            cmd = Cmd.DATA_SEND, proto = _proto, data = buffer.View(offset, size).B64(),
            sid = _streamId == -1 ? null : _streamId,
            src = _client.StubIP.ToU32(), sPort = _bindPort,
            dst = remote.Address.ToU32(), dPort = remote.Port
        });
        return size;
    }

    // Only used in BroadcastSocket
    public int SendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP)
    {
        Log.Error("SendTo: Blocked");
        return 0;

        Log.Debug($"SendTo: {size} bytes");
        if (remoteEP is not IPEndPoint remote) return 0;
        _client.sendQ.Enqueue(new Msg
        {
            cmd = Cmd.DATA_BROADCAST, proto = _proto, data = buffer.View(offset, size).B64(), 
            src = _client.StubIP.ToU32(), sPort = _bindPort,
            dst = remote.Address.ToU32(), dPort = remote.Port
        });
        return size;
    }

    // Only used in TCP ConnectSocket
    public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode)
    {
        if (!_client.tcpRecvQ.TryGetValue(_streamId + _bindPort, out var q) || 
            !q.TryDequeue(out var msg))
        {
            Log.Warn("Receive: No data to receive");
            errorCode = SocketError.WouldBlock;
            return 0;
        }
        var data = msg.data!.B64();
        Log.Debug($"Receive: {data.Length} bytes, {q.Count} left in queue");

        Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
        errorCode = SocketError.Success;
        return data.Length;
    }

    // Only used in UdpRecvSocket to receive from 0 (broadcast)
    public int ReceiveFrom(byte[] buffer, SocketFlags socketFlags, ref EndPoint remoteEP)
    {
        Log.Error("ReceiveFrom: Blocked");
        return 0;
        
        if (!_client.udpRecvQ.TryGetValue(_bindPort, out var q) || 
            !q.TryDequeue(out var msg))
        {
            Log.Warn("ReceiveFrom: No data to receive");
            return 0;
        }
        var data = msg.data?.B64() ?? [];
        Log.Debug($"ReceiveFrom: {data.Length} bytes");

        // Set remote endpoint to the sender
        if (msg.src.HasValue)
            remoteEP = new IPEndPoint(msg.src.Value.ToIP(), msg.sPort ?? 0);

        Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
        return data.Length;
    }

    // Called everywhere, but only relevant for TCP
    public void Close()
    {
        // TCP FIN/RST
        if (_proto == ProtocolType.Tcp) 
            _client.sendQ.Enqueue(new Msg { cmd = Cmd.CTL_TCP_CLOSE, proto = _proto });
    }

    public void Shutdown(SocketShutdown how) => Close();
}