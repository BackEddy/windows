**Backeddy for Windows 0.1 — first version that really backs up.** Early test release: keep another copy of anything you can't lose. · Primeira versão que faz backup de verdade (versão de teste) · Primera versión que hace backup de verdad (versión de prueba)

| File | |
| --- | --- |
| `Backeddy-Setup-*.exe` | Installer (Windows 10 22H2 or 11, 64-bit) |

- Hourly backups of your user folder and other internal drives, with Volume Shadow Copy
- To a **Backeddy Server 0.9+** (found on the network) or an **external drive** (with a recovery key)
- Restore any file or folder from any moment, beside the current one or in its original place
- Server alerts next to the clock · waits on low battery · English, Português, Español

### ⚠️ "Windows protected your PC"

The installer **isn't digitally signed yet** (we're working on it), so Windows SmartScreen warns about it: **More info › Run anyway**. It's open source and built automatically by GitHub Actions from the code in this repository. The backup engine is [Kopia](https://kopia.io) (Apache 2.0), included.

**PT** — O instalador **ainda não tem assinatura digital**, por isso o Windows avisa “O Windows protegeu o computador”: **Mais informações › Executar assim mesmo**. O código é aberto e o arquivo é montado pelo GitHub a partir dele.

**ES** — El instalador **todavía no tiene firma digital**, por eso Windows avisa “Windows protegió su PC”: **Más información › Ejecutar de todas formas**. El código es abierto y GitHub genera el archivo a partir de él.
