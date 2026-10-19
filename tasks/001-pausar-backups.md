# 001 — Pausar backups (1 hora ou até amanhã)

## Objetivo
A pessoa consegue pausar os backups automáticos por 1 hora ou até amanhã de manhã (ex.: está numa chamada, ou numa rede lenta) e retomar quando quiser.
A pausa sobrevive a reiniciar o PC e o serviço.

## Comportamento
- Pausa só vale para backups automáticos. "Fazer backup agora" (manual) continua funcionando e NÃO cancela a pausa.
- Pausar não interrompe um backup que já está rodando; vale a partir do próximo.
- Opções: "Por 1 hora" (agora + 1 h) e "Até amanhã de manhã" (08:00 do dia seguinte, no horário local do PC). "Retomar" cancela.
- Passado o horário, a pausa some sozinha (nenhum clique necessário) e o backup volta à rotina normal: se estiver atrasado, roda na hora.
- A tela inicial mostra o estado "Backups pausados" com "Pausado até {quando}." e o link "Retomar backups".
  Fora da pausa, mostra o link "Pausar backups…" que abre um menu com as duas opções.
- Só o usuário que configurou o Backeddy pode pausar (a verificação em `Engine.HandleAsync` já cobre todo comando, menos `Status`).
- Sem destino configurado: `Err.NotConfigured`.

## O que mudar, arquivo por arquivo
- `src/Backeddy.Core/Config.cs`: em `BackupConfig`, nova propriedade `public DateTimeOffset? PausedUntil { get; init; }`.
- `src/Backeddy.Core/Models.cs`:
  - `public enum PauseKind { Resume, Hour, Tomorrow }`
  - `public sealed record PauseRequest(PauseKind Kind);`
  - em `WaitReason` (o enum já existe), adicionar o valor `Paused` no FIM da lista.
  - em `ServiceStatus`, nova propriedade `public DateTimeOffset? PausedUntil { get; init; }`.
- `src/Backeddy.Core/Ipc.cs`: em `Cmd`, `public const string Pause = "pause.set";`.
- `src/Backeddy.Core/Schedule.cs`:
  - `Decision` ganha o valor `Paused` (no fim).
  - Novo `public static DateTimeOffset? PauseUntil(PauseKind kind, DateTimeOffset now)`:
    `Resume` → `null`; `Hour` → `now + 1 hora`; `Tomorrow` → `new DateTimeOffset(now.Date.AddDays(1).AddHours(8), now.Offset)`.
  - `Decide`: logo depois da checagem `NotConfigured`, `if (!manual && c.PausedUntil is { } until && now < until) return Decision.Paused;`.
  - `Next`: se `c.PausedUntil` existe e é maior que o resultado calculado (ou o resultado é `null`), devolver `PausedUntil`.
    Pausa no passado não interfere (compare só com o valor calculado; o `Decide` já ignora pausa vencida).
- `src/Backeddy.Service/Engine.cs`:
  - `HandleAsync`: novo `case Cmd.Pause:` — `Err.NotConfigured` se não houver destino; calcular `Schedule.PauseUntil(Ipc.From<PauseRequest>(req.Data)?.Kind ?? PauseKind.Resume, DateTimeOffset.Now)`;
    gravar com `Update(c => c with { PausedUntil = ... })` (o mesmo método usado em `SetExclusions`); `wake.Release()`; `Ipc.Success()`.
  - `Status()`: preencher `PausedUntil` só se `cfg.PausedUntil` for no futuro; caso contrário `null`.
  - `TickAsync`: no `switch` que mapeia `Decision` para `WaitReason`, `Decision.Paused => WaitReason.Paused`.
- `src/Backeddy.App/MainWindow.Home.cs`:
  - Dois novos campos de `HyperlinkButton`: um "Pausar backups…" com `Flyout = new MenuFlyout` contendo dois itens criados com `Ui.MenuItem` (1 hora → `PauseKind.Hour`, amanhã → `PauseKind.Tomorrow`), e um "Retomar backups" (→ `PauseKind.Resume`).
    Os dois chamam `ServiceClient.CallAsync(Cmd.Pause, new PauseRequest(kind))` (confira em `ServiceClient.cs` a assinatura real de `CallAsync` com dados; siga o uso de `Cmd.SetExclusions` em `MainWindow.Exclusions.cs`).
  - Colocar os dois links no `Ui.Column(6, ...)` dos links, junto de "O que entra no backup". Em `UpdateHome`, mostrar um ou outro conforme `st.PausedUntil is not null`.
  - Em `UpdateHome`, no `switch` do título/detalhe, antes do `BackupState.Waiting` genérico, acrescentar
    `BackupState.Waiting when st.Wait == WaitReason.Paused => (Strings.T("statePaused"), Strings.F("pausedUntil", Format.When(st.PausedUntil!.Value, now)))`.
- `src/Backeddy.Core/Strings.cs`: as chaves abaixo, na tabela `Table`, perto das chaves de estado (`stateWaiting` etc.).

## Textos novos (pt / en / es)
- `pauseBackups`: "Pausar backups…" / "Pause backups…" / "Pausar backups…"
- `pauseHour`: "Por 1 hora" / "For 1 hour" / "Por 1 hora"
- `pauseTomorrow`: "Até amanhã de manhã" / "Until tomorrow morning" / "Hasta mañana por la mañana"
- `pauseResume`: "Retomar backups" / "Resume backups" / "Reanudar backups"
- `statePaused`: "Backups pausados" / "Backups paused" / "Backups en pausa"
- `pausedUntil`: "Pausado até {0}. \"Fazer backup agora\" continua funcionando." / "Paused until {0}. \"Back up now\" still works." / "En pausa hasta {0}. \"Hacer backup ahora\" sigue funcionando."

## Testes obrigatórios (em `tests/Backeddy.Core.Tests/EngineTests.cs`, na classe `ScheduleTests`)
- `PausedBackupWaitsUntilTheEnd`: com `PausedUntil` no futuro, `Decide` automático devolve `Decision.Paused`; depois do horário, volta a `Decision.Run` (se estiver na hora).
- `ManualBackupIgnoresThePause`: com pausa ativa, `Decide` com `manual: true` devolve `Decision.Run`.
- `NextBackupRespectsThePause`: `Schedule.Next` devolve `PausedUntil` quando ele é depois do próximo horário calculado; e ignora uma pausa vencida.
- `PauseUntilComputesTheEnd`: `Resume` → `null`; `Hour` → `now + 1h`; `Tomorrow` com `now = 2026-10-17 12:00 +00:00` → `2026-10-18 08:00 +00:00`; e com `now = 2026-10-17 02:00 -03:00` → `2026-10-18 08:00 -03:00`.
- Um teste em `IpcTests` (arquivo `CoreTests.cs`) que ida-e-volta (`Ipc.Encode` / `Ipc.Decode`) um `PauseRequest(PauseKind.Tomorrow)`.
O teste existente que confere os três idiomas em `Strings.Table` tem que continuar passando.

## Fora do escopo
- Pausar por "N horas" livre, pausa por rede, ou qualquer agendamento além das duas opções.
- Mexer em `VERSION`, README, instalador, `contexto.md` (isso é do Claude).
- Mexer no Tray (ícone da bandeja).

## Pronto quando
- O CI compila e todos os testes passam, os novos e os antigos.
- Nenhum warning novo.
- `tasks/001.relatorio.md` escrito no formato do `AGENTS.md`.
