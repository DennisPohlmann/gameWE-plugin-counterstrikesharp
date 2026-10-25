---
name: release-security-gate
description: Full security pass over the gameWE CounterStrikeSharp plugin before release-relevant commits are pushed to main (semantic-release then bumps, tags and releases). Use whenever a release is being prepared; ends with a go/no-go report and never tags or pushes on its own.
---

# Release security gate (gameWE CounterStrikeSharp plugin)

Run all steps over the whole repository, not only the last diff. Report a
table with the 20 points (ok / finding / n.a.) and a go/no-go.

Releases are made by semantic-release in CI: every push to `main` that
contains a `feat`, `fix` or breaking-change commit (Conventional Commits)
bumps the version, writes `CHANGELOG.md`, tags `vX.Y.Z` and publishes the
GitHub release. So this gate runs **before pushing release-relevant
commits to `main`**, without asking first. Never tag by hand, never edit
`VERSION` or `CHANGELOG.md` by hand, and do not push to `main` yourself:
report the gate result and the version semantic-release will make
(`feat` → minor, `fix` → patch, `!`/`BREAKING CHANGE` → major), then ask
Dennis for the go.

## Steps

1. **Clean tree and version:** `git status` is clean; `VERSION` and the
   top `CHANGELOG.md` entry agree with the latest tag
   (`git describe --tags --abbrev=0`; before the first release: `VERSION`
   is `0.0.0` and `CHANGELOG.md` has only the title).
2. **Build and tests:** `make build` and `make test`; no warnings, all
   tests green. `make package` contains only `GameWE.dll`, `lang/`,
   README, CHANGELOG and LICENSE.
3. **Secrets in the whole history:** prefer
   `gitleaks detect --source . --log-opts="--all"`; otherwise
   `git log -p --all | grep -nEi '(rcon_password|password|secret|token)\s*[:= ]'`
   and a search for URLs with a token-like path.
4. **Command inventory:**
   `grep -n 'AddCommand\|AddCommandListener\|ConsoleCommand' src/GameWE/*.cs`
   - every collector command checks `player == null`, every player
   command is harmless or permission-gated.
5. **Injection review:** every `Server.ExecuteCommand(`, every `LogFormat`
   call with text from players or the collector, every
   `PrintToCenterHtml` (only via `ChatText.Html`); confirm escaping.
6. **Bounds:** queues, lists and dictionaries fed by players or the
   collector are bounded (`LogSender`, `UserIds`, weapon stats, settings
   store, menu entries); changes since the last tag:
   `git diff $(git describe --tags --abbrev=0) -- src`.
7. **Threads:** nothing on the game thread blocks (HTTP, file writes);
   background tasks never touch CounterStrikeSharp objects.
8. **Workflow:** `.github/workflows/build.yml` builds `main` and publishes
   only via semantic-release (no tag trigger); permissions are minimal;
   action versions are current (Dependabot).
9. **Test on a server** if one is available: hostile player name, chat
   flood of `/rank`, `gamewe_browse <id> javascript:...`, user id of a
   player who left, `logaddress_delall_http` with `ProtectLogAddress`.
10. **The 20 points:** go through the `security-check` skill's list for the
   whole plugin, with extra attention on changes since the last tag.
11. **Security and privacy audit:** run the `security-privacy-audit`
   skill over the whole repository, all branches and tags since the last
   release, the release commit messages and the `CHANGELOG.md` entry that
   semantic-release will write. Any `BLOCKED` item is a no-go.
12. **Report:** go/no-go, the `security-privacy-audit` summary, findings with file:line and fix, the version semantic-release will make, then ask Dennis
    for the go to push to `main`.
