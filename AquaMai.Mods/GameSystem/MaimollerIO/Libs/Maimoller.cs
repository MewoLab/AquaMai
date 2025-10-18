#nullable enable

using System;
using System.Linq;
using HidLibrary;
using MelonLoader;

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

    private static readonly MaimollerDeviceHandle?[] _devices = new MaimollerDeviceHandle?[2];
    private static bool _initialized = false;

    public static void Initialize(MaimollerInitMode mode = MaimollerInitMode.P1P2)
    {
        if (_initialized) return;
        _initialized = true;

        var allDevices = HidDevices.Enumerate(VID, PID)
            .Where(d => (ushort)d.Capabilities.UsagePage == USAGE_PAGE 
                     && (ushort)d.Capabilities.Usage == USAGE)
            .OrderBy(d => d.DevicePath, StringComparer.Ordinal)
            .ToList();

        int[] initIndices = mode switch
        {
            MaimollerInitMode.P1P2 => [0, 1],
            MaimollerInitMode.P1 => [0],
            MaimollerInitMode.P2 => [1],
            _ => throw new InvalidOperationException($"Invalid init mode: {mode}")
        };

        int assignedCount = 0;

        foreach (var hidDevice in allDevices)
        {
            if (assignedCount >= initIndices.Length)
                break;

            int playerIndex = initIndices[assignedCount];

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

    public static bool Read(int player, MaimollerInputReport report)
    {
        var device = _devices[player];
        if (device is not { IsConnected: true }) return false;

        long data = device.InputData;

        // byte 1-5: touches[0-4], byte 6: playerBtn, byte 7: systemBtn
        report.touches[0] = (byte)(data >> 8);
        report.touches[1] = (byte)(data >> 16);
        report.touches[2] = (byte)(data >> 24);
        report.touches[3] = (byte)(data >> 32);
        report.touches[4] = (byte)(data >> 40);
        report.playerBtn = (byte)(data >> 48);
        report.systemBtn = (byte)(data >> 56);
        // Is this really faster than Marshal?

        return true;
    }

    public static bool Write(int player, MaimollerOutputReport report)
    {
        var device = _devices[player];
        if (device is not { IsConnected: true }) return false;

        var outputBuffer = device.outputBuffer;
        outputBuffer[0] = 1;
        Array.Copy(report.buttonColors, 0, outputBuffer, 1, 24);
        outputBuffer[25] = report.circleBrightness;
        outputBuffer[26] = report.bodyBrightness;
        outputBuffer[27] = report.sideBrightness;
        Array.Copy(report.billboardColor, 0, outputBuffer, 28, 3);
        outputBuffer[31] = (byte)report.indicators;
        // Is this really faster than Marshal?

        device.SetOutputPending();
        return true;
    }

    public static bool IsConnected(int player)
    {
        var device = _devices[player];
        return device is { IsConnected: true };
    }
}

