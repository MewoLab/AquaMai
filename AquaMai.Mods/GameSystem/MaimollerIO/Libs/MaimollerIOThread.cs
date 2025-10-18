using System;
using System.Threading;

namespace AquaMai.Mods.GameSystem.MaimollerIO.Libs;

public class MaimollerIOThread : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Thread _thread;
    private readonly Func<bool> _ioLoopTask;

    public MaimollerIOThread(CancellationTokenSource cts, string name, Func<bool> ioLoopTask, ThreadPriority priority = ThreadPriority.Normal)
    {
        _cts = cts;
        _thread = new Thread(ThreadProc) { IsBackground = true, Name = name, Priority = priority };
        _ioLoopTask = ioLoopTask;
    }

    private void ThreadProc()
    {
        while (!_cts.IsCancellationRequested && _ioLoopTask())
        { }
        _cts.Cancel(); // Also cancel the paired thread
    }

    public void Dispose() => _thread.Abort();
}
