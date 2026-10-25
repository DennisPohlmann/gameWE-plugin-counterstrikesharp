# Installing gameWE - CounterStrikeSharp plugin

The plugin runs on each Counter-Strike 2 server and works together with the
[gameWE collector](https://github.com/DennisPohlmann/gameWE-collector): it
sends extra log lines to the collector over HTTP and executes the collector's
replies (private chat, panels) that arrive over RCON. It has no database
access of its own.

Placeholders below: `203.0.113.5` is the collector host, `198.51.100.7:27015`
the game server, `change-me` your RCON password, `YOUR_TOKEN` the
collector's token for this server.

1. [Requirements](#requirements)
2. [Install](#install)
3. [Game server setup](#game-server-setup)
4. [Settings](#settings)
5. [Pairing with the collector](#pairing-with-the-collector)
6. [Reload](#reload)
7. [Update](#update)
8. [Uninstall](#uninstall)
9. [Coming from gameME](#coming-from-gameme)
10. [Troubleshooting](#troubleshooting)

## Requirements

- A Counter-Strike 2 dedicated server.
- [Metamod:Source 2.0](https://www.sourcemm.net/downloads.php?branch=dev) and
  [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
  **v378 or newer** (the release is built against `CounterStrikeSharp.API`
  1.0.378, .NET 10), in the Linux or Windows build that matches the
  server. Use the "with runtime" build of CounterStrikeSharp unless the
  .NET runtime is installed otherwise.
- A gameWE collector with CS2 support that has the server in
  `hlstats_Servers` with its RCON password ([pairing](#pairing-with-the-collector)).

## Install

The same package works on Linux and Windows servers: `GameWE.dll` is plain
.NET code. Take the `.tar.gz` on Linux and the `.zip` on Windows; both have
the same content.

1. Download the package and its `.sha256` from the releases and check it.

   Linux:

   ```bash
   sha256sum -c gamewe-counterstrikesharp-<version>.sha256
   ```

   Windows (PowerShell): compare the output with the `.zip` line of the
   `.sha256` file:

   ```powershell
   (Get-FileHash gamewe-counterstrikesharp-<version>.zip -Algorithm SHA256).Hash.ToLower()
   ```

2. Extract it into `game/csgo` (Windows: `game\csgo`), the folder that
   contains `addons/`.

   Linux:

   ```bash
   tar -xzf gamewe-counterstrikesharp-<version>.tar.gz -C /path/to/cs2/game/csgo
   ```

   Windows (PowerShell):

   ```powershell
   Expand-Archive gamewe-counterstrikesharp-<version>.zip -DestinationPath C:\path\to\cs2\game\csgo -Force
   ```

   It contains:

   | Path | Content |
   |------|---------|
   | `addons/counterstrikesharp/plugins/GameWE/GameWE.dll` | the plugin |
   | `addons/counterstrikesharp/plugins/GameWE/lang/*.json` | translations (en, de, ru, es, fr, pt, nl, da, pl) |
   | `README.md`, `CHANGELOG.md`, `LICENSE` | can be deleted from the game folder |

3. Start or restart the server. On the first start the plugin writes its
   config `addons/counterstrikesharp/configs/plugins/GameWE/GameWE.json`.

## Game server setup

CS2 accepts RCON only with the launch option `-usercon`, on Linux
(`./cs2.sh -dedicated -usercon ...`) as on Windows
(`cs2.exe -dedicated -usercon ...`). In `server.cfg`:

```
log on
rcon_password "change-me"    // the same as in the collector's hlstats_Servers
```

Do not add the collector's log address yourself unless the collector's docs
say so: the collector sets it over RCON with
`logaddress_add_http "http://203.0.113.5:41338/YOUR_TOKEN/27015"`, and the
plugin takes the address from that command. If the collector cannot reach
RCON, put the same address into `LogUrl` ([CONFIG.md](CONFIG.md)) and add
the `logaddress_add_http` line to `server.cfg`.

The address contains the collector's token. Keep `server.cfg` and
`GameWE.json` private and never post the address in chat, forums or
screenshots. The plugin never writes it to the log or the console.

## Settings

All settings are in `addons/counterstrikesharp/configs/plugins/GameWE/GameWE.json`,
see [CONFIG.md](CONFIG.md). The players' own choices are saved in
`addons/counterstrikesharp/data/GameWE/players.json` (SteamID64 and the
display settings only).

## Pairing with the collector

1. Register the server in the collector's `hlstats_Servers` (or in the
   gameWE admin area) with its address, port and RCON password and the CS2
   game.
2. The collector sends `logaddress_add_http` over RCON; from then on the
   engine and the plugin post their log lines to the collector.
3. Check: after a map change the collector shows the line
   `gameWE Plugin <version> (CounterStrikeSharp)` for the server, and a
   player who types `/rank` gets the answer in chat.

## Reload

`css_plugins reload GameWE` reloads the plugin and its config. A map change
also sends the start line again. Changes to `GameWE.json` need a reload.

## Update

Extract the new package over the old one and reload the plugin (or restart
the server). `GameWE.json` and `players.json` are kept; new settings get
their default value.

## Uninstall

Delete `addons/counterstrikesharp/plugins/GameWE/`,
`addons/counterstrikesharp/configs/plugins/GameWE/` and
`addons/counterstrikesharp/data/GameWE/`, then restart the server.

## Coming from gameME

The plugin offers gameME's server commands as `gameme_*` too and keeps
gameME's chat commands (`gameme_display`, `gameme_chat`, `gameme_auto`,
`gameme_lang`, `!gameme`). gameME had no CS2 plugin for CounterStrikeSharp,
so there is nothing to remove.

## Troubleshooting

| Problem | Check |
|---------|-------|
| `css_plugins list` does not show gameWE | CounterStrikeSharp version (v378+), the server console at start for a load error |
| No `weaponstats` in the collector | the collector has set its address (`logaddress_list_http` in the server console), or `LogUrl` is set; when the collector cannot be reached, the server console shows `gameWE: the collector cannot be reached (...), log lines dropped` (or `answered HTTP <code>`) at most once per minute |
| `/rank` gets no answer | the collector's RCON access (`-usercon`, password), the collector log |
| The panel (center text) does not show | another plugin uses the center HTML at the same time; try `MenuType: "chat"` for the menus |
