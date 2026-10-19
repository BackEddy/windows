# 004 — Conferência periódica dos backups

## Objetivo
Backup que ninguém confere só mostra que está quebrado na hora de restaurar. Uma vez por semana, depois de um backup bem-sucedido, o serviço
pede ao kopia que confira os dados guardados (estrutura inteira + uma amostra de 1% dos arquivos baixados e checados). Se achar problema,
a pessoa vê um aviso na tela inicial. Se estiver tudo certo, a tela inicial mostra quando foi a última conferência.

## Comportamento
- Quando conferir: no fim de um backup que deu certo (`error is null`), se `Schedule.VerifyDue(cfg, agora)` for verdadeiro. Nunca em backup que falhou.
- `VerifyDue`: falso sem destino ou sem `LastSuccess`; senão verdadeiro se `LastVerify` é nulo ou se passou o intervalo desde `LastVerify`.
  Intervalo: 7 dias; mas 1 dia se a última conferência falhou (`VerifyFailed`), para o aviso sumir logo depois de um conserto.
- Comando: `kopia snapshot verify --verify-files-percent=1` (um único argumento `--verify-files-percent=1`), com limite de 2 horas.
- Resultado:
  - saiu com sucesso: `LastVerify = agora`, `VerifyFailed = false`.
  - falhou e o erro classificado (`r.Error`) é `Err.Unknown`: `LastVerify = agora`, `VerifyFailed = true`, registrar no log.
  - falhou por rede, senha, certificado, espaço, conexão caída (qualquer outro erro conhecido): NÃO é problema dos dados; não mexe em `LastVerify` nem em `VerifyFailed` (tenta de novo no próximo backup).
  - estourou o limite de 2 horas: `LastVerify = agora` (tenta de novo só na semana seguinte), sem marcar falha, registrar no log.
- A conferência roda dentro do mesmo bloco do backup (com a trava do repositório e com o PC mantido acordado, como a tarefa 003). Ela NUNCA pode fazer o backup virar erro: qualquer exceção ali é capturada, registrada e ignorada, exceto o cancelamento do serviço (`stop`), que continua subindo.
- Esquecer o destino (`ForgetAsync`) já zera tudo, porque recria a configuração.

## O que mudar, arquivo por arquivo
- `src/Backeddy.Core/Config.cs`: em `BackupConfig`, `public DateTimeOffset? LastVerify { get; init; }` e `public bool VerifyFailed { get; init; }`, perto de `LastSuccess`.
- `src/Backeddy.Core/Schedule.cs`:
  - `public static readonly TimeSpan VerifyEvery = TimeSpan.FromDays(7);` e `public static readonly TimeSpan VerifyRetryAfterFailure = TimeSpan.FromDays(1);`
  - `public static bool VerifyDue(BackupConfig c, DateTimeOffset now)` conforme acima.
- `src/Backeddy.Core/Kopia.cs`: em `KopiaCli`, `public static string[] Verify() => new[] { "snapshot", "verify", "--verify-files-percent=1" };` (perto de `SnapshotList`).
- `src/Backeddy.Core/Models.cs`: em `ServiceStatus`, `public DateTimeOffset? LastVerify { get; init; }` e `public bool VerifyFailed { get; init; }`.
- `src/Backeddy.Service/Engine.cs`:
  - em `Status()`, `LastVerify = cfg.LastVerify, VerifyFailed = cfg.VerifyFailed,` junto dos outros campos.
  - novo método `async Task VerifyAsync(CancellationToken ct)`, chamado em `BackupAsync` logo depois de `if (!r.Ok) error = r.Error;`, assim: `else await VerifyAsync(stop);` (ou seja, só quando o snapshot deu certo). Dentro dele:
