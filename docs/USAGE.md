# gameWE - CounterStrikeSharp plugin: usage

What players can type in game and what the gameWE menu offers. The answers
come from the [gameWE collector](https://github.com/DennisPohlmann/gameWE-collector);
the plugin hides the commands from chat, sends them to the collector and
shows the replies.

## Chat commands

Players type these in chat, with `!`, `/` or nothing in front. With `/`
(`/rank`) the plugin hides the line from chat and sends it itself as a `say`
line, for example `"Name<2><[U:1:123]><CT>" say "/rank"`; with `!` (`!rank`)
or without a prefix the line stays visible to everybody and CS2 logs it. The
collector answers both, privately. A player can send at most one command per
`ChatCooldown` ([CONFIG.md](CONFIG.md)).

Hidden with `/`: `rank`, `skill`, `points`, `place`, `session`, `sdata`,
`session_data`, `kpd`, `kdratio`, `kdeath`, `next`, `load`, `status`,
`servers`, `top5`, `top10`, `top20`, `maps`, `map_stats`, `clans`,
`cheaters`, `statsme`, `weapons`, `weapon`, `action`, `actions`, `accuracy`,
`targets`, `target`, `kills`, `kill`, `player_kills`, `cmd`, `cmds`,
`commands`, `help`, `gstats`, `global_stats`, and the settings commands
`gamewe_atb 0|1`, `gamewe_hideranking [0|1]`, `gamewe_reset [0|1]`. The
`gameme_*` spellings are hidden too. `BlockCommandsValues` adds more.

CS2 has no in-game browser and no MOTD: answers that are web pages on other
games (`!statsme`, `!status`, `!servers`, ...) arrive as a chat line with the
address.

### Settings the plugin keeps

These are answered by the plugin itself (in the player's language, with the
message prefix) and saved per player ([CONFIG.md](CONFIG.md#players-settings));
the collector does not see them as commands. With `/` they are hidden from
chat, with `!` or without a prefix they stay visible. The `gameme_*`
spellings work as well.

| Command | Effect |
|---------|--------|
| `gamewe_display 0\|1` | console events off or on (default on): the collector's points of your kills and actions in the console |
| `gamewe_chat 0\|1` | global chat events off or on (default on): the collector's announcements of player and team actions with points |
| `gamewe_auto start\|end\|kill <command>` | a command the plugin sends for you at round start (`start`), round end (`end`) or when you die (`kill`), as if you typed `/<command>`. Only `rank`, `skill`, `points`, `kpd`, `kdratio`, `session`, `sdata`, `next`, `top5`, `top10`, `top20`, `statsme`; `gamewe_auto start clear` removes one, `gamewe_auto clear` all, `gamewe_auto` alone lists them |
| `gamewe_lang en\|de\|ru\|es\|fr\|pt\|nl\|dk\|pl` | the language of the plugin's menus and messages and of the collector's answers (`dk` is Danish). The plugin sets the player's CounterStrikeSharp language and sends `setup "gamewe_lang"` to the collector, on every connect too. Without a choice the game's language (`cl_language`) is used |

## Menu

`!gamewe` or `!gamewe_menu` (also `!gameme`) opens the gameWE menu. With
`MenuType: "center"` it is shown in the center of the screen (W/S to move,
E to select); with `"chat"` in chat (`!1` - `!9`). The entries send the
matching command for the player, for example `/rank` or `/top10`;
"Chat-Events" switches console events and global chat.

The entry "Settings" (also `!gamewe_settings` or the console command
`css_gamewe_settings`) opens the settings menu:

- **Ranking:** one entry that fits the player's state: "Hide me in the
  ranking", "Show me in the ranking", or a disabled "Hidden from the
  ranking" when an admin or a ban hid the player. It sends
  `/gamewe_hideranking 1` or `0`; the collector answers with the new state.
  The plugin asks the collector for the state (`requested "hideranking"`)
  when the menu opens the first time.
- **Damage display:** server default, off, panel, chat. With "panel" the
  summary of the round (attackers, victims, killer) is shown in the center on
  death and at round end; with "chat" the killer, the damage and the
  killer's health left are shown in chat on death.
- **Spectator info:** server default, off, on. Shows the stats of the
  spectated player (first person or third person view) in the center.
- **Console events** and **Global chat:** on, off.

Selecting an entry switches to the next value and shows the menu again.
"Server default" follows `DamageDisplay` / `DisplaySpectatorinfo` in the
config; the server can turn a display off for everyone (`...Allow: false`).

## Differences to the SourceMod plugin

| SourceMod plugin | CS2 |
|------------------|-----|
| MOTD web pages (`gamewe_browse`) | the address as a chat line |
| key hint box (`gamewe_khint`), menu panels (`gamewe_msay`) | center HTML text for the given time |
| top left message (`gamewe_tsay`) | chat line |
| redirect dialog (`gamewe_redirect`) | chat line with `connect <address>` to type in the console |
| `weaponstats2` (hit groups), positions (`located on`) | not sent: CS2's own log has hit groups and positions |
| natives and forwards for other plugins | not available yet |
