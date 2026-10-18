# Backeddy for Windows — contexto

Parte da família Backeddy (contexto geral e decisões em BackEddy/server, arquivo contexto.md).

## Decisões (17/10/2026)
- C# + WinUI 3 (sem MSIX, que exige assinatura), .NET 8 self-contained, x64, Windows 10 22H2+ / 11.
- Peças: Backeddy.Core (sem dependências; textos pt/en/es, canal app↔serviço em JSON por linha), Backeddy.Service
  (serviço do Windows "Backeddy", como sistema, canal nomeado Backeddy.Service.v1 aberto a usuários autenticados),
  Backeddy.App (janela + ícone na bandeja feito direto na API do Windows, sem bibliotecas; uma instância só; --tray).
- Instalador Inno Setup (pt/en/es), sem assinatura por enquanto; CI no GitHub Actions (windows-latest) testa,
  publica e monta Backeddy-Setup-X.exe; tags v* viram pré-lançamento.
- 0.1 = destinos Backeddy Server (pareamento /api/pair do servidor 0.9) e HD externo (chave de recuperação), só arquivos,
  tudo entra (exclusões opt-out), de hora em hora, VSS, linha do tempo, alertas do servidor; só o usuário que instalou;
  espera com bateria < 20%. Motor: kopia.exe embutido com versão fixa.

## Passos
1. (servidor 0.9) pareamento — feito.
2. Esqueleto: repositório, CI, serviço e bandeja — 0.0.1 (instalado e testado numa VM).
3. 0.1.0 (rodada única, 17/10/2026): tudo abaixo. Falta testar de verdade (docs/TESTING.pt-BR.md).
4. 1.0.0 (18/10/2026; decisão: fechar arquivos e versões como produto, desastre depois): "Versões deste arquivo…"
   (Cmd file.versions; FileHistory.CollectAsync percorre os backups da origem com cache por objeto de pasta e lista os
   conteúdos diferentes); restaurar usa referência "objeto-da-pasta/nome" (FileHistory.IsRef valida) para o Kopia
   manter nome e data; bloco "Ainda não tem um Backeddy Server?" (servidor em OUTRO computador; link + HD externo).
   README: seção "Se o PC morrer" (plano B: KopiaUI + chave). Servidor 1.0 passa a se atualizar sozinho.

## Como a 0.1 funciona
- Core (testado em tests/, roda no Linux): Ipc (comandos em Cmd, respostas IpcResponse com códigos Err), Models,
  Config (C:\ProgramData\Backeddy\config.json), Sources (pasta do usuário + discos fixos menos o do Windows e o de
  destino; ignora temp/caches/Lixeira/OneDrive; exclusões viram regras "/pasta/"), RecoveryKey (24 caracteres sem
  0/1/O/I/L), Schedule (1 h; 15 min depois de falha; bateria < 20% espera, menos no manual), KopiaCli (monta comandos,
  lê JSON, classifica erros), ServerClient (/api/pair, /api/status, /api/ack), RestorePlan ("ao lado" e trava de
  restaurar só dentro das origens), Format, Strings (pt/en/es; err.* para cada código).
- Serviço: Engine (agendador a cada 30 s, backup, configurar HD/servidor, pontos, listar pasta, restaurar, alertas a
  cada minuto), KopiaRunner (kopia.exe ao lado, config/cache em ProgramData\Backeddy\private, fechada a usuários
  comuns), Secrets (DPAPI da conta Sistema), Power, FileLogger (logs\backeddy.log). PipeServer confere o usuário que
  chama: só quem configurou muda algo.
- HD: repositório em X:\Backeddy, senha = chave de recuperação, usuário Kopia fixo "backeddy" e host = nome do PC
  (reinstalar o Windows com o mesmo nome traz os backups antigos). Política global: 24 por hora, 30 diários, 12
  semanais, 24 mensais, 5 anuais, zstd-fastest, ignora erros de arquivo.
- Servidor: pareia, conecta com --server-cert-fingerprint, tenta o nome .local e cai para o IP; senha do servidor
  guardada só para alertas.
- App: telas montadas em código (MainWindow.Setup/Home/Exclusions/Timeline.cs + Ui.cs); descoberta mDNS
  _backeddy._tcp via Windows.Devices.Enumeration; aviso na bandeja (NIF_INFO) para alerta novo.
- CI baixa o Kopia (KOPIA_VERSION no build.yml, hoje 0.19.0) e confere com checksums.txt quando existe.

## Pendências
- Testar tudo (HD, restauração, versões, servidor 1.0, alertas, bateria). Fixar o SHA-256 do Kopia no build.yml.
- Recuperação de desastre (próxima): "Este PC substitui o PC-X" (HD: snapshot list --all / servidor: /api/pcs e
  re-pareamento com o nome antigo), mapear a pasta de usuário antiga para a atual (a trava InsideSources hoje bloqueia
  caminhos antigos), "Restaurar tudo deste momento".
- Depois: jogos/apps, migrar para PC novo, imagem do sistema, pendrive de resgate; SignPath quando houver reputação.
