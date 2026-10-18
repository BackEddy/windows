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
2. Esqueleto: repositório, CI, serviço e bandeja — 0.0.1 (este).
3. Motor Kopia + destino HD externo. 4. Destino servidor. 5. Telas. 6. Linha do tempo. 7. Alertas. 8. Docs e 0.1.
