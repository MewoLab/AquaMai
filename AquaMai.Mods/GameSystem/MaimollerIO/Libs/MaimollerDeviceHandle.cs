#nullable enable

using System;
using System.Threading;
using HidLibrary;
using MelonLoader;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public sealed class MaimollerDeviceHandle(HidDevice hidDevice, int playerIndex) : IDisposable
{
    private const int HID_BUFFER_SIZE = 64;
    private const int INPUT_REPORT_SIZE = 7;

    private readonly HidDevice _hidDevice = hidDevice ?? throw new ArgumentNullException(nameof(hidDevice));
    private readonly int _playerIndex = playerIndex;
    private readonly AutoResetEvent _writeEvent = new(false);

    private Thread? _readThread;
    private Thread? _writeThread;

    private long _inputData;
    public byte[] outputBuffer = new byte[HID_BUFFER_SIZE];

    public string DevicePath { get; } = hidDevice.DevicePath;
    public bool IsConnected { get; private set; }

    public bool Open()
    {
        try
        {
            _hidDevice.OpenDevice();
            if (!_hidDevice.IsOpen)
                return false;

            IsConnected = true;

            MelonLogger.Msg($"[Maimoller] {_playerIndex + 1}P opened");

            _readThread = new Thread(ReadThreadProc)
            {
                IsBackground = true,
                Name = $"Maimoller Read {_playerIndex + 1}P",
                Priority = ThreadPriority.Highest
            };
            _readThread.Start();

            _writeThread = new Thread(WriteThreadProc)
            {
                IsBackground = true,
                Name = $"Maimoller Write {_playerIndex + 1}P",
            };
            _writeThread.Start();

            return true;
        }
        catch
        {
            IsConnected = false;
            MelonLogger.Warning($"[Maimoller] {_playerIndex + 1}P open failed");
            return false;
        }
    }

    public long GetLatestInputReport() => Interlocked.Read(ref _inputData);

    public void SetOutputPending() => _writeEvent.Set();

    private void ReadThreadProc()
    {
        MelonLogger.Msg($"[Maimoller] {_playerIndex + 1}P read thread started");

        while (IsConnected)
        {
            var readResult = _hidDevice.Read();
            if (readResult.Status != HidDeviceData.ReadStatus.Success)
            {
                MelonLogger.Warning($"[Maimoller] {_playerIndex + 1}P read failed: {readResult.Status}");
                continue;
            }

            if (readResult.Data == null || readResult.Data.Length == 0)
            {
                MelonLogger.Warning($"[Maimoller] {_playerIndex + 1}P read data is null or empty");
                continue;
            }

            long newData = 0;
            int maxBytes = Math.Min(readResult.Data.Length, INPUT_REPORT_SIZE);
            for (int i = 0; i < maxBytes; i++)
            {
                newData |= ((long)readResult.Data[i]) << ((i + 1) * 8);
            }

            Interlocked.Exchange(ref _inputData, newData);
        }
    }

    private void WriteThreadProc()
    {
        var writeBuffer = new byte[HID_BUFFER_SIZE]; // Double buffer

        while (IsConnected)
        {
            _writeEvent.WaitOne();

            if (!IsConnected)
                break;

            writeBuffer = Interlocked.Exchange(ref outputBuffer, writeBuffer);
            if (!_hidDevice.Write(writeBuffer))
            {
                IsConnected = false;
                MelonLogger.Error($"[Maimoller] {_playerIndex + 1}P write failed");
                break;
            }
        }
    }

    public void Dispose()
    {
        IsConnected = false;
        _writeEvent.Set();

        _readThread?.Join(500);
        _writeThread?.Join(500);

        try
        {
            _hidDevice?.CloseDevice();
            _hidDevice?.Dispose();
        }
        catch { }

        _writeEvent.Dispose();
    }
}

