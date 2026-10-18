<p align="center"><img src="assets/icon-256.png" width="112" alt="Backeddy"></p>

<h1 align="center">Backeddy for Windows</h1>

<p align="center"><b>A Time Machine for Windows</b> · Um Time Machine para o Windows · Un Time Machine para Windows</p>

> 🚧 **Under construction — not ready for use yet.** This is the very first skeleton: it installs a background service and a tray icon, but **doesn't back anything up yet**. Follow along, or wait for 0.1.
>
> 🚧 **Em construção — ainda não serve para usar.** Por enquanto instala um serviço e um ícone na bandeja, mas **ainda não faz backup**. · 🚧 **En construcción — todavía no sirve para usar.** Por ahora instala un servicio y un icono en la bandeja, pero **todavía no hace backup**.

## The plan for 0.1

- Back up **everything in your user folder and your other internal drives**, every hour — you only choose what to leave out.
- To your home **[Backeddy Server](https://github.com/BackEddy/server)** (found automatically on the network; you just type the server password) or to an **external drive** (with a recovery key you write down).
- A **timeline** to bring back any file as it was on any day.
- Tray icon with status and the server's alerts. English, Português, Español.

Under the hood: C# / WinUI 3, a Windows service running the open-source [Kopia](https://kopia.io) engine (Apache 2.0) with Volume Shadow Copy, so even open files are copied.

Later: games, apps and moving to a new PC, a full system image and a rescue USB drive.

## Unsigned installer

The installer isn't digitally signed yet, so Windows SmartScreen will warn (“Windows protected your PC”): **More info › Run anyway**. It's built by GitHub Actions straight from this code.

## Building

GitHub Actions builds everything on Windows (`.github/workflows/build.yml`): tests (`tests/`), the service (`src/Backeddy.Service`), the app (`src/Backeddy.App`) and the Inno Setup installer (`installer/Backeddy.iss`). Locally, on Windows with the .NET 8 SDK:

```powershell
dotnet test tests/Backeddy.Core.Tests
dotnet publish src/Backeddy.App -c Release -r win-x64 --self-contained -p:Platform=x64 -o out/app
```

## Part of Backeddy

[Backeddy for Mac](https://github.com/BackEddy/mac) · [Backeddy Server](https://github.com/BackEddy/server) · MIT © 2026 [Eddy Antonini](https://eddyantonini.com.br)
