# 002 — Aviso de backup atrasado (3 dias)

## Objetivo
Se o último backup bem-sucedido tem 3 dias ou mais, a pessoa é avisada: uma faixa de aviso na tela inicial e uma notificação perto do relógio
(uma vez por dia, ou ao abrir o app). Sem isso, um HD esquecido na gaveta ou um servidor fora da rede passam semanas sem ninguém notar.

## Comportamento
- "Atrasado" = existe um backup bem-sucedido (`LastSuccess`) e `now - LastSuccess >= 3 dias`. A contagem mostrada é o número de dias inteiros (arredondado para baixo, então sempre 3 ou mais).
- Sem backup nenhum ainda (`LastSuccess` nulo) ou sem destino configurado: NÃO é atrasado (o primeiro backup tem a tela própria "Primeiro backup chegando").
- A pausa de backups (tarefa 001) não esconde o aviso.
- A faixa fica na tela inicial enquanto estiver atrasado, no topo da lista de avisos (antes dos avisos do servidor), com gravidade de aviso (amarela), sem botão de fechar. Some sozinha no primeiro backup bem-sucedido.
- Texto da faixa depende do destino: HD (`DestinationKind.Disk`) ou servidor (`DestinationKind.Server`), com o nome do destino (`st.DestinationName`).
- Notificação: usar o mesmo caminho dos alertas do servidor (`CheckAlerts` em `MainWindow.xaml.cs`, evento `AlertRaised`). Uma notificação por valor de dias (chave `"overdue|" + dias`), então no máximo uma por dia enquanto o app está aberto, e de novo ao reabrir o app na bandeja.
  Mesma regra de hoje para a primeira consulta: só notifica na primeira consulta se a janela estiver escondida.

## O que mudar, arquivo por arquivo
- `src/Backeddy.Core/Schedule.cs`:
  - `public static readonly TimeSpan OverdueAfter = TimeSpan.FromDays(3);`
  - `public static int? OverdueDays(BackupConfig c, DateTimeOffset now)`: devolve `null` se `c.Destination == DestinationKind.None`, se `c.LastSuccess` é nulo ou se `now - LastSuccess < OverdueAfter`; senão `(int)(now - LastSuccess).TotalDays`.
- `src/Backeddy.Core/Models.cs`: em `ServiceStatus`, `public int? OverdueDays { get; init; }`.
- `src/Backeddy.Service/Engine.cs`: em `Status()`, `OverdueDays = Schedule.OverdueDays(cfg, DateTimeOffset.Now),` junto dos outros campos do objeto.
- `src/Backeddy.App/MainWindow.Home.cs`: em `UpdateHome`, logo depois de `homeBars.Children.Clear();`, se `st.OverdueDays is { } days`, adicionar `Ui.Bar(InfoBarSeverity.Warning, Strings.F("overdueTitle", days), Strings.F(st.Destination == DestinationKind.Server ? "overdueServer" : "overdueDisk", st.DestinationName ?? ""))`.
- `src/Backeddy.App/MainWindow.xaml.cs`: em `CheckAlerts`, antes de `firstPoll = false;`, tratar `st.OverdueDays` como descrito (mesmo `seenAlerts`, mesma condição `!firstPoll || !AppWindow.IsVisible`, mesmo `AlertRaised`, com título `Strings.F("overdueTitle", days)` e texto igual ao da faixa).
- `src/Backeddy.Core/Strings.cs`: as chaves abaixo, perto de `statePaused`.

## Textos novos (pt / en / es)
- `overdueTitle`: "Faz {0} dias sem backup" / "{0} days without a backup" / "{0} días sin backup"
- `overdueDisk`: "Ligue o HD {0} e o Backeddy volta a copiar sozinho, ou clique em Fazer backup agora." / "Plug in the drive {0} and Backeddy resumes by itself, or click Back up now." / "Conecta el disco {0} y Backeddy vuelve a copiar solo, o haz clic en Hacer backup ahora."
- `overdueServer`: "Volte para a rede de casa para falar com o servidor {0}. O backup continua sozinho, ou clique em Fazer backup agora." / "Get back on your home network to reach the server {0}. Backups resume by themselves, or click Back up now." / "Vuelve a la red de casa para llegar al servidor {0}. El backup continúa solo, o haz clic en Hacer backup ahora."

## Testes obrigatórios
- Em `ScheduleTests` (EngineTests.cs), `OverdueAfterThreeDays`: com `Now` fixo e um `BackupConfig` de disco —
  `LastSuccess = Now - 2d23h` → `null`; `Now - 3d` → `3`; `Now - 5d12h` → `5`; `LastSuccess` nulo → `null`; `BackupConfig` sem destino com `LastSuccess` antigo → `null`.
- Em `IpcTests` (CoreTests.cs), `OverdueDaysTravelsInTheStatus`: `Ipc.Encode` / `Ipc.Decode` de um `ServiceStatus { OverdueDays = 4 }` devolve 4.

## Fora do escopo
- Limite configurável, aviso por e-mail, qualquer mudança no servidor, no ícone da bandeja ou no texto de erro de backup.
- Mexer em `VERSION`, README, instalador, `contexto.md` (isso é do Claude).

## Pronto quando
- `dotnet test tests/Backeddy.Core.Tests` passa (os existentes e os novos), com o resultado no relatório.
- Nenhum warning novo.
- `tasks/002.relatorio.md` escrito no formato do `AGENTS.md`.
