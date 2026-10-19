# B2 — Benchmark: horário silencioso (só Core)

Tarefa-padrão nº 2 para comparar modelos. Escopo fechado: o App e o Service NÃO entram.

## Objetivo
O usuário define uma janela diária em que o Backeddy não inicia backups automáticos (ex.: 22:00 às 07:00, para não pesar o PC à noite). "Fazer backup agora" continua funcionando.

## Comportamento
- Duas novas opções em `BackupConfig` (`src/Backeddy.Core/Config.cs`): `public TimeSpan? QuietStart { get; set; }` e `public TimeSpan? QuietEnd { get; set; }` (hora do dia, de 00:00 a 23:59).
- A janela só vale se as DUAS estiverem preenchidas. Com só uma ou nenhuma, não há horário silencioso.
- O início é inclusivo e o fim é exclusivo: com 22:00–07:00, às 22:00 já é silencioso e às 07:00 já não é.
- A janela pode cruzar a meia-noite (22:00–07:00) ou ficar no mesmo dia (13:00–15:00).
- A hora que conta é o relógio do PC, ou seja, a hora do dia do próprio `DateTimeOffset now` recebido (`now.TimeOfDay`), com o deslocamento que ele já traz. Não converta para UTC.
- Início igual ao fim: a janela é vazia e nunca é silencioso.
- Novo valor `Decision.Quiet` em `Schedule.cs`. Em `Schedule.Decide` a ordem das checagens passa a ser:
  1. `NotConfigured`
  2. `Paused`
  3. `NotDue`
  4. `Quiet` (só se não for manual e `now` estiver dentro da janela)
  5. `DestinationMissing`
  6. `LowBattery`
  7. `Run`
  Ou seja: o que não está na hora continua `NotDue` mesmo dentro da janela, e `manual` ignora a janela.
- Exemplos:

| Janela | Hora do PC | Resultado |
|---|---|---|
| 22:00–07:00 | 23:30 | silencioso |
| 22:00–07:00 | 07:00 | não silencioso |
| 13:00–15:00 | 14:59 | silencioso |
| 22:00–22:00 | 22:00 | silencioso (a janela cobre o dia todo) |

## O que mudar
- `Config.cs`: as duas propriedades.
- `Schedule.cs`: `Decision.Quiet`, a checagem em `Decide`, e um método público `static bool IsQuiet(BackupConfig c, DateTimeOffset now)`.
- `Strings.cs`: a chave `stateQuiet` na tabela `Table`, perto de `statePaused`: "Horário silencioso" / "Quiet hours" / "Horario silencioso".

## Testes obrigatórios
Em `ScheduleTests` (`tests/Backeddy.Core.Tests/EngineTests.cs`): janela que cruza a meia-noite, janela no mesmo dia, o fato de `manual` passar por cima, e a hora vir do relógio do `now` (com `now` em deslocamento -03:00, não em UTC).

## Fora do escopo
App, Service, `Engine.cs`, `VERSION`, README, `contexto.md`.

## Pronto quando
- `dotnet test tests/Backeddy.Core.Tests` passa, com o resultado no relatório.
- `tasks/bench.relatorio.md` escrito no formato do `AGENTS.md`.
