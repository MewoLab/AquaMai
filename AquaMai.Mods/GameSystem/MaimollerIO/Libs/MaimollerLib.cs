#nullable enable

using System;
using System.Linq;
using System.Threading;
using HidLibrary;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public enum MaimollerInitMode
{
    P1,
    P2,
    P1P2
}

public static class Maimoller
{
    private const int VID = 0x0E8F;
    private const int PID = 0x1224;
    private const int USAGE_PAGE = 0xFF41;
    private const int USAGE = 0x4458;
    private const int DEVICE_SCAN_INTERVAL_MS = 1000;

    private static readonly MaimollerDeviceHandle?[] _devices = new MaimollerDeviceHandle?[2];
    private static Thread? _scanThread;
    private static volatile bool _shutdown;
    private static MaimollerInitMode _initMode = MaimollerInitMode.P1P2;

    public static void Initialize(MaimollerInitMode mode = MaimollerInitMode.P1P2)
    {
        if (Volatile.Read(ref _scanThread) != null)
            return;

        _shutdown = false;
        _initMode = mode;

        var newThread = new Thread(DeviceScanThread)
        {
            IsBackground = true,
            Name = "Maimoller Scanner",
            Priority = ThreadPriority.AboveNormal
        };

        if (Interlocked.CompareExchange(ref _scanThread, newThread, null) == null)
        {
            newThread.Start();
            ScanAndOpenDevices();
        }
    }

    public static bool Read(int player, MaimollerInputReport report)
    {
        if (player is < 0 or > 1 || report is null)
            return false;

        var device = Volatile.Read(ref _devices[player]);
        if (device is not { IsConnected: true })
            return false;

        long data = device.GetLatestInputReport();
        
        // byte 1-5: touches[0-4], byte 6: playerBtn, byte 7: systemBtn
        report.touches[0] = (byte)(data >> 8);
        report.touches[1] = (byte)(data >> 16);
        report.touches[2] = (byte)(data >> 24);
        report.touches[3] = (byte)(data >> 32);
        report.touches[4] = (byte)(data >> 40);
        report.playerBtn = (byte)(data >> 48);
        report.systemBtn = (byte)(data >> 56);
        return true;
    }

    public static bool Write(int player, MaimollerOutputReport report)
    {
        if (player is < 0 or > 1 || report is null)
            return false;

        var device = Volatile.Read(ref _devices[player]);
        if (device is not { IsConnected: true })
            return false;

        var data = new byte[31];
        Array.Copy(report.buttonColors, 0, data, 0, 24);
        data[24] = report.circleBrightness;
        data[25] = report.bodyBrightness;
        data[26] = report.sideBrightness;
        Array.Copy(report.billboardColor, 0, data, 27, 3);
        data[30] = (byte)report.indicators;
        
        device.QueueOutputReport(data);
        return true;
    }

    public static bool IsConnected(int player)
    {
        if (player is < 0 or > 1)
            return false;

        var device = Volatile.Read(ref _devices[player]);
        return device is { IsConnected: true };
    }

    public static void Shutdown()
    {
        _shutdown = true;

        for (int i = 0; i < _devices.Length; i++)
        {
            var device = Interlocked.Exchange(ref _devices[i], null);
            device?.Dispose();
        }

        _scanThread?.Join(2000);
        _scanThread = null;
    }

    private static void DeviceScanThread()
    {
        while (!_shutdown)
        {
            Thread.Sleep(DEVICE_SCAN_INTERVAL_MS);

            try
            {
                bool needRescan = false;
                for (int i = 0; i < _devices.Length; i++)
                {
                    var device = Volatile.Read(ref _devices[i]);
                    if (device is { IsConnected: false })
                    {
                        needRescan = true;
                        break;
                    }
                }

                if (needRescan)
                    ScanAndOpenDevices();
            }
            catch { }
        }
    }

    private static void ScanAndOpenDevices()
    {
        var allDevices = HidDevices.Enumerate(VID, PID)
            .Where(d => (ushort)d.Capabilities.UsagePage == USAGE_PAGE 
                     && (ushort)d.Capabilities.Usage == USAGE)
            .OrderBy(d => d.DevicePath, StringComparer.Ordinal)
            .ToList();

        int[] initIndices = _initMode switch
        {
            MaimollerInitMode.P1P2 => [0, 1],
            MaimollerInitMode.P1 => [0],
            MaimollerInitMode.P2 => [1],
            _ => throw new InvalidOperationException($"Invalid init mode: {_initMode}")
        };

        int assignedCount = 0;

        foreach (var hidDevice in allDevices)
        {
            if (assignedCount >= initIndices.Length)
                break;

            bool alreadyOpen = false;
            for (int i = 0; i < _devices.Length; i++)
            {
                var device = Volatile.Read(ref _devices[i]);
                if (device is { IsConnected: true } && device.DevicePath == hidDevice.DevicePath)
                {
                    alreadyOpen = true;
                    break;
                }
            }

            if (alreadyOpen)
            {
                assignedCount++;
                continue;
            }

            int playerIndex = initIndices[assignedCount];
            var currentDevice = Volatile.Read(ref _devices[playerIndex]);

            if (currentDevice is { IsConnected: true })
                continue;

            var newDevice = new MaimollerDeviceHandle(hidDevice, playerIndex);
            if (!newDevice.Open())
            {
                newDevice.Dispose();
                continue;
            }

            var oldDevice = Interlocked.CompareExchange(ref _devices[playerIndex], newDevice, currentDevice);
            if (oldDevice == currentDevice)
            {
                oldDevice?.Dispose();
                assignedCount++;
            }
            else
            {
                newDevice.Dispose();
            }
        }
    }
}

