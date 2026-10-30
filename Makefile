# gameWE - CounterStrikeSharp plugin
#   make build | test | package | clean
#
# Needs the .NET 10 SDK (dotnet in PATH, or set DOTNET=/path/to/dotnet).
# CounterStrikeSharp.API comes from NuGet; only GameWE.dll and lang/ go
# into the package, CounterStrikeSharp itself ships the API.
#
# The version comes from the git tag on HEAD (v1.2.3 -> 1.2.3), otherwise
# from the VERSION file. Override with: make package VERSION=1.2.3-rc1

DOTNET     ?= dotnet
GIT_TAG    := $(shell git describe --tags --exact-match --match 'v*' 2>/dev/null)
VERSION    ?= $(if $(GIT_TAG),$(patsubst v%,%,$(GIT_TAG)),$(shell cat VERSION))

STAGE    := build/stage
PLUGIN   := $(STAGE)/addons/counterstrikesharp/plugins/GameWE
PKG      := gamewe-counterstrikesharp-$(VERSION)
DIST     := dist

export DOTNET_CLI_TELEMETRY_OPTOUT := 1
export DOTNET_NOLOGO := 1

.PHONY: all build test package clean

all: build

# warnings are errors (TreatWarningsAsErrors in the project)
build:
	rm -rf $(STAGE)
	$(DOTNET) build src/GameWE/GameWE.csproj -c Release -p:Version=$(VERSION) -o build/out
	install -d $(PLUGIN)/lang
	install -m 0644 build/out/GameWE.dll $(PLUGIN)/
	install -m 0644 lang/*.json $(PLUGIN)/lang/

test:
	$(DOTNET) test --project tests/GameWE.Tests/GameWE.Tests.csproj -c Release

package: build
	install -m 0644 README.md CHANGELOG.md LICENSE $(STAGE)/
	install -d $(DIST)
	rm -f $(DIST)/$(PKG).tar.gz $(DIST)/$(PKG).zip $(DIST)/$(PKG).sha256 \
		$(DIST)/$(PKG).tar.gz.sha256 $(DIST)/$(PKG).zip.sha256
	tar --sort=name --owner=0 --group=0 --numeric-owner -C $(STAGE) -czf $(DIST)/$(PKG).tar.gz addons README.md CHANGELOG.md LICENSE
	cd $(STAGE) && zip -qrX ../../$(DIST)/$(PKG).zip addons README.md CHANGELOG.md LICENSE
	cd $(DIST) && sha256sum $(PKG).tar.gz > $(PKG).tar.gz.sha256
	cd $(DIST) && sha256sum $(PKG).zip > $(PKG).zip.sha256
	@echo "built $(DIST)/$(PKG).tar.gz and $(DIST)/$(PKG).zip"

clean:
	rm -rf build $(DIST) src/GameWE/bin src/GameWE/obj tests/GameWE.Tests/bin tests/GameWE.Tests/obj
