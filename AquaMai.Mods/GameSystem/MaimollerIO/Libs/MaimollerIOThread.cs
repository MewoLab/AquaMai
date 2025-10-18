using System;
using System.Threading;
using MelonLoader;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public class MaimollerIOThread : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Thread _thread;
    private readonly string _name;
    private readonly Func<bool> _ioLoopTask;

    public MaimollerIOThread(CancellationTokenSource cts, string name, Func<bool> ioLoopTask, ThreadPriority priority = ThreadPriority.Normal)
    {
        _cts = cts;
        _thread = new Thread(ThreadProc) { IsBackground = true, Name = name, Priority = priority };
        _name = name;
        _ioLoopTask = ioLoopTask;
        _thread.Start();
    }

    private void ThreadProc()
    {
        MelonLogger.Msg($"[MaimollerIO] Thread {_name} started");
        while (!_cts.IsCancellationRequested && _ioLoopTask())
        { }
        _cts.Cancel(); // Also cancel the paired thread
        MelonLogger.Msg($"[MaimollerIO] Thread {_name} stopped");
    }

    public void Dispose() => _thread.Abort();
}
