using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Backeddy.Core;

namespace Backeddy.Service;

/// <summary>
/// Canal local com o app. O serviço roda como sistema; qualquer usuário conectado ao PC pode perguntar
/// a situação (por enquanto só leitura).
/// </summary>
public sealed class PipeServer(StatusStore store, ILogger<PipeServer> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        Directory.CreateDirectory(AppInfo.LogDir);
        log.LogInformation("Backeddy {Version} iniciado", AppInfo.Version);
        while (!stop.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = NamedPipeServerStreamAcl.Create(AppInfo.PipeName, PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                    4096, 4096, Security());
                await pipe.WaitForConnectionAsync(stop);
                var conn = pipe;
                pipe = null;
                _ = Task.Run(() => Serve(conn, stop), stop);   // atende e volta a esperar o próximo
            }
            catch (OperationCanceledException) { break; }
            catch (Exception e)
            {
                log.LogWarning(e, "canal do app");
                await Task.Delay(1000, stop).ContinueWith(_ => { });
            }
            finally { pipe?.Dispose(); }
        }
    }

    async Task Serve(NamedPipeServerStream conn, CancellationToken stop)
    {
        using (conn)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var req = await Ipc.ReadAsync<IpcRequest>(conn, timeout.Token);
                switch (req?.Cmd)
                {
                    case "status":
                        await Ipc.WriteAsync(conn, store.Get(), timeout.Token);
                        break;
                    default:
                        await Ipc.WriteAsync(conn, new { error = "unknown command" }, timeout.Token);
                        break;
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException) { }
        }
    }

    // usuários autenticados leem e escrevem no canal; sistema e administradores controlam
    static PipeSecurity Security()
    {
        var s = new PipeSecurity();
        s.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        s.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        s.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        return s;
    }
}
