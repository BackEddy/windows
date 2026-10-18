<p align="center"><img src="assets/icon-256.png" width="112" alt="Backeddy"></p>

<h1 align="center">Backeddy for Windows</h1>

<p align="center"><b>Un Time Machine para Windows: backup de tus archivos cada hora, y cualquier archivo de vuelta tal como estaba cualquier día.</b><br>
<a href="README.md">English</a> · <a href="README.pt-BR.md">Português</a></p>

<p align="center"><a href="https://github.com/BackEddy/windows/releases"><b>⬇️ Descargar (Backeddy-Setup-0.1.x.exe)</b></a> · Windows 10 22H2 u 11, 64 bits · gratis</p>

---

## Antes de empezar: los riesgos

- **Esta es la versión 0.1 — la primera que hace backup de verdad.** La han probado muy pocas personas. Guarda **otra copia** de lo que no puedes perder hasta que demuestre que funciona.
- Se escribió con mucha ayuda de IA y la revisó una persona. El código es abierto: léelo, pruébalo y cuenta lo que falle.
- El instalador **todavía no tiene firma digital**, así que Windows avisa “Windows protegió su PC”: **Más información › Ejecutar de todas formas**. GitHub genera el archivo directamente desde este código.
- Se ofrece tal cual, sin garantías ([licencia MIT](LICENSE)).

## Qué hace

- Cada hora hace backup de **tu carpeta de usuario y de los otros discos internos** (D:, E:…). Los programas y el propio Windows quedan fuera — se pueden reinstalar. Puedes desmarcar carpetas que no necesitas (juegos, descargas grandes).
- También copia los archivos abiertos (instantáneas de volumen de Windows).
- Los backups van al **[Backeddy Server](https://github.com/BackEddy/server)** de tu casa (lo encuentra solo en la red; solo escribes la contraseña del servidor) o a un **disco externo**.
- **Restaurar archivos**: elige fecha y hora, navega por las carpetas y recupera un archivo o una carpeta entera — al lado de la actual (una copia con otro nombre) o en su lugar original.
- En un portátil, espera cuando la batería está por debajo del 20%; y espera a que aparezcan el disco o el servidor.
- Los avisos del servidor (un disco fallando, por ejemplo) aparecen junto al reloj, con “recordar mañana / en 3 / en 7 días”.
- Español, Português, English (sigue el idioma de Windows).

## Configurar

1. Descarga y abre `Backeddy-Setup-0.1.x.exe` (mira el aviso de arriba).
2. Backeddy se abre y pregunta **dónde guardar los backups**:
   - **Backeddy Server** — elige tu servidor de la lista (o escribe la dirección que aparece en su pantalla) y la contraseña del servidor. Necesita Backeddy Server **0.9 o más nuevo**.
   - **Disco externo** — elige el disco. Backeddy crea una carpeta `Backeddy` en él y muestra una **clave de recuperación**: anótala en papel. Sin ella nadie puede abrir esos backups (están cifrados) — ni nosotros.
3. Listo. El primer backup copia todo y puede tardar horas; los siguientes copian solo lo que cambió.

Un disco que ya tiene backups de Backeddy (de este PC antes de formatear, por ejemplo) pide la clave de recuperación y los backups antiguos vuelven a aparecer.

## ¿Cuánto espacio?

Calcula **el doble de lo que va al backup**: una copia completa y espacio para el historial. Backeddy guarda backups por hora durante un día, diarios durante un mes, semanales durante 3 meses, mensuales durante 2 años y anuales durante 5 años (en un Backeddy Server decide el servidor).

## Por dentro

C# / .NET 8. Un servicio de Windows (`Backeddy`, que se ejecuta como sistema) hace el trabajo con el motor abierto **[Kopia](https://kopia.io)** (Apache 2.0, incluido como `kopia.exe`); la app (WinUI 3) es la ventana y el icono en la bandeja, y habla con el servicio por un canal local. Solo el usuario de Windows que configuró Backeddy puede cambiarlo. Las contraseñas se guardan con DPAPI de Windows, legibles solo por el servicio. Registros: `C:\ProgramData\Backeddy\logs`.

Todavía no: juegos y programas, mudarse a un PC nuevo, imagen completa del sistema y pendrive de rescate.

## Parte de Backeddy

[Backeddy for Mac](https://github.com/BackEddy/mac) · [Backeddy Server](https://github.com/BackEddy/server) · MIT © 2026 [Eddy Antonini](https://eddyantonini.com.br)
