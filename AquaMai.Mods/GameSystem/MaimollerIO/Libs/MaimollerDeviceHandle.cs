#nullable enable

using System;
using System.Threading;
using HidLibrary;
using MelonLoader;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public sealed class MaimollerDeviceHandle : IDisposable
{
    private const int HID_BUFFER_SIZE = 64;
    private const int INPUT_REPORT_SIZE = 7;

    private readonly HidDevice _hidDevice;
    private readonly int _playerIndex;
    private readonly object _readLock = new();
    private readonly object _writeLock = new();
    private readonly AutoResetEvent _writeEvent = new(false);

    private Thread? _readThread;
    private Thread? _writeThread;
    private volatile bool _running;

    private readonly byte[] _inputBuffer1 = new byte[HID_BUFFER_SIZE];
    private readonly byte[] _inputBuffer2 = new byte[HID_BUFFER_SIZE];
    private byte[] _currentInputBuffer;

    private readonly byte[] _outputBuffer = new byte[HID_BUFFER_SIZE];
    private volatile bool _outputPending;

    public string DevicePath { get; }
    public bool IsConnected { get; private set; }

    public MaimollerDeviceHandle(HidDevice hidDevice, int playerIndex)
    {
        _hidDevice = hidDevice ?? throw new ArgumentNullException(nameof(hidDevice));
        _playerIndex = playerIndex;
        DevicePath = hidDevice.DevicePath;
        _currentInputBuffer = _inputBuffer1;
    }

    public bool Open()
    {
        try
        {
            _hidDevice.OpenDevice();
            if (!_hidDevice.IsOpen)
                return false;

            IsConnected = true;
            _running = true;

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
                Priority = ThreadPriority.AboveNormal
            };
            _writeThread.Start();

            return true;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }

    public byte[] GetLatestInputReport()
    {
        lock (_readLock)
        {
            var result = new byte[INPUT_REPORT_SIZE];
            Array.Copy(_currentInputBuffer, 1, result, 0, INPUT_REPORT_SIZE);
            return result;
        }
    }

    public void QueueOutputReport(byte[] data)
    {
        if (data.Length != 31)
            return;

        lock (_writeLock)
        {
            _outputBuffer[0] = 1;
            Array.Copy(data, 0, _outputBuffer, 1, 31);
            Array.Clear(_outputBuffer, 32, HID_BUFFER_SIZE - 32);
            _outputPending = true;
        }
        
        _writeEvent.Set();
    }

    private void ReadThreadProc()
    {
        while (_running && IsConnected)
        {
            try
            {
                var report = _hidDevice.ReadReport();
                if (report?.Data is { Length: > 0 })
                {
                    lock (_readLock)
                    {
                        _currentInputBuffer = _currentInputBuffer == _inputBuffer1 
                            ? _inputBuffer2 
                            : _inputBuffer1;

                        _currentInputBuffer[0] = report.ReportId;
                        Array.Copy(report.Data, 0, _currentInputBuffer, 1, 
                            Math.Min(report.Data.Length, HID_BUFFER_SIZE - 1));
                    }
                }
            }
            catch
            {
                IsConnected = false;
                break;
            }
        }
    }

    private void WriteThreadProc()
    {
        var writeBuffer = new byte[HID_BUFFER_SIZE];

        while (_running && IsConnected)
        {
            try
            {
                _writeEvent.WaitOne();

                if (!_running || !IsConnected)
                    break;

                lock (_writeLock)
                {
                    if (!_outputPending)
                        continue;

                    Array.Copy(_outputBuffer, writeBuffer, HID_BUFFER_SIZE);
                    _outputPending = false;
                }

                if (!_hidDevice.Write(writeBuffer))
                {
                    IsConnected = false;
                    break;
                }
            }
            catch
            {
                IsConnected = false;
                break;
            }
        }
    }

    public void Dispose()
    {
        _running = false;
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

