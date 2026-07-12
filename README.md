# Jellyfin ES-DE Launcher

A Jellyfin plugin that adds a **Retro Gaming** card to the home screen, allowing you to launch [EmulationStation DE](https://es-de.org) directly from the Jellyfin interface.

![Jellyfin 10.11.11](https://img.shields.io/badge/Jellyfin-10.11.11-00a4dc) ![.NET 9](https://img.shields.io/badge/.NET-9.0-512bd4) ![License: MIT](https://img.shields.io/badge/License-MIT-green)

## Features

- **Zero configuration** — the plugin auto-detects ES-DE on the system, no setup needed
- **Native look** — the card matches Jellyfin's library cards (same size, focus zoom, gamepad navigation)
- **Silent when unused** — if ES-DE isn't installed, nothing is shown
- **Status tracking** — the card reflects whether ES-DE is running and re-enables when it exits

## Screenshots

_Coming soon_

## Installation

1. Download the latest release from the [Releases](../../releases) page
2. Extract the archive into your Jellyfin plugins directory:
   - **Linux**: `/var/lib/jellyfin/plugins/EsDe/`
   - **Windows**: `C:\ProgramData\Jellyfin\Server\plugins\EsDe\`
3. Restart Jellyfin Server
4. The Retro Gaming card appears on the home screen if ES-DE is detected

### ES-DE detection paths

The plugin looks for the ES-DE binary in these locations:

| OS      | Paths checked                                                                                 |
| ------- | --------------------------------------------------------------------------------------------- |
| Linux   | `/usr/bin/es-de`, `/usr/local/bin/es-de`, `~/.local/bin/es-de`, AppImage in `~/Applications/` |
| Windows | `C:\Program Files\ES-DE\ES-DE.exe`, `%LOCALAPPDATA%\ES-DE\ES-DE.exe`                          |

## Building from source

```bash
git clone https://github.com/MBach/jellyfin-plugin-esde.git
cd jellyfin-plugin-esde
dotnet build
```

The output (`Jellyfin.Plugin.EsDe.dll` + `meta.json` + `Web/esde.js`) is in `bin/Debug/net9.0/`. Copy the contents to your Jellyfin plugins directory.

## Project structure

```
├── meta.json                 # Plugin metadata (read by Jellyfin)
├── Plugin.cs                 # Plugin entry point
├── Api/
│   └── EsDeController.cs     # REST API (Status / Launch / Stop)
├── ScriptInjector.cs         # Injects esde.js into the web client
├── Jellyfin.Plugin.EsDe.csproj
└── Web/
    └── esde.js               # Home screen card (injected client-side)
```

## How it works

The plugin has two parts:

**Server side** (C#) — registers REST endpoints (`/EsDe/Status`, `/EsDe/Launch`) and auto-injects a `<script>` tag into Jellyfin's `index.html` at startup.

**Client side** (JS) — clones an existing library card from the home page to inherit Jellyfin's native styling and gamepad focus behavior, replaces the content with a gamepad icon, and wires the click to the Launch endpoint.

## License

MIT

## Acknowledgments

Built with the help of [Claude](https://claude.ai) by Anthropic.
