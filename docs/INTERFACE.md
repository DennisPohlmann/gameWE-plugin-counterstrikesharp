# gameWE - CounterStrikeSharp plugin: interface

How the plugin talks to the [gameWE collector](https://github.com/DennisPohlmann/gameWE-collector):
the log lines it posts over HTTP and the server commands the collector sends
over RCON. The collector's side is described in its
[INTERFACE.md](https://github.com/DennisPohlmann/gameWE-collector/blob/main/docs/INTERFACE.md).

## Transport of the log lines

CS2 sends its own log to every address added with `logaddress_add_http`.
CounterStrikeSharp cannot add lines to that log, so the plugin posts its
lines itself:

- **Address:** the URL of the last `logaddress_add_http` command from the
  server console or RCON, normally
  `http://203.0.113.5:41338/YOUR_TOKEN/27015` (collector, token, game
  port), or `LogUrl` from the config. `logaddress_delall_http` and
  `logaddress_del_http` with that URL stop the sending (unless
  `ProtectLogAddress` adds it again).
- **Request:** `POST`, `Content-Type: text/plain; charset=utf-8`, one log
  line per line, each ending with `\n`, at most about 32 KiB per request.
  The token is only in the path; the collector identifies the server by
  the source address and the port in the path. No other headers are used.
- **Line format:** like CS2's HTTP log:
  `MM/DD/YYYY - HH:MM:SS.mmm - <line>`, server local time.
- **Sending:** once per second from a background task, one request at a
  time, 5 seconds timeout. Lines wait while no address is known (at most
  5000; older lines are dropped then). When the collector cannot be reached
  or answers with an error, the lines are dropped and the server console
  shows a warning at most once a minute, without the address.

The address holds the collector's token: the plugin never writes it to the
log, the console or the chat.

## Log lines

Player references are written as CS2 writes them:
`"Name<userid><[U:1:123]><CT>"` (teams `CT`, `TERRORIST`, `Spectator`,
`Unassigned`; bots `<BOT>`). In names and other player text `"` becomes `'`
and control characters are removed, so a name cannot fake an event.

```
gameWE Plugin <version> (CounterStrikeSharp)
"Name<2><[U:1:123]><CT>" triggered "weaponstats" (weapon "ak47") (shots "14") (hits "6") (kills "1") (headshots "1") (tks "0") (damage "412") (deaths "0")
"Name<2><[U:1:123]><CT>" triggered "headshot"
"Name<2><[U:1:123]><CT>" triggered "domination" against "Other<3><[U:1:456]><TERRORIST>"
"Name<2><[U:1:123]><CT>" triggered "revenge" against "Other<3><[U:1:456]><TERRORIST>"
"Name<2><[U:1:123]><CT>" triggered "mvp"
"Name<2><[U:1:123]><CT>" changed role to "ctm_sas"
"Name<2><[U:1:123]><CT>" say "/rank"
"Name<2><[U:1:123]><CT>" requested "hideranking" (value "0")
"Name<2><[U:1:123]><CT>" requested "spectatorinfo" (value "17")
"Name<2><[U:1:123]><CT>" setup "cl_language" (value "german")
"Name<2><[U:1:123]><CT>" setup "gamewe_lang" (value "de")
```

- The start line is sent when the plugin loads and at every map start. A
  collector can use it to know that this server has the plugin.
- `weaponstats` is written per weapon when a player dies, disconnects, at
  round end and when the plugin unloads. Grenades and fire count no shots.
- `say "/..."` only for the hidden commands; `!rank` and `rank` are in
  CS2's own log.
- `requested "hideranking"` asks for the player's ranking state (answer:
  `gamewe_hidestate`), `requested "spectatorinfo"` asks for the stats of
  the spectated player with a query id (answer: `gamewe_raw_message 1001`).
- `setup "cl_language"` is the game's language on connect, `setup
  "gamewe_lang"` the player's choice (`en`, `de`, `ru`, `es`, `fr`, `pt`,
  `nl`, `da`, `pl`) on connect and when it changes.
- Not sent, because CS2 logs it itself: kills, `attacked` lines (with hit
  group), assists, positions, bomb and hostage events. Not sent at all:
  `weaponstats2`.

## Server commands

The collector sends these over RCON (CS2 needs `-usercon`). They run only
from the server console or RCON; a player who types one gets nothing.
Players are addressed by their **userid**, the number in the first `<..>` of
a log line (`2` above). Arguments may be quoted or not. The message is
everything after the other arguments, so it may contain spaces. `\n` in a
message (backslash, n) is a line break. Lists of userids are limited to 64.

| Command | Does on CS2 |
|---------|-------------|
| `gamewe_psay <userid[,userid...]> [colored] "<message>"` | Private chat message. `colored`: `0` plain, `1` color tags, `2`/`3` like `1`. |
| `gamewe_csay "<message>"` | Center message to everybody. |
| `gamewe_msay <time> <userid> [handler] "<text>"` | Center HTML text for `time` seconds (1 - 60), lines separated by `\n`. |
| `gamewe_tsay <time> <userid> "<message>"` | Chat line (CS2 has no top left messages). |
| `gamewe_hint <userid[,userid...]> "<message>"` | Center message. |
| `gamewe_khint <userid[,userid...]> "<message>"` | Center HTML text for 5 seconds. |
| `gamewe_browse <userid[,userid...]> "<url>"` | The address as a chat line (only `http://` and `https://`). |
| `gamewe_swap <userid>` | Moves a player to the other team. |
| `gamewe_redirect <time> <userid[,userid...]> <address> "<reason>"` | The reason and `connect <address>` as chat lines. |
| `gamewe_player_action <userid> <action>` | Logs `triggered "<action>"` for a player. |
| `gamewe_team_action <team> <action> [objective]` | Logs `Team "<team>" triggered "<action>"`. |
| `gamewe_world_action <action>` | Logs `World triggered "<action>"`. |
| `gamewe_hidestate <userid> <hideranking>` | The player's ranking state for the settings menu: `0` shown, `1` hidden by an admin, `2` banned, `3` hidden by the player. |
| `gamewe_console_event <userid[,userid...]> "<message>"` | A console line for the listed players whose console events are on (`gamewe_display`). |
| `gamewe_chat_event <userid[,userid...]> "<message>"` | A chat line (with the message prefix) for the listed players whose global chat is on (`gamewe_chat`). |
| `gamewe_raw_message 1001 <query_id> <callers> <userid> "<text>"` | The spectator info for an open `requested "spectatorinfo"` query; unknown query ids are ignored. `1000 <query_id>` closes a query. |

Every command also exists with the `gameme_` prefix, so a collector or
script written for gameME works unchanged.

`gamewe_plugin_version` (no `gameme_` alias, as in the SourceMod plugin)
answers `gamewe_plugin_version = <version>` in the server console and over
RCON and cannot be changed; players get nothing.

With `colored 1` the plugin replaces the tags `x01`, `x03`, `x04`, `x05`,
`x07`, `x08` and the color names (`{green}`, ...) by CS2 chat colors.

The collector starts every `gamewe_psay` chat line with `[gameWE]`. When
`MessagePrefix` is set, the plugin puts the prefix in its place.
