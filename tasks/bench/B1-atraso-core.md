# B1 — Benchmark: núcleo do aviso de atraso (só Core)

Tarefa-padrão para comparar modelos. Escopo pequeno e fechado; o App e o Service NÃO entram.

## Objetivo
O Core passa a calcular há quantos dias o último backup bem-sucedido aconteceu, quando isso passa de 3 dias.

## Comportamento
- "Atrasado" = existe `LastSuccess` e `now - LastSuccess >= 3 dias`. O número devolvido é de dias inteiros (para baixo), sempre 3 ou mais.
- Sem `LastSuccess` (nenhum backup ainda) ou sem destino configurado (`DestinationKind.None`): não é atraso (`null`).

## O que mudar
- `src/Backeddy.Core/Schedule.cs`:
  - `public static readonly TimeSpan OverdueAfter = TimeSpan.FromDays(3);`
  - `public static int? OverdueDays(BackupConfig c, DateTimeOffset now)`
- `src/Backeddy.Core/Models.cs`: em `ServiceStatus`, `public int? OverdueDays { get; init; }`.
- `src/Backeddy.Core/Strings.cs`, três chaves na tabela `Table`, perto de `statePaused`:
  - `overdueTitle`: "Faz {0} dias sem backup" / "{0} days without a backup" / "{0} días sin backup"
  - `overdueDisk`: "Ligue o HD {0} e o Backeddy volta a copiar sozinho, ou clique em Fazer backup agora." / "Plug in the drive {0} and Backeddy resumes by itself, or click Back up now." / "Conecta el disco {0} y Backeddy vuelve a copiar solo, o haz clic en Hacer backup ahora."
  - `overdueServer`: "Volte para a rede de casa para falar com o servidor {0}. O backup continua sozinho, ou clique em Fazer backup agora." / "Get back on your home network to reach the server {0}. Backups resume by themselves, or click Back up now." / "Vuelve a la red de casa para llegar al servidor {0}. El backup continúa solo, o haz clic en Hacer backup ahora."

## Testes obrigatórios
- Em `ScheduleTests` (`tests/Backeddy.Core.Tests/EngineTests.cs`): um teste para `OverdueDays` com os casos: 2 dias e 23 h → `null`; 3 dias → `3`; 5 dias e 12 h → `5`; sem `LastSuccess` → `null`; sem destino com backup antigo → `null`.
- Em `IpcTests` (`tests/Backeddy.Core.Tests/CoreTests.cs`): `ServiceStatus { OverdueDays = 4 }` vai e volta por `Ipc.Encode` / `Ipc.Decode`.

## Fora do escopo
- App, Service, notificações, `VERSION`, README, `contexto.md`.

## Pronto quando
- `dotnet test tests/Backeddy.Core.Tests` passa, com o resultado no relatório.
- `tasks/bench.relatorio.md` escrito no formato do `AGENTS.md`.
