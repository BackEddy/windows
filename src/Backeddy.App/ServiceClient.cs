using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Backeddy.Core;

namespace Backeddy.App;

/// <summary>Conversa com o serviço pelo canal local.</summary>
static class ServiceClient
{
    /// <summary>Manda um comando; null se o serviço não respondeu (parado ou não instalado).</summary>
    public static async Task<IpcResponse?> CallAsync(IpcRequest req, TimeSpan? timeout = null)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", AppInfo.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
            await pipe.ConnectAsync(cts.Token);
            await Ipc.WriteAsync(pipe, req, cts.Token);
            return await Ipc.ReadAsync<IpcResponse>(pipe, cts.Token);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Task<IpcResponse?> CallAsync(string cmd, TimeSpan? timeout = null) => CallAsync(new IpcRequest(cmd), timeout);

    public static Task<IpcResponse?> CallAsync<T>(string cmd, T data, TimeSpan? timeout = null) => CallAsync(Ipc.Request(cmd, data), timeout);

    public static async Task<ServiceStatus?> StatusAsync()
    {
        var r = await CallAsync(Cmd.Status, TimeSpan.FromSeconds(3));
        return r?.Ok == true ? Ipc.From<ServiceStatus>(r.Data) : null;
    }

    /// <summary>Erro legível de uma resposta (ou do serviço fora do ar).</summary>
    public static string ErrorText(IpcResponse? r) => r is null ? Strings.T("serviceDown") : Format.Error(r.Error);

    // configurar, listar e restaurar podem demorar
    public static readonly TimeSpan Long = TimeSpan.FromMinutes(10);
}
