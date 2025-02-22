#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Manager.Party.Party;
using PartyLink;

namespace AquaMai.Mods.WorldsLink;

public static class FutariExt
{
    private static uint HashStringToUInt(string input)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return ((uint)(hashBytes[0] & 0xFF) << 24) |
               ((uint)(hashBytes[1] & 0xFF) << 16) |
               ((uint)(hashBytes[2] & 0xFF) << 8) |
               ((uint)(hashBytes[3] & 0xFF));
    }

    public static uint KeychipToStubIp(string keychip) => HashStringToUInt(keychip);

    public static IPAddress ToIP(this uint val) => new(new IpAddress(val).GetAddressBytes());
    public static uint ToU32(this IPAddress ip) => ip.ToNetworkByteOrderU32();
    
    public static void Do<T>(this T x, Action<T> f) => f(x);
    public static R Let<T, R>(this T x, Func<T, R> f) => f(x);
    public static T Also<T>(this T x, Action<T> f) { f(x); return x; }

    public static List<T> Each<T>(this IEnumerable<T> enu, Action<T> f) => 
        enu.ToList().Also(x => x.ForEach(f));

    public static byte[] View(this byte[] buffer, int offset, int size)
    {
        var array = new byte[size];
        Array.Copy(buffer, offset, array, 0, size);
        return array;
    }
    
    public static string B64(this byte[] buffer) => Convert.ToBase64String(buffer);
    public static byte[] B64(this string str) => Convert.FromBase64String(str);
    
    public static V? Get<K, V>(this ConcurrentDictionary<K, V> dict, K key) where V : class
    {
        return dict.GetValueOrDefault(key);
    }
    
    // Call a function using reflection
    public static void Call(this object obj, string method, params object[] args)
    {
        obj.GetType().GetMethod(method)?.Invoke(obj, args);
    }

    public static uint MyStubIP() => KeychipToStubIp(AMDaemon.System.KeychipId.ShortValue);
    
    public static string Post(this string url, string body) => new WebClient().UploadString(url, body);
    public static void PostAsync(this string url, string body, UploadStringCompletedEventHandler? callback = null) => 
        new WebClient().Also(web =>
        {
            callback?.Do(it => web.UploadStringCompleted += it);
            web.UploadStringAsync(new Uri(url), body);
        });
    
    public static Thread Interval(
        this int delay, Action action, bool stopOnError = false, 
        Action<Exception>? error = null, Action? final = null, string? name = null
    ) => new Thread(() => 
    {
        name ??= $"Interval {Thread.CurrentThread.ManagedThreadId} for {action}";
        try
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(delay);
                    action();
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    if (stopOnError) throw;
                    Log.Error($"Error in {name}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"Fatal error in {name}: {e}");
            error?.Invoke(e);
        }
        finally
        {
            Log.Warn($"{name} stopped");
            final?.Invoke();
        }
    }).Also(x => x.Start());

}