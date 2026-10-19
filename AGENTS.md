# Regras para agentes (opencode) — Backeddy for Windows

Quem manda neste projeto é o Claude (PO e arquiteto) e o Eddy. Você implementa tarefas descritas em `tasks/NNN-nome.md`.
Antes de qualquer coisa, leia `contexto.md` (decisões e estado do projeto) e a spec da tarefa.

## O projeto em dois minutos
- App de backup para Windows 10/11, em C# / .NET 8, WinUI 3 sem MSIX. Motor: kopia.exe embutido.
- Três peças:
  - `src/Backeddy.Core`: lógica pura, sem dependências externas (textos pt/en/es em `Strings.cs`, comandos do canal em `Ipc.cs`,
    modelos em `Models.cs`, configuração em `Config.cs`, agenda em `Schedule.cs`).
  - `src/Backeddy.Service`: serviço do Windows "Backeddy" (`Engine.cs` é o cérebro; `PipeServer.cs` recebe os comandos do app).
  - `src/Backeddy.App`: janela e ícone na bandeja. As telas são montadas em código (`Ui.cs` + `MainWindow.*.cs`), sem XAML novo.
- Testes ficam em `tests/Backeddy.Core.Tests` (xUnit) e só cobrem o Core e a lógica do Engine que não depende do Windows.

## Como verificar o trabalho
- Neste Mac NÃO há .NET nem Windows. Quem compila e testa é o CI do GitHub (windows-latest). Por isso:
  - Leia o código vizinho antes de escrever e copie os padrões que já existem (nomes de métodos, estilo, helpers).
  - Antes de chamar um método ou propriedade, confira no código que ele existe e qual é a assinatura. Não invente API.
  - Toda lógica nova do Core ganha teste em `tests/Backeddy.Core.Tests`. Os testes existentes não podem quebrar.
  - Um erro de compilação só aparece no CI: seja conservador, mudanças pequenas e coerentes.

## Regras de código
- Core não usa WinUI, nem API do Windows, nem pacote NuGet novo. Nenhuma dependência nova em lugar nenhum.
- Todo texto que a pessoa vê entra nos TRÊS idiomas em `Strings.Table` (`Pt`, `En`, `Es`), com os mesmos `{0}`, `{1}`.
  Existe um teste que confere isso. Chaves novas seguem o padrão das vizinhas.
- Nullable ligado: nenhum warning novo. `sealed record` com `init` para modelos, como já é feito.
- Novo comando do canal app↔serviço: constante em `Cmd` (Ipc.cs), tratado em `Engine.HandleAsync`, chamado pelo app com
  `ServiceClient.CallAsync`. Dados vão em um `record` no `Models.cs`.
- Comentários curtos em português, explicando o porquê (não o quê). Nada de código morto, nada de TODO solto.
- Mudança mínima: não reformate nem renomeie o que não faz parte da tarefa.

## O que você NUNCA faz
- `git commit`, `git push`, `git tag`, `git reset`, `git checkout`, `git stash`, `git rebase`, `git merge`, `gh ...`. Quem publica é o Claude/Eddy.
- Editar: `.github/`, `installer/`, `VERSION`, `Directory.Build.props`, `README*.md`, `contexto.md`, `AGENTS.md`, `opencode.json`,
  e as specs `tasks/*.md`. Se achar que alguma dessas precisa mudar, diga no relatório.
- Apagar arquivos, baixar nada da internet, instalar pacotes, rodar `curl`/`wget`.
- Adivinhar quando a spec e o código discordam: pare e relate (veja abaixo).

## Relatório (obrigatório, curto)
Ao terminar, escreva `tasks/NNN.relatorio.md` com no máximo 25 linhas, exatamente nestas seções:
```
## Feito
(uma linha por mudança)
## Arquivos tocados
(lista de caminhos)
## Decisões
(o que você escolheu quando a spec deixou aberto)
## Não consegui / dúvidas
(ou "nada")
## Risco no CI
(onde um erro de compilação ou de teste seria mais provável)
```
Se o CI falhou e você recebeu o log: corrija SÓ o que o log mostra e acrescente ao mesmo relatório uma seção
`## Correção do CI (tentativa K)` com a causa e o que mudou.
