#nullable enable

using System;
using System.Threading;
using HidLibrary;
using MelonLoader;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public sealed class MaimollerDeviceHandle(HidDevice hidDevice, int playerIndex) : IDisposable
{
    private const int HID_BUFFER_SIZE = 64;

    private readonly HidDevice _hidDevice = hidDevice ?? throw new ArgumentNullException(nameof(hidDevice));
    private readonly int _playerIndex = playerIndex;
    private readonly AutoResetEvent _writeEvent = new(false);
    private readonly CancellationTokenSource _cts = new();

    private MaimollerIOThread? _readThread;
    private MaimollerIOThread? _writeThread;

    private long _inputData;
    public long InputData => _inputData;
    public byte[] outputBuffer = new byte[HID_BUFFER_SIZE];
    private byte[] _writeBuffer = new byte[HID_BUFFER_SIZE]; // Double buffer

    public string DevicePath { get; } = hidDevice.DevicePath;
    public bool IsConnected { get; private set; }

    public bool Open()
    {
        try
        {
            _hidDevice.OpenDevice();
            if (!_hidDevice.IsOpen) return false;

            IsConnected = true;
            MelonLogger.Msg($"[MaimollerIO] {_playerIndex + 1}P opened");

            _readThread = new(_cts, $"Maimoller Read {_playerIndex + 1}P", ReadThreadProc, ThreadPriority.Highest);
            _writeThread = new(_cts, $"Maimoller Write {_playerIndex + 1}P", WriteThreadProc, ThreadPriority.Normal);

            return true;
        }
        catch (Exception e)
        {
            IsConnected = false;
            MelonLogger.Error($"[MaimollerIO] {_playerIndex + 1}P open failed", e);
            return false;
        }
    }

    public void SetOutputPending() => _writeEvent.Set();

    private bool ReadThreadProc()
    {
        var readResult = _hidDevice.Read();
        if (readResult.Status != HidDeviceData.ReadStatus.Success)
        {
            MelonLogger.Error($"[MaimollerIO] {_playerIndex + 1}P read failed: {readResult.Status}");
            return false;
        }

        if (readResult.Data.Length == 0)
        {
            MelonLogger.Error($"[MaimollerIO] {_playerIndex + 1}P read data empty");
            return false;
        }

        long newData = 0;
        int maxBytes = Math.Min(readResult.Data.Length, 8);
        for (int i = 0; i < maxBytes; i++)
        {
            newData |= ((long)readResult.Data[i]) << (i * 8);
        }

        _inputData = newData;

        return true;
    }

    private bool WriteThreadProc()
    {
        _writeEvent.WaitOne(/* It doesn't accept a CancellationToken */);

        if (!IsConnected) return false;

        var previousWriteBuffer = _writeBuffer;
        _writeBuffer = outputBuffer;
        outputBuffer = previousWriteBuffer;
        if (!_hidDevice.Write(_writeBuffer))
        {
            IsConnected = false;
            MelonLogger.Error($"[MaimollerIO] {_playerIndex + 1}P write failed");
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        IsConnected = false;
        _writeEvent.Set();

        _readThread?.Dispose();
        _writeThread?.Dispose();

        try
        {
            _hidDevice?.CloseDevice();
            _hidDevice?.Dispose();
        }
        catch { }

        _writeEvent.Dispose();
        _cts.Dispose();
    }
}

