<p align="center"><img src="assets/icon-256.png" width="112" alt="Backeddy"></p>

<h1 align="center">Backeddy for Windows</h1>

<p align="center"><b>Um Time Machine para o Windows: backup dos seus arquivos a cada hora, e qualquer arquivo de volta como estava em qualquer dia.</b><br>
<a href="README.md">English</a> · <a href="README.es.md">Español</a></p>

<p align="center"><a href="https://github.com/BackEddy/windows/releases"><b>⬇️ Baixar (Backeddy-Setup-0.1.x.exe)</b></a> · Windows 10 22H2 ou 11, 64 bits · grátis</p>

---

## Antes de começar: os riscos

- **Esta é a versão 0.1 — a primeira que faz backup de verdade.** Pouquíssimas pessoas testaram. Mantenha **outra cópia** do que você não pode perder até ela provar que funciona.
- Foi escrita com muita ajuda de IA e revisada por uma pessoa. O código é aberto: leia, teste e conte o que quebrar.
- O instalador **ainda não tem assinatura digital**, então o Windows avisa “O Windows protegeu o computador”: **Mais informações › Executar assim mesmo**. O arquivo é montado pelo GitHub direto deste código.
- Fornecido como está, sem garantias ([licença MIT](LICENSE)).

## O que ele faz

- A cada hora, faz backup da **sua pasta de usuário e dos outros discos internos** (D:, E:…). Programas e o próprio Windows ficam de fora — dá para reinstalar. Você pode desmarcar pastas que não precisam (jogos, downloads grandes).
- Arquivos abertos também são copiados (cópia de sombra do Windows).
- Os backups vão para o **[Backeddy Server](https://github.com/BackEddy/server)** da sua casa (achado sozinho na rede; você só digita a senha do servidor) ou para um **HD externo**.
- **Restaurar arquivos**: escolha data e hora, navegue pelas pastas e traga de volta um arquivo ou uma pasta inteira — ao lado da atual (uma cópia com outro nome) ou no lugar original.
- No notebook, espera quando a bateria está abaixo de 20%; e espera o HD ou o servidor aparecerem.
- Avisos do servidor (um disco falhando, por exemplo) aparecem perto do relógio, com “lembrar amanhã / em 3 / em 7 dias”.
- Português, English, Español (segue o idioma do Windows).

## Configurar

1. Baixe e abra o `Backeddy-Setup-0.1.x.exe` (veja o aviso acima).
2. O Backeddy abre e pergunta **onde guardar os backups**:
   - **Backeddy Server** — escolha o servidor na lista (ou digite o endereço que aparece na tela dele) e digite a senha do servidor. Precisa do Backeddy Server **0.9 ou mais novo**.
   - **HD externo** — escolha o HD. O Backeddy cria uma pasta `Backeddy` nele e mostra uma **chave de recuperação**: anote no papel. Sem ela, ninguém abre esses backups (são criptografados) — nem nós.
3. Pronto. O primeiro backup copia tudo e pode levar horas; os seguintes copiam só o que mudou.

Um HD que já tem backups do Backeddy (deste PC antes de formatar, por exemplo) pede a chave de recuperação e os backups antigos aparecem de novo.

## Quanto espaço?

Conte **o dobro do que vai para o backup**: uma cópia completa e espaço para o histórico. O Backeddy guarda backups de hora em hora por um dia, diários por um mês, semanais por 3 meses, mensais por 2 anos e anuais por 5 anos (no Backeddy Server, quem decide é o servidor).

## Por dentro

C# / .NET 8. Um serviço do Windows (`Backeddy`, rodando como sistema) faz o trabalho com o motor aberto **[Kopia](https://kopia.io)** (Apache 2.0, incluído como `kopia.exe`); o app (WinUI 3) é a janela e o ícone na bandeja, e conversa com o serviço por um canal local. Só o usuário do Windows que configurou o Backeddy pode mudá-lo. As senhas ficam guardadas com o DPAPI do Windows, legíveis só pelo serviço. Registros: `C:\ProgramData\Backeddy\logs`.

Ainda não: jogos e programas, mudar para um PC novo, imagem completa do sistema e pendrive de resgate.

Guia de testes: [docs/TESTING.pt-BR.md](docs/TESTING.pt-BR.md).

## Faz parte do Backeddy

[Backeddy for Mac](https://github.com/BackEddy/mac) · [Backeddy Server](https://github.com/BackEddy/server) · MIT © 2026 [Eddy Antonini](https://eddyantonini.com.br)
