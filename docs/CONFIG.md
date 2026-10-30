# gameWE - CounterStrikeSharp plugin: settings

The plugin writes `addons/counterstrikesharp/configs/plugins/GameWE/GameWE.json`
with the default values on the first start. Change it, then reload the plugin
(`css_plugins reload GameWE`) or change the map. A wrong value falls back to
its default.

The names follow the ConVars of the gameWE SourceMod plugin
(`gamewe_message_prefix` → `MessagePrefix`, ...).

| Setting | Default | SourceMod ConVar | Meaning |
|---------|---------|------------------|---------|
| `LogUrl` | `""` | - | Where the plugin posts its log lines. Empty: the address the server got with `logaddress_add_http` (the collector sets it over RCON). Only `http://` or `https://`, no spaces or quotes. **Secret**: the path holds the collector's token. |
| `ProtectLogAddress` | `false` | `gamewe_protect_address` | Adds the log address again when someone removes it (`logaddress_delall_http`, `logaddress_del_http`) and turns logging on again after `log off`. |
| `MessagePrefix` | `""` | `gamewe_message_prefix` | Replaces the `[gameWE]` tag at the start of every gameWE chat line, also of the collector's answers. Color tags allowed, e.g. `{green}[Stats]`. At most 63 characters. |
| `BlockCommandsValues` | `""` | `gamewe_block_commands_values` | More chat commands to hide with `/`, separated by spaces; `clear` hides none, not even the defaults ([USAGE.md](USAGE.md#chat-commands)). |
| `ChatCooldown` | `1.0` | - | Seconds a player waits between two gameWE commands (0 - 30, 0 = off), so nobody can flood the collector. |
| `DamageDisplay` | `0` | `gamewe_damage_display` | Default for players: damage summary on death and at round end (0 off, 1 center panel, 2 chat). |
| `DamageDisplayAllow` | `true` | - | `false`: the damage display is off for everyone (no damage tracking) and the menu entry shows it as disabled. |
| `DisplaySpectatorinfo` | `0` | `gamewe_display_spectatorinfo` | Default for players: the stats of the spectated player (0 off, 1 on). |
| `DisplaySpectatorinfoAllow` | `true` | - | `false`: the spectator info is off for everyone and the menu entry shows it as disabled. |
| `MenuType` | `"center"` | - | `center`: menus in the center of the screen (W/S, E); `chat`: menus in chat (`!1` - `!9`). |
| `ConfigVersion` | `1` | - | Written by CounterStrikeSharp; do not change. |

The plugin also has the read-only ConVar `gamewe_plugin_version` (the
plugin version, as in the SourceMod and AMX Mod X plugins). It answers in
the server console and over RCON; players get nothing, and setting another
value fails with `Error: read only`. `css_plugins list` shows the same
version.

Example:

```json
{
  "LogUrl": "",
  "ProtectLogAddress": true,
  "MessagePrefix": "{green}[Stats]",
  "BlockCommandsValues": "",
  "ChatCooldown": 1.0,
  "DamageDisplay": 1,
  "DamageDisplayAllow": true,
  "DisplaySpectatorinfo": 0,
  "DisplaySpectatorinfoAllow": true,
  "MenuType": "center",
  "ConfigVersion": 1
}
```

## Players' settings

Every player chooses the displays in the settings menu (`!gamewe_settings`)
or with the chat commands in [USAGE.md](USAGE.md). The choices are saved in
`addons/counterstrikesharp/data/GameWE/players.json`, keyed by SteamID64;
only players with a setting other than the default are kept, and the file
holds no names or addresses. Delete the file to reset everyone.
