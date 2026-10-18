using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Backeddy.Core;

namespace Backeddy.App;

/// <summary>Conversa com o serviço pelo canal local.</summary>
static class ServiceClient
{
    public static async Task<ServiceStatus?> StatusAsync()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", AppInfo.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await pipe.ConnectAsync(cts.Token);
            await Ipc.WriteAsync(pipe, new IpcRequest("status"), cts.Token);
            return await Ipc.ReadAsync<ServiceStatus>(pipe, cts.Token);
        }
        catch (Exception)
        {
            return null;   // serviço parado ou não instalado
        }
    }
}
