# Testando o Backeddy for Windows (0.0.x: esqueleto)

Use uma **máquina virtual Windows limpa** (Hyper-V: Windows 11 ou 10 22H2, 64 bits). O app instala um serviço.

## Baixar

Do último build (sem release): `gh run download -R BackEddy/windows -n backeddy-windows -D $HOME\Downloads`
Com tag: página de Releases do BackEddy/windows.

## Conferir (0.0.1)

- [ ] O instalador abre no idioma do Windows (português, inglês ou espanhol); o aviso azul do Windows aparece
      ("Mais informações › Executar assim mesmo")
- [ ] **O Windows Defender não bloqueia** nem apaga nada (nem na instalação, nem depois de alguns minutos)
- [ ] No fim, a janela do Backeddy abre: "Serviço de backup: funcionando (versão 0.0.1)" e o aviso de versão de teste
- [ ] O ícone aparece perto do relógio: clique abre a janela; botão direito mostra o menu
- [ ] Fechar a janela no X só esconde (o ícone continua); "Fechar o ícone" no menu tira o ícone
- [ ] Abrir o Backeddy de novo pelo menu Iniciar traz a mesma janela (não abre outra)
- [ ] Reiniciar o Windows: o ícone volta sozinho, e a janela mostra o serviço funcionando
- [ ] Serviços do Windows (services.msc): "Backeddy", em execução, início automático (atraso)
- [ ] Desinstalar (Configurações › Aplicativos): o ícone some e o serviço "Backeddy" sai da lista

## Se algo der errado

- Registro do serviço: Visualizador de Eventos › Logs do Windows › Aplicativo (procure por "Backeddy").
- `sc query Backeddy` num PowerShell mostra se o serviço está rodando.
