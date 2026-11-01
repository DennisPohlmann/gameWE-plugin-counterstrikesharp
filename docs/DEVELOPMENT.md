# gameWE - CounterStrikeSharp plugin: development

Building, testing and releasing the plugin. The code layout and the rules
for changes are in [CLAUDE.md](../CLAUDE.md).

## Build

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).
`CounterStrikeSharp.API` comes from NuGet.

```bash
make build      # build/stage/addons/counterstrikesharp/plugins/GameWE/GameWE.dll
make test       # xUnit tests (tests/GameWE.Tests)
make package    # dist/gamewe-counterstrikesharp-<version>.tar.gz and .zip, each with its .sha256
```

If `dotnet` is not in `PATH`, use `make DOTNET=/path/to/dotnet`. The version
comes from the git tag (`v<version>`) or the `VERSION` file. Warnings, including
the .NET analyzers (`AnalysisLevel latest-recommended`), are treated as
errors.

The package contains only `GameWE.dll` and `lang/`; CounterStrikeSharp
brings its own API at runtime.

## Code layout

| Path | Content |
|------|---------|
| `src/GameWE/Core/` | logic without CounterStrikeSharp types: log line format, weapon stats, command text, chat colors, player settings, damage tracker, HTTP sender |
| `src/GameWE/*.cs` | the plugin: load and unload, events, server commands, chat, menus, displays, config |
| `lang/*.json` | translations, converted from the SourceMod plugin's phrases (`{0}`, `{1}` ... placeholders) |
| `tests/GameWE.Tests/` | tests for `Core/` and the translations; they compile the `Core/` files directly |

## Tests

`make test` runs the xUnit v3 tests with `dotnet test` on Microsoft.Testing.Platform
(`global.json` turns it on; the .NET 10 SDK no longer runs xUnit v3 through VSTest).
`xunit.runner.visualstudio` stays in the project for IDEs. New logic in `Core/` gets a test; a new
phrase goes into all nine files in `lang/` (a test checks that every
language has every phrase with the same placeholders).

There are no automatic tests against a CS2 server. Before a release that
changes events, commands or menus, check the plugin on a CS2 test server:
`css_plugins list`, a few kills (`weaponstats` in the collector), `/rank`,
`!gamewe`, `!gamewe_settings`, the damage panel and the spectator info.

## CI

The GitHub workflow `.github/workflows/build.yml` runs `make test` and
`make package` for every push (any branch), every pull request and when
started by hand, and keeps the packages as a workflow artifact for 7 days;
nothing is published without a release run. Dependabot
(`.github/dependabot.yml`) opens weekly pull requests for GitHub Actions and
the NuGet packages. A new `CounterStrikeSharp.API` needs a check on a CS2
server before it is merged.

## Releases

Releases are made by [semantic-release](https://semantic-release.gitbook.io/)
(`.releaserc.json`) in the same workflow; nobody tags by hand. Pushing to
`main` does not release: a release is started by hand on `main`, in GitHub
under Actions → build → Run workflow with `release` ticked, or with
`gh workflow run build.yml --ref main -f release=true`. Run the `release-security-gate` skill
first (see `CLAUDE.md`). The run builds and tests everything again; only
when that passed does semantic-release read the
[Conventional Commits](https://www.conventionalcommits.org/) since the last
tag and decide the next version:

| Commit | Release |
|---|---|
| `feat: ...` | minor (`X.Y.Z` → `X.(Y+1).0`) |
| `fix: ...` (also `fix(security): ...`), `perf: ...` | patch (`X.Y.Z` → `X.Y.(Z+1)`) |
| `feat!: ...`, `fix!: ...` or a `BREAKING CHANGE:` footer | major (`X.Y.Z` → `(X+1).0.0`) |
| `docs`, `chore`, `ci`, `build`, `test`, `refactor`, `style` | no release |

For a release it writes `VERSION` and `CHANGELOG.md`, commits them as
`chore(release): v<version> [skip ci]`, tags `v<version>` and creates the GitHub
release as a draft. The workflow then builds that tag, uploads the packages
(`.tar.gz`, `.zip`) and their own `.tar.gz.sha256` and `.zip.sha256` to the
release and makes it public. The release stays a draft until all files are
in it, so it never shows up incomplete; if the job fails, re-run it. The
project file reads
`VERSION`, so the plugin's `ModuleVersion` follows. Never edit `VERSION` or
`CHANGELOG.md` by hand.
