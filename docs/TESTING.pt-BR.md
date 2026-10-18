# Testando o Backeddy for Windows 0.1

Use uma **máquina virtual Windows** (Hyper-V: Windows 11 ou 10 22H2, 64 bits) com alguns arquivos de teste na pasta
do usuário (Documentos, Imagens). Para o teste com servidor, precisa de um **Backeddy Server 0.9 ou mais novo** na
mesma rede (o "Backup da Casa 9" ainda está na 0.6.4: monte um novo com o `Instalar-Backeddy.exe` mais recente ou
atualize).

## Baixar

Com tag: página de Releases do BackEddy/windows. Do último build: `gh run download -R BackEddy/windows -n backeddy-windows -D $HOME\Downloads`

## 1. Instalação

- [ ] Instalador no idioma do Windows; aviso azul do SmartScreen ("Mais informações › Executar assim mesmo")
- [ ] **Windows Defender não bloqueia** nada (nem o `kopia.exe` em `C:\Program Files\Backeddy\service`)
- [ ] A janela abre em **"Onde guardar os backups?"**; o ícone aparece perto do relógio
- [ ] Atualizando da 0.0.1: instala por cima sem erro

## 2. HD externo

Na VM: Hyper-V › Configurações › Controlador SCSI › Disco rígido › novo VHDX de uns 20 GB; no Windows, Gerenciamento
de Disco › inicializar e formatar (NTFS, nome "Backup").

- [ ] **HD externo** lista o disco com nome, espaço livre e "disco interno" (na VM ele aparece como interno, tudo bem)
- [ ] Escolher → aparece a **chave de recuperação**; "Continuar" só libera depois de marcar "Anotei"; "Copiar" funciona
- [ ] Tela inicial: "Primeiro backup chegando" → "Fazendo backup…" (com rodinha) → "✅ Tudo em dia",
      último backup "hoje às …" com nº de arquivos e tamanho; próximo "hoje às …" (1 hora depois)
- [ ] `B:\Backeddy` (ou a letra do disco) foi criado e tem arquivos do Kopia
- [ ] Mexer num arquivo, **Fazer backup agora** → termina rápido (só o que mudou)
- [ ] Abrir um documento no Word/Bloco de Notas e fazer backup: passa (cópia de sombra)
- [ ] **O que entra no backup**: lista as pastas da sua pasta de usuário; desmarcar "Downloads" e Salvar → "Salvo"
- [ ] Desligar o disco (Gerenciamento de Disco › Offline) → em até 1 minuto: "⏸ Esperando — Ligue o HD …";
      ligar de novo → volta sozinho
- [ ] **Chave errada**: Trocar o destino › HD externo › mesmo disco → pede a chave; digitar uma errada → "não confere";
      a certa → os backups antigos aparecem em Restaurar arquivos

## 3. Restaurar

- [ ] **Restaurar arquivos**: à esquerda, dias e horários; à direita "Sua pasta (C:\Users\…)" e os outros discos
- [ ] Entrar em pastas; o caminho no topo permite voltar; pasta vazia diz "Pasta vazia"
- [ ] Num arquivo: **Restaurar ao lado** → volta à tela inicial, "Restaurando…" → "Restaurado" com o caminho
      `nota (restaurado 2026-10-17 10h00).txt`; **Mostrar na pasta** abre o Explorer com ele selecionado
- [ ] Apagar uma pasta de teste e restaurá-la **no lugar original** (pede confirmação) → volta inteira
- [ ] O arquivo restaurado abre normalmente com o seu usuário (permissões)

## 4. Backeddy Server (0.9+)

- [ ] Trocar o destino › **Backeddy Server** → o servidor aparece sozinho na lista (nome e endereço)
- [ ] Senha errada → "Senha do servidor errada"; certa → tela inicial, primeiro backup começa
- [ ] O painel do servidor (navegador, `http://nome.local`) mostra o PC em "PCs com Windows"
- [ ] "Digite o endereço": funciona com o IP mostrado na tela do servidor
- [ ] Desligar o servidor → "⏸ Esperando — O servidor … não está respondendo"; ligar → volta sozinho
- [ ] Restaurar um arquivo vindo do servidor

## 5. Alertas do servidor

- [ ] Provocar um alerta no servidor (ex.: tirar um dos discos da VM do servidor, ou desligar o cabo de rede dele)
- [ ] Em até 1 minuto: aviso perto do relógio ("Aviso do servidor de backup") e faixa amarela na tela inicial
- [ ] **Amanhã / Em 3 dias / Em 7 dias** → a faixa some e o servidor para de bipar
- [ ] Clicar no aviso perto do relógio abre o Backeddy

## 6. Notebook (se tiver um)

- [ ] Na bateria abaixo de 20%: "⏸ Esperando — A bateria está abaixo de 20%"; "Fazer backup agora" faz mesmo assim

## 7. Geral

- [ ] Reiniciar o Windows: ícone volta, backups continuam sozinhos
- [ ] Outro usuário do mesmo PC abre o Backeddy: vê a situação, mas não consegue mudar nada ("configurado por outro usuário")
- [ ] Desinstalar: ícone e serviço somem; os backups continuam no HD/servidor

## Se algo der errado

Mande o arquivo `C:\ProgramData\Backeddy\logs\backeddy.log` (e a pasta `logs\kopia`, se existir) junto com o que
aconteceu. `sc query Backeddy` mostra se o serviço está rodando.
