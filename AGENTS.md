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
- Este Mac tem o SDK do .NET 8. O `Core` e os testes compilam e rodam aqui; o `App` (WinUI) e o `Service` só compilam no Windows, no CI do GitHub.
- ANTES de escrever o relatório, rode `dotnet test tests/Backeddy.Core.Tests` e corrija o que falhar. Só entregue com tudo passando.
  Escreva no relatório o resultado (ex.: "54 passaram").
- Para o que só o CI compila (`src/Backeddy.App`, `src/Backeddy.Service`): leia o código vizinho antes de escrever, copie os padrões que
  já existem e confira no código que cada método ou propriedade existe, com a assinatura certa. Não invente API.
- Toda lógica nova do Core ganha teste em `tests/Backeddy.Core.Tests`. Os testes existentes não podem quebrar.
- Não rode `dotnet build` nem `dotnet publish` nos projetos `App` ou `Service` (não compilam no Mac).

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
## Testes locais
(saída resumida de `dotnet test tests/Backeddy.Core.Tests`)
## Risco no CI
(onde um erro de compilação ou de teste seria mais provável, sobretudo no App e no Service)
```
Se o CI falhou e você recebeu o log: corrija SÓ o que o log mostra e acrescente ao mesmo relatório uma seção
`## Correção do CI (tentativa K)` com a causa e o que mudou.

## Armadilhas de C# que já deram erro
- `var x = cond ? null : valorStruct;` não compila (CS0173). Declare o tipo: `DateTimeOffset? x = cond ? null : valor;`.

## Economia de tokens (vale para toda tarefa)
- Respostas curtas: sem explicações longas, sem narrar o que vai fazer. O relatório final é a única prosa.
- Não pesquise na web. Se a spec e a API discordarem, use o que o código do repositório já faz e relate a dúvida.
- Leia só os arquivos que a spec cita (e o que eles chamam diretamente). Não varra o repositório.
- Rode `dotnet test tests/Backeddy.Core.Tests` uma vez no fim (e de novo só se corrigir algo).
