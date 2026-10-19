# 003 — Manter o PC acordado durante o backup

## Objetivo
Um backup não pode morrer pela metade porque o Windows suspendeu o PC. Enquanto um backup roda, o PC não suspende: sempre na tomada;
na bateria só se a pessoa marcar uma opção (com aviso do custo). Fechar a tampa continua suspendendo o PC (não é coberto).

## Comportamento
- Vale só durante a execução de um backup (do início da preparação até o fim do `kopia snapshot`). Antes e depois, o PC dorme normal.
- Regra (no Core, testável): fica acordado se `!onBattery` ou se `BackupConfig.KeepAwakeOnBattery` for verdadeiro.
  A decisão é tomada UMA vez, quando o backup começa; não reavaliar no meio.
- Só mantém o sistema ligado; a tela pode apagar (não pedir "tela ligada").
- Se algo falhar ao pedir (a chamada do Windows devolver 0), o backup segue normalmente, sem erro para a pessoa.
- Opção da bateria: desligada por padrão (arquivos de configuração antigos, sem o campo, valem como falso).
- A opção aparece como uma caixa de marcar na tela inicial, só quando já existe destino configurado.

## O que mudar, arquivo por arquivo
- `src/Backeddy.Core/Config.cs`: em `BackupConfig`, `public bool KeepAwakeOnBattery { get; init; }`, perto de `PausedUntil`.
- `src/Backeddy.Core/Schedule.cs`: `public static bool ShouldKeepAwake(BackupConfig c, bool onBattery) => !onBattery || c.KeepAwakeOnBattery;`
- `src/Backeddy.Core/Models.cs`:
  - em `ServiceStatus`, `public bool KeepAwakeOnBattery { get; init; }`
  - `public sealed record KeepAwakeRequest(bool OnBattery);` (perto de `PauseRequest`)
- `src/Backeddy.Core/Ipc.cs`: em `Cmd`, `public const string KeepAwake = "keepawake.set";`
- `src/Backeddy.Service/KeepAwake.cs` (arquivo novo), exatamente assim:
```csharp
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
```
- `src/Backeddy.Service/Engine.cs`:
  - em `BackupAsync`, dentro do `else` que roda o kopia, como PRIMEIRA linha do bloco (antes de `if (policiesDirty)`):
    `using var awake = Schedule.ShouldKeepAwake(cfg, Power.Read().OnBattery) ? KeepAwake.Begin() : null;`
  - em `HandleAsync`, novo `case Cmd.KeepAwake:` (perto de `Cmd.Pause`): lê `Ipc.From<KeepAwakeRequest>(req.Data)?.OnBattery ?? false`,
    faz `Update(c => c with { KeepAwakeOnBattery = valor });` e devolve `Ipc.Success()`. Não precisa de `wake.Release()`.
  - em `Status()`, `KeepAwakeOnBattery = cfg.KeepAwakeOnBattery,` junto dos outros campos.
- `src/Backeddy.App/MainWindow.Home.cs`:
  - campo `CheckBox? homeKeepAwake;` junto de `homePause`.
  - em `ShowHome`, criar `homeKeepAwake = new CheckBox { Content = Strings.T("keepAwakeBattery") };`
    com `homeKeepAwake.Click += (_, _) => _ = KeepAwakeAsync(homeKeepAwake.IsChecked == true);` (Click só dispara por ação da pessoa).
  - colocá-lo no `Ui.Column(6, ...)` dos links, logo antes da linha da versão, na forma `Ui.Column(2, homeKeepAwake, Ui.Caption(Strings.T("keepAwakeBatteryNote")))` e deixar esse bloco inteiro oculto/visível junto com a caixa.
    Para isso guarde a coluna em um campo `StackPanel? homeKeepAwakeBox` e mude a visibilidade DELA em `UpdateHome`, e deixe a caixa sempre visível dentro dela.
  - em `UpdateHome`: `homeKeepAwakeBox!.Visibility = st.Destination == DestinationKind.None ? Visibility.Collapsed : Visibility.Visible;` e `homeKeepAwake!.IsChecked = st.KeepAwakeOnBattery;`
  - novo método `KeepAwakeAsync(bool onBattery)` no padrão de `PauseAsync`: `ServiceClient.CallAsync(Cmd.KeepAwake, new KeepAwakeRequest(onBattery))`,
    se `r?.Ok != true` mostrar `Ui.InfoAsync(XRoot, Strings.T("keepAwakeBattery"), ServiceClient.ErrorText(r), Strings.T("ok"))`, e depois `await PollAsync();`.
- `src/Backeddy.Core/Strings.cs`: as chaves abaixo, perto de `statePaused`.

## Textos novos (pt / en / es)
- `keepAwakeBattery`: "Manter o PC acordado durante o backup, mesmo na bateria" / "Keep the PC awake during a backup, even on battery" / "Mantener el PC despierto durante el backup, incluso con batería"
- `keepAwakeBatteryNote`: "Na tomada isso já é automático. Na bateria gasta mais carga. Fechar a tampa ainda suspende o PC." / "On the charger this is automatic. On battery it uses more charge. Closing the lid still suspends the PC." / "Con el cargador es automático. Con batería gasta más carga. Cerrar la tapa sigue suspendiendo el PC."

## Testes obrigatórios
- `ScheduleTests` (`tests/Backeddy.Core.Tests/EngineTests.cs`): `ShouldKeepAwake` nos quatro casos (tomada sem opção → true; bateria sem opção → false; bateria com opção → true; tomada com opção → true).
- `IpcTests` (`tests/Backeddy.Core.Tests/CoreTests.cs`): `KeepAwakeRequest(true)` vai e volta por `Ipc.Encode` / `Ipc.Decode`; `ServiceStatus { KeepAwakeOnBattery = true }` também.
- Se já existir teste de ida e volta de `BackupConfig` (salvar e carregar), copie o padrão para `KeepAwakeOnBattery`; um JSON sem o campo carrega como falso.

## Fora do escopo
Restauração (não segura o PC acordado), tampa fechada, manter a tela ligada, reavaliar tomada/bateria no meio do backup, `VERSION`, README, `contexto.md`.

## Pronto quando
- `dotnet test tests/Backeddy.Core.Tests` passa, com o resultado no relatório.
- O CI compila App e Service sem erro.
- `tasks/003.relatorio.md` escrito no formato do `AGENTS.md`.
