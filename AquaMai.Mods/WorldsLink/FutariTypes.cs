using System;
using System.Linq;
using System.Net.Sockets;
using MelonLoader;

namespace AquaMai.Mods.WorldsLink;

public enum Cmd
{
    // Control plane
    CTL_START = 1,
    CTL_BIND = 2,
    CTL_HEARTBEAT = 3,
    CTL_TCP_CONNECT = 4,  // Accept a new multiplexed TCP stream
    CTL_TCP_ACCEPT = 5,
    CTL_TCP_ACCEPT_ACK = 6,
    CTL_TCP_CLOSE = 7,

    // Data plane
    DATA_SEND = 21,
    DATA_BROADCAST = 22,
}


public struct Msg
{
    public Cmd cmd;
    public ProtocolType? proto;
    public int? sid;
    public uint? src;
    public int? sPort;
    public uint? dst;
    public int? dPort;
    public string? data;

    public override string ToString()
    {
        int? proto_ = proto == null ? null : (int) proto;
        var arr = new object[] {
            1, (int) cmd, proto_, sid, src, sPort, dst, dPort, 
            null, null, null, null, null, null, null, null,  // reserved for future use
            data
        };
        
        // Map nulls to empty strings
        return string.Join(",", arr.Select(x => x ?? "")).TrimEnd(',');
    }
    
    private static T? Parse<T>(string[] fields, int i) where T : struct 
        => fields.Length <= i || fields[i] == "" ? null
            : (T) Convert.ChangeType(fields[i], typeof(T));
    

    public static Msg FromString(string str)
    {
        var fields = str.Split(',');
        return new Msg
        {
            cmd = (Cmd) (Parse<int>(fields, 1) ?? throw new InvalidOperationException("cmd is required")),
            proto = Parse<int>(fields, 2)?.Let(it => (ProtocolType) it),
            sid = Parse<int>(fields, 3),
            src = Parse<uint>(fields, 4),
            sPort = Parse<int>(fields, 5),
            dst = Parse<uint>(fields, 6),
            dPort = Parse<int>(fields, 7),
            data = string.Join(",", fields.Skip(16))
        };
    }
}

public abstract class Log
{
    // Text colors
    private const string BLACK = "\u001b[30m";
    private const string RED = "\u001b[31m";
    private const string GREEN = "\u001b[32m";
    private const string YELLOW = "\u001b[33m";
    private const string BLUE = "\u001b[34m";
    private const string MAGENTA = "\u001b[35m";
    private const string CYAN = "\u001b[36m";
    private const string WHITE = "\u001b[37m";

    // Bright text colors
    private const string BRIGHT_BLACK = "\u001b[90m";
    private const string BRIGHT_RED = "\u001b[91m";
    private const string BRIGHT_GREEN = "\u001b[92m";
    private const string BRIGHT_YELLOW = "\u001b[93m";
    private const string BRIGHT_BLUE = "\u001b[94m";
    private const string BRIGHT_MAGENTA = "\u001b[95m";
    private const string BRIGHT_CYAN = "\u001b[96m";
    private const string BRIGHT_WHITE = "\u001b[97m";

    // Reset
    private const string RESET = "\u001b[0m";
    
    public static void Error(string msg)
    {
        MelonLogger.Error(RED + "[FUTARI] ERROR " + msg + RESET);
    }
    
    public static void Warn(string msg)
    {
        MelonLogger.Warning(YELLOW + "[FUTARI] WARN  " + msg + RESET);
    }

    public static void Debug(string msg)
    {
        MelonLogger.Msg(BLUE + "[FUTARI] DEBUG " + msg + RESET);
    }

    public static void Info(string msg)
    {
        if (msg.StartsWith("A001")) msg = MAGENTA + msg;
        if (msg.StartsWith("A002")) msg = CYAN + msg;
        msg = msg.Replace("Error", RED + "Error");
        MelonLogger.Msg("[FUTARI] INFO  " + msg + RESET);
    }
}