```csharp
    /// <summary>Confere os dados guardados uma vez por semana; nunca faz o backup virar erro.</summary>
    async Task VerifyAsync(CancellationToken ct)
    {
        if (!Schedule.VerifyDue(cfg, DateTimeOffset.Now)) return;
        try
        {
            var r = await kopia.RunAsync(KopiaCli.Verify(), secrets.RepoPassword, ct, TimeSpan.FromHours(2));
            var now = DateTimeOffset.Now;
            if (r.Ok) Update(c => c with { LastVerify = now, VerifyFailed = false });
            else if (r.Error == Err.Unknown)
            {
                log.LogWarning("conferência achou problema");
                Update(c => c with { LastVerify = now, VerifyFailed = true });
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            log.LogWarning("conferência passou de 2 horas; tenta de novo na semana que vem");
            var now = DateTimeOffset.Now;
            Update(c => c with { LastVerify = now });
        }
        catch (Exception e) { log.LogError(e, "conferência"); }
    }
```
  (o `KopiaResult.Error` já classifica a saída do kopia; confira no código que o nome `Err.Unknown` é esse.)
- `src/Backeddy.App/MainWindow.Home.cs`:
  - campo `TextBlock? homeVerified;`; em `ShowHome`, `homeVerified = Ui.Text("");` e `Fact(3, Strings.T("lastVerify"), homeVerified);` depois do `Fact(2, ...)`.
  - em `UpdateHome`, `homeVerified!.Text = st.LastVerify is { } v ? Format.When(v, now) + (st.VerifyFailed ? " · " + Strings.T("verifyFailedShort") : "") : "—";`
  - em `UpdateHome`, logo depois da faixa de atraso (`overdue`) e antes do `kopiaMissing`: se `st.VerifyFailed`, adicionar `Ui.Bar(InfoBarSeverity.Warning, Strings.T("verifyFailedTitle"), Strings.T("verifyFailedText"))`.
- `src/Backeddy.Core/Strings.cs`: as chaves abaixo, perto de `statePaused`.

## Textos novos (pt / en / es)
- `lastVerify`: "Última conferência" / "Last check" / "Última comprobación"
- `verifyFailedShort`: "achou problema" / "found a problem" / "encontró un problema"
- `verifyFailedTitle`: "A conferência do backup achou um problema" / "The backup check found a problem" / "La comprobación del backup encontró un problema"
- `verifyFailedText`: "Alguns dados guardados podem estar danificados. Não apague o destino; faça um novo backup e, se o aviso voltar, peça ajuda." / "Some stored data may be damaged. Don't erase the destination; run a new backup and, if the warning returns, ask for help." / "Algunos datos guardados pueden estar dañados. No borres el destino; haz un nuevo backup y, si el aviso vuelve, pide ayuda."

## Testes obrigatórios
- `ScheduleTests` (`tests/Backeddy.Core.Tests/EngineTests.cs`), `VerifyDue`: sem destino → falso; sem `LastSuccess` → falso; `LastVerify` nulo → verdadeiro; 6 dias depois → falso; 7 dias depois → verdadeiro; com `VerifyFailed`, 23 horas depois → falso e 24 horas → verdadeiro.
- Teste de `KopiaCli.Verify()`: os três argumentos exatos, na ordem. Procure onde os outros comandos do `KopiaCli` são testados e siga o padrão.
- `IpcTests` (`tests/Backeddy.Core.Tests/CoreTests.cs`): `ServiceStatus { LastVerify = ..., VerifyFailed = true }` vai e volta.
- Config: ida e volta de `LastVerify` e `VerifyFailed` (padrão do teste que a tarefa 003 acrescentou); arquivo antigo sem os campos carrega como nulo e falso.

## Fora do escopo
Botão "Conferir agora", notificação perto do relógio, restauração, `VERSION`, README, `contexto.md`. Não mudar a rede nem o `KopiaRunner`.

## Pronto quando
- `dotnet test tests/Backeddy.Core.Tests` passa, com o resultado no relatório.
- O CI compila App e Service sem erro.
- `tasks/004.relatorio.md` escrito no formato do `AGENTS.md`.
