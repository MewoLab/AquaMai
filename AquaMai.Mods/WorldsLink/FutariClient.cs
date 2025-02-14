using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AMDaemon;
using PartyLink;
using static Manager.Accounting;

namespace AquaMai.Mods.WorldsLink;

public class FutariClient(string keychip, string host, int port, int _)
{
    // public static string LOBBY_BASE = "http://localhost/mai2-futari/recruit";
    public const string LOBBY_BASE = "https://aquadx.net/aqua/mai2-futari/recruit";
    public static FutariClient Instance { get; private set; }

    public FutariClient(string keychip, string host, int port) : this(keychip, host, port, 0)
    {
        Instance = this;
    }

    public string keychip { get; set; } = keychip;

    private TcpClient _tcpClient;
    private StreamWriter _writer;
    private StreamReader _reader;

    public readonly ConcurrentQueue<Msg> sendQ = new();
    // <Port + Stream ID, Message Queue>
    public readonly ConcurrentDictionary<int, ConcurrentQueue<Msg>> tcpRecvQ = new();
    // <Port, Message Queue>
    public readonly ConcurrentDictionary<int, ConcurrentQueue<Msg>> udpRecvQ = new();
    // <Port, Accept Queue>
    public readonly ConcurrentDictionary<int, ConcurrentQueue<Msg>> acceptQ = new();
    // <Port + Stream ID, Callback>
    public readonly ConcurrentDictionary<int, Action<Msg>> acceptCallbacks = new();

    private Thread _sendThread;
    private Thread _recvThread;
     
    private bool _reconnecting = false;

    private readonly Stopwatch _heartbeat = new Stopwatch().Also(it => it.Start());
    private readonly long[] _delayWindow = new int[20].Select(_ => -1L).ToArray();
    public int _delayIndex = 0;
    public long _delayAvg = 0;
    
    public IPAddress StubIP => FutariExt.KeychipToStubIp(keychip).ToIP();

    /// <summary>
    /// -1: Failed to connect
    /// 0: Not connect
    /// 1: Connecting
    /// 2: Connected
    /// </summary>
    public int StatusCode { get; private set; } = 0;
    public string ErrorMsg { get; private set; } = "";

    public void ConnectAsync() => new Thread(Connect) { IsBackground = true }.Start();

    private void Connect()
    {
        _tcpClient = new TcpClient();

        try
        {
            StatusCode = 1;
            _tcpClient.Connect(host, port);
            StatusCode = 2;
        }
        catch (Exception ex)
        {
            StatusCode = -1;
            ErrorMsg = ex.Message;
            Log.Error($"Error connecting to server:\nHost:{host}:{port}\n{ex.Message}");
            ConnectAsync();
            return;
        }
        var networkStream = _tcpClient.GetStream();
        _writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };
        _reader = new StreamReader(networkStream, Encoding.UTF8);
        _reconnecting = false;

        // Register
        Send(new Msg { cmd = Cmd.CTL_START, data = keychip });
        Log.Info($"Connected to server at {host}:{port}");

        // Start communication and message receiving in separate threads
        _sendThread = 10.Interval(() =>
        {
            if (_heartbeat.ElapsedMilliseconds > 1000)
            {
                _heartbeat.Restart();
                Send(new Msg { cmd = Cmd.CTL_HEARTBEAT });
            }

            // Send any data in the send queue
            while (sendQ.TryDequeue(out var msg)) Send(msg);

        }, final: Reconnect, name: "SendThread", stopOnError: true);
        
        _recvThread = 10.Interval(() =>
        {
            var line = _reader.ReadLine();
            if (line == null) return;

            var message = Msg.FromString(line);
            HandleIncomingMessage(message);
        
        }, final: Reconnect, name: "RecvThread", stopOnError: true);
    }

    public void Bind(int bindPort, ProtocolType proto)
    {
        if (proto == ProtocolType.Tcp) 
            acceptQ.TryAdd(bindPort, new ConcurrentQueue<Msg>());
        else if (proto == ProtocolType.Udp)
            udpRecvQ.TryAdd(bindPort, new ConcurrentQueue<Msg>());
    }

    private void Reconnect()
    {
        Log.Warn("Reconnect Entered");
        if (_reconnecting) return;
        _reconnecting = true;
        
        try { _tcpClient.Close(); }
        catch { /* ignored */ }

        try { _sendThread.Abort(); }
        catch { /* ignored */ }
        
        try { _recvThread.Abort(); }
        catch { /* ignored */ }
        
        _sendThread = null;
        _recvThread = null;
        _tcpClient = null;
        
        // Reconnect
        Log.Warn("Reconnecting...");
        ConnectAsync();
    }

    private void HandleIncomingMessage(Msg msg)
    {
        if (msg.cmd != Cmd.CTL_HEARTBEAT)
            Log.Info($"{StubIP} <<< {msg.ToReadableString()}");

        switch (msg.cmd)
        {
            // Heartbeat
            case Cmd.CTL_HEARTBEAT:
                var delay = _heartbeat.ElapsedMilliseconds;
                _delayWindow[_delayIndex] = delay;
                _delayIndex = (_delayIndex + 1) % _delayWindow.Length;
                _delayAvg = (long) _delayWindow.Where(x => x != -1).Average();
                Log.Info($"Heartbeat: {delay}ms, Avg: {_delayAvg}ms");
                break;
            
            // UDP message
            case Cmd.DATA_SEND or Cmd.DATA_BROADCAST when msg is { proto: ProtocolType.Udp, dPort: not null }:
                udpRecvQ.Get(msg.dPort.Value)?.Also(q =>
                {
                    Log.Info($"+ Added to UDP queue, there are {q.Count + 1} messages in queue");
                })?.Enqueue(msg);
                break;
            
            // TCP message
            case Cmd.DATA_SEND when msg.proto == ProtocolType.Tcp && msg is { sid: not null, dPort: not null }:
                tcpRecvQ.Get(msg.sid.Value + msg.dPort.Value)?.Also(q =>
                {
                    Log.Info($"+ Added to TCP queue, there are {q.Count + 1} messages in queue for port {msg.dPort}");
                })?.Enqueue(msg);
                break;
            
            // TCP connection request
            case Cmd.CTL_TCP_CONNECT when msg.dPort != null:
                acceptQ.Get(msg.dPort.Value)?.Also(q =>
                {
                    Log.Info($"+ Added to Accept queue, there are {q.Count + 1} messages in queue");
                })?.Enqueue(msg);
                break;
            
            // TCP connection accept
            case Cmd.CTL_TCP_ACCEPT when msg is { sid: not null, dPort: not null }:
                acceptCallbacks.Get(msg.sid.Value + msg.dPort.Value)?.Invoke(msg);
                break;
        }
    }

    private void Send(Msg msg)
    {
        // Check if msg's destination ip is the same as my local ip. If so, handle it locally
        if (msg.dst == StubIP.ToU32())
        {
            Log.Debug($"Loopback @@@ {msg.ToReadableString()}");
            HandleIncomingMessage(msg);
            return;
        }
        
        _writer.WriteLine(msg);
        if (msg.cmd != Cmd.CTL_HEARTBEAT)
            Log.Info($"{StubIP} >>> {msg.ToReadableString()}");
    }
}
