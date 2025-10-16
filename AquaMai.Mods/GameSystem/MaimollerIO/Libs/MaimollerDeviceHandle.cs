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
    private readonly AutoResetEvent _writeEvent = new(false);

    private Thread? _readThread;
    private Thread? _writeThread;
    private volatile bool _running;

    private long _inputData;
    public byte[] outputBuffer = new byte[HID_BUFFER_SIZE];

    public string DevicePath { get; }
    public bool IsConnected { get; private set; }

    public MaimollerDeviceHandle(HidDevice hidDevice, int playerIndex)
    {
        _hidDevice = hidDevice ?? throw new ArgumentNullException(nameof(hidDevice));
        _playerIndex = playerIndex;
        DevicePath = hidDevice.DevicePath;
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
            MelonLogger.Warning($"[Maimoller] {_playerIndex + 1}P open failed");
            return false;
        }
    }

    public long GetLatestInputReport()
    {
        return Interlocked.Read(ref _inputData);
    }

    public void SetOutputPending() => _writeEvent.Set();

    private void ReadThreadProc()
    {
        MelonLogger.Msg($"[Maimoller] {_playerIndex + 1}P read thread started");

        while (_running && IsConnected)
        {
            try
            {
                var report = _hidDevice.ReadReport();

                if (report == null)
                {
                    MelonLogger.Warning($"[Maimoller] {_playerIndex + 1}P ReadReport returned null");
                    continue;
                }

                if (report.Data == null || report.Data.Length == 0)
                {
                    MelonLogger.Warning($"[Maimoller] {_playerIndex + 1}P report.Data is null or empty");
                    continue;
                }

                long newData = report.ReportId;
                int maxBytes = Math.Min(report.Data.Length, INPUT_REPORT_SIZE);
                for (int i = 0; i < maxBytes; i++)
                {
                    newData |= ((long)report.Data[i]) << ((i + 1) * 8);
                }

                Interlocked.Exchange(ref _inputData, newData);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Maimoller] {_playerIndex + 1}P read error: {ex.Message}");
                IsConnected = false;
                break;
            }
        }
    }

    private void WriteThreadProc()
    {
        var writeBuffer = new byte[HID_BUFFER_SIZE]; // Double buffer

        while (_running && IsConnected)
        {
            try
            {
                _writeEvent.WaitOne();

                if (!_running || !IsConnected)
                    break;

                writeBuffer = Interlocked.Exchange(ref outputBuffer, writeBuffer);
                if (!_hidDevice.Write(writeBuffer))
                {
                    IsConnected = false;
                    MelonLogger.Error($"[Maimoller] {_playerIndex + 1}P write failed");
                    break;
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                MelonLogger.Error($"[Maimoller] {_playerIndex + 1}P write error: {ex.Message}");
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

