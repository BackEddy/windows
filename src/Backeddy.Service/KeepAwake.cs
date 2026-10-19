using System.Runtime.InteropServices;

namespace Backeddy.Service;

/// <summary>Impede o Windows de suspender o PC enquanto existir (um backup rodando). Fechar a tampa ainda suspende.</summary>
sealed class KeepAwake : IDisposable
{
    const uint EsContinuous = 0x80000000;
    const uint EsSystemRequired = 0x00000001;

    [DllImport("kernel32.dll")]
    static extern uint SetThreadExecutionState(uint flags);

    readonly ManualResetEventSlim done = new(false);
    readonly Thread thread;

    KeepAwake()
    {
        // o estado vale por thread e some quando ela termina: uma thread própria, parada até o Dispose
        thread = new Thread(() =>
        {
            SetThreadExecutionState(EsContinuous | EsSystemRequired);
            done.Wait();
            SetThreadExecutionState(EsContinuous);
        }) { IsBackground = true, Name = "Backeddy.KeepAwake" };
        thread.Start();
    }

    public static IDisposable Begin() => new KeepAwake();

    public void Dispose()
    {
        done.Set();
        thread.Join(TimeSpan.FromSeconds(2));
    }
}
