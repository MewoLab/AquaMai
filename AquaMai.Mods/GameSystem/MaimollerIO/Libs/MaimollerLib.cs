#nullable enable

using System;
using System.Linq;
using System.Runtime.InteropServices;
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

    private static readonly object _lock = new();
    private static readonly MaimollerDeviceHandle?[] _devices = new MaimollerDeviceHandle?[2];
    private static Thread? _scanThread;
    private static volatile bool _shutdown;
    private static MaimollerInitMode _initMode = MaimollerInitMode.P1P2;

    public static void Initialize(MaimollerInitMode mode = MaimollerInitMode.P1P2)
    {
        lock (_lock)
        {
            if (_scanThread != null)
                return;

            _shutdown = false;
            _initMode = mode;

            _scanThread = new Thread(DeviceScanThread)
            {
                IsBackground = true,
                Name = "Maimoller Scanner",
                Priority = ThreadPriority.AboveNormal
            };
            _scanThread.Start();

            ScanAndOpenDevices();
        }
    }

    public static bool Read(int player, MaimollerInputReport report)
    {
        if (player is < 0 or > 1 || report is null)
            return false;

        lock (_lock)
        {
            if (_devices[player] is not { IsConnected: true } device)
                return false;

            var data = device.GetLatestInputReport();
            if (data.Length < Marshal.SizeOf<MaimollerInputReport>())
                return false;

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Marshal.PtrToStructure(handle.AddrOfPinnedObject(), report);
                return true;
            }
            finally
            {
                handle.Free();
            }
        }
    }

    public static bool Write(int player, MaimollerOutputReport report)
    {
        if (player is < 0 or > 1 || report is null)
            return false;

        lock (_lock)
        {
            if (_devices[player] is not { IsConnected: true } device)
                return false;

            int size = Marshal.SizeOf<MaimollerOutputReport>();
            var data = new byte[size];
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(report, handle.AddrOfPinnedObject(), false);
                device.QueueOutputReport(data);
                return true;
            }
            finally
            {
                handle.Free();
            }
        }
    }

    public static bool IsConnected(int player)
    {
        if (player is < 0 or > 1)
            return false;

        lock (_lock)
        {
            return _devices[player] is { IsConnected: true };
        }
    }

    public static void Shutdown()
    {
        _shutdown = true;

        lock (_lock)
        {
            foreach (var device in _devices)
                device?.Dispose();
            
            Array.Clear(_devices, 0, _devices.Length);
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
                lock (_lock)
                {
                    if (_devices.Any(d => d is { IsConnected: false }))
                        ScanAndOpenDevices();
                }
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

            bool alreadyOpen = _devices.Any(d => 
                d is { IsConnected: true } && d.DevicePath == hidDevice.DevicePath);

            if (alreadyOpen)
            {
                assignedCount++;
                continue;
            }

            int playerIndex = initIndices[assignedCount];

            if (_devices[playerIndex] is not { IsConnected: true })
            {
                _devices[playerIndex]?.Dispose();
                _devices[playerIndex] = null;
                
                var newDevice = new MaimollerDeviceHandle(hidDevice, playerIndex);
                if (newDevice.Open())
                {
                    _devices[playerIndex] = newDevice;
                    assignedCount++;
                }
                else
                {
                    newDevice.Dispose();
                }
            }
        }
    }
}

