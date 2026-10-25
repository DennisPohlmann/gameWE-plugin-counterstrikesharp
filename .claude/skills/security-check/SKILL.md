---
name: security-check
description: Security checklist for every change in the gameWE CounterStrikeSharp plugin (C#, HTTP log lines for the collector, RCON server commands). Use before finishing, committing or opening a PR for any .cs, translation, build or workflow change.
---

# Security check for a change (gameWE CounterStrikeSharp plugin)

Scope: the current diff (`git diff` plus staged and untracked files). Walk
the points below, only as deep as the diff reaches, and report the result.
Many points of the original web-app list do not apply to a game server
plugin; they are mapped to what does.

## Steps

1. Read the diff: `git status --short`, `git diff HEAD`.
2. List new or changed commands (`AddCommand`, `AddCommandListener`,
   `[ConsoleCommand]`), event handlers, config fields, `LogFormat` lines,
   `Server.ExecuteCommand` calls and HTTP requests.
3. For each point below that the diff touches, check it in the code.
4. Build and test: `make build` (warnings are errors) and `make test`. If
   the .NET SDK is missing, say so.
5. Quick secret grep on the diff:
   `git diff HEAD | grep -nEi '(rcon_password|password|secret|token)\s*[:= ]'`.
6. Fix what you find, then report: touched points, findings with
   file:line, what you fixed, what is left.

## The checklist, adapted to the plugin

1. **Secrets / keys:** no RCON passwords, server IPs of real servers,
   collector tokens or log URLs with a real token in code, configs, tests
   or docs. The log URL is never logged or printed (`LogSender` warnings
   must not contain it).
2. **Least privilege:** new collector commands run only for
   `player == null`; player-facing commands (`css_*`) must be harmless or
   check a permission (`AdminManager.PlayerHasPermissions`) with the
   lowest fitting flag.
3. **IDOR:** a command with a user id acts only on that player after
   `PlayerByUserId` (valid, connected; not a bot where needed); lists of ids
   (`1,2,3`) are each checked and bounded (`CommandText.UserIds`).
4. **Git secrets:** see step 5; build output (`build/`, `dist/`, `bin/`,
   `obj/`) stays ignored.
5. **Admin routes:** no admin-only action (swap, redirect, browse,
   logaddress changes) reachable from player chat or client console.
6. **User isolation:** private messages and menus go only to the target
   player; spectator/raw-message data of one player is not sent to others.
7. **Rate limits:** chat triggers that make the collector answer
   (`rank`, `top10`, `session`, ...) and settings changes go through the
   per-player cooldown (`TriggerAllowed`); the HTTP queue stays bounded
   (`LogSender.MaxQueuedLines`).
8. **Storage:** files the plugin writes (`players.json`) have fixed paths
   under the plugin's data folder, never names from input; written via a
   temporary file and off the game thread.
9. **Input validation:** command arguments are parsed with `CommandText`
   (`Int` with range checks for time, team, handler); strings from the
   collector are length-limited; config values go through
   `GameWEConfig.Validate`, stored settings through
   `PlayerSettings.Validated`.
10. **Unauthenticated entry points:** chat (`say`, `say_team`) is the only
    player input; it may only be matched against fixed triggers.
11. **Injection (SQL equivalent):** log injection, command injection and
    HTML injection. Player text in log lines only via `LogFormat`
    (`Sanitize`); nothing from players or the collector reaches
    `Server.ExecuteCommand` unchecked (`;`, `"`, newlines); center HTML
    only via `ChatText.Html`.
12. **Sensitive logs:** no IP addresses or SteamIDs beyond what the CS2
    log format already carries; no RCON output and no log URL in logs.
13. **Field tampering:** values from players never decide points, ranks or
    teams in the log lines (the collector computes them).
14. **Uploads:** not applicable.
15. **Server logic:** the collector, not the plugin, is the source of
    truth for stats; the plugin only reports events.
16. **Trim responses:** messages shown to players contain only their own or
    public stats.
17. **Auth sessions:** not applicable (RCON auth is the server's); the log
    address protection (`ProtectLogAddress`) must keep working.
18. **Dependencies:** a change to `CounterStrikeSharp.API`, the test
    packages, the .NET version or `.github/workflows/build.yml` (actions
    versions) gets a note; new NuGet packages need a license check.
19. **Record access:** query ids of `gamewe_raw_message` are checked
    against the open queries before the answer is shown.
20. **Attack your own change:** try one hostile input (name with `"`, `<`,
    newline, 128 chars; userid of a disconnected player; `gamewe_browse`
    with `javascript:`; `logaddress_add_http` with `;`) and check the code
    path, ideally with a test in `tests/GameWE.Tests`.
