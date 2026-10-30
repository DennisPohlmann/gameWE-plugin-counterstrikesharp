# gameWE - CounterStrikeSharp plugin

gameWE is a statistics system for Source, Source 2 and GoldSrc game servers,
in the style of gameME / HLstatsX. The **CounterStrikeSharp plugin** runs on
each Counter-Strike 2 server and works together with the
[gameWE collector](https://github.com/DennisPohlmann/gameWE-collector). It

- sends extra statistics to the collector: per-weapon accuracy and damage
  (`weaponstats`), headshots, dominations, revenges, MVPs, roles and the
  players' languages,
- offers the server commands the collector uses to answer players in game
  (`gamewe_psay`, `gamewe_msay`, ...; also as `gameme_*`),
- hides the stats chat commands (`/rank`, `/top10`, ...) from chat, but still
  sends them to the collector,
- shows the gameWE menu (`!gamewe`), a damage summary on death and the stats
  of the spectated player; every player chooses these displays in the
  settings menu (`!gamewe_settings`), the config only sets the default,
- keeps the players' own settings of gameME's commands: console and chat
  events (`gamewe_display`, `gamewe_chat`), auto commands at round start,
  round end and death (`gamewe_auto`) and the language (`gamewe_lang`, 9
  languages, also for the collector's answers).

It is a port of the [gameWE SourceMod plugin](https://github.com/DennisPohlmann/gameWE-plugin-sourcemod)
(a fork of the gameME plugin 5.0.0) to C# and CounterStrikeSharp, because
SourceMod does not run on CS2. The log format and the command interface stay
compatible with gameME. It has no database access of its own.

**How the log lines reach the collector:** CS2 sends its own log over HTTP
(`logaddress_add_http`), and CounterStrikeSharp cannot add lines to it. So
the plugin posts its lines over HTTP to the same address the collector set.
The address contains the collector's token: treat it like a password.

Current version: see `VERSION` and [CHANGELOG.md](CHANGELOG.md).

## Requirements

- a Counter-Strike 2 dedicated server on Linux or Windows with
  [Metamod:Source 2.0](https://www.sourcemm.net/downloads.php?branch=dev) and
  [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
  **v378 or newer** (the "with runtime" build, or the .NET runtime it needs)
- a gameWE collector with CS2 support, with the server registered in its
  `hlstats_Servers` (address, port and RCON password)

## Quick start

1. Extract `gamewe-counterstrikesharp-<version>.tar.gz` (Linux) or `.zip`
   (Windows) from the releases into the `game/csgo` folder, the folder that
   contains `addons/` (on Windows e.g. with `Expand-Archive`):

   ```bash
   tar -xzf gamewe-counterstrikesharp-<version>.tar.gz -C /path/to/cs2/game/csgo
   ```

2. Start the server with RCON enabled (`-usercon`) and add to `server.cfg`:

   ```
   log on
   rcon_password "change-me"            // the same as in the collector's hlstats_Servers
   ```

   The collector adds its HTTP log address over RCON; the plugin uses the
   same address.

3. Restart the server or change the map; `css_plugins list` shows `gameWE`.

Every step in detail: [docs/INSTALLATION.md](docs/INSTALLATION.md).

## Documentation

| File | Content |
|------|---------|
| [docs/INSTALLATION.md](docs/INSTALLATION.md) | installation step by step: requirements, server setup, pairing with the collector, update, removal, troubleshooting |
| [docs/CONFIG.md](docs/CONFIG.md) | the settings in `configs/plugins/GameWE/GameWE.json` |
| [docs/USAGE.md](docs/USAGE.md) | chat commands, the gameWE menu and the settings menu |
| [docs/INTERFACE.md](docs/INTERFACE.md) | the HTTP log lines and the collector's server commands |
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | building, tests, CI and releases |
| [CHANGELOG.md](CHANGELOG.md) | changes per version |

## gameWE components

| Repository | Part |
|------------|------|
| [gameWE-collector](https://github.com/DennisPohlmann/gameWE-collector) | receives the game server logs and fills the database |
| [gameWE-web](https://github.com/DennisPohlmann/gameWE-web) | statistics website and admin area |
| [gameWE-plugin-sourcemod](https://github.com/DennisPohlmann/gameWE-plugin-sourcemod) | game server plugin for Source games |
| [gameWE-plugin-amxmodx](https://github.com/DennisPohlmann/gameWE-plugin-amxmodx) | game server plugin for GoldSrc games |
| [gameWE-plugin-counterstrikesharp](https://github.com/DennisPohlmann/gameWE-plugin-counterstrikesharp) | game server plugin for Counter-Strike 2 (this repository) |

## Building

```bash
make build      # build/stage/addons/counterstrikesharp/plugins/GameWE/GameWE.dll
make test       # xUnit tests
make package    # dist/gamewe-counterstrikesharp-<version>.tar.gz and .zip, each with its .sha256
```

Requirements and the release process: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## License

gameWE is free software under the **GNU General Public License version 3 or later**,
see [LICENSE](LICENSE).

It is a port of the gameWE SourceMod plugin, which is based on the gameME
plugin, Copyright (C) 2007-2020 TTS Oetzel & Goerz GmbH, published under the
GPL version 2 or later. The translations come from the gameME plugin.

It is built against [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
(`CounterStrikeSharp.API`, MIT license), which is not included in the
package. The tests use [xUnit v3](https://xunit.net/) (Apache-2.0) and the
Microsoft .NET Test SDK (MIT); they are not included either.
