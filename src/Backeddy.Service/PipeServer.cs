using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Backeddy.Core;

namespace Backeddy.Service;

/// <summary>
/// Canal local com o app. O serviço roda como sistema; qualquer usuário conectado ao PC pode perguntar
/// a situação, mas só quem configurou o Backeddy pode mudar algo (o Engine confere o nome de quem chamou).
/// </summary>
public sealed class PipeServer(Engine engine, ILogger<PipeServer> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = NamedPipeServerStreamAcl.Create(AppInfo.PipeName, PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                    65536, 65536, Security());
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
                if (req is null) return;
                string? caller = null;
                try { caller = conn.GetImpersonationUserName(); } catch (Exception) { }
                // configurar e listar podem demorar (rede, HD lento): até 10 minutos
                using var work = CancellationTokenSource.CreateLinkedTokenSource(stop);
                work.CancelAfter(TimeSpan.FromMinutes(10));
                var resp = await engine.HandleAsync(req, caller, work.Token);
                await Ipc.WriteAsync(conn, resp, work.Token);
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
