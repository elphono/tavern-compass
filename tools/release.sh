#!/usr/bin/env bash
# Builds a release zip of the plugin against the HDT actually installed (GitHub only hosts HDT up to 1.55.6).
#   tools/release.sh [/mnt/c/Users/<user>/AppData/Local/HearthstoneDeckTracker/app-<version>/]
# Without an argument it builds against the newest HDT installed (highest app-<version>/ by `sort -V`,
# tools/hdt-install.sh); the argument forces another install.
# Refuses a dirty tree or an existing tag, runs the same checks as the CI, and writes
# out/TavernCompass-v<Version>-hdt-<hdt version>.zip (BronzebeardHud/ with the two DLLs and INSTALL.txt).
# It publishes nothing: the GitHub release is created afterwards (see the last line it prints).
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo"

# shellcheck source=tools/hdt-install.sh
. "$repo/tools/hdt-install.sh"
hdt="${1:-$(hdt_latest_app_dir)}"
[ -f "$hdt/HearthstoneDeckTracker.exe" ] || { echo "No HearthstoneDeckTracker.exe in $hdt" >&2; exit 1; }
hdt_version="$(hdt_version_of "$hdt")"
echo "HDT target: $hdt_version ($hdt)"

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)"
[ -n "$version" ] || { echo "No <Version> in Directory.Build.props" >&2; exit 1; }
tag="v$version"
[ -z "$(git status --porcelain)" ] || { echo "The tree is not clean: commit first." >&2; exit 1; }
! git rev-parse -q --verify "refs/tags/$tag" >/dev/null || { echo "Tag $tag already exists: raise <Version> in Directory.Build.props." >&2; exit 1; }

dotnet format whitespace --folder --verify-no-changes --exclude .claude .
dotnet test -warnaserror
dotnet build src/BronzebeardHud.HdtPlugin -c Release -warnaserror -p:HdtInstallDir="$hdt"

bin=src/BronzebeardHud.HdtPlugin/bin/Release/net48
name="TavernCompass-$tag-hdt-$hdt_version"
mkdir -p out
python3 - "$bin" "out/$name.zip" "$version" "$hdt_version" <<'PY'
import sys, zipfile
bin_dir, path, version, hdt = sys.argv[1:]
install = f"""Tavern Compass {version}, built against Hearthstone Deck Tracker {hdt}.

1. Close HDT.
2. Copy the BronzebeardHud folder into %AppData%\\HearthstoneDeckTracker\\Plugins\\
   (you get %AppData%\\HearthstoneDeckTracker\\Plugins\\BronzebeardHud\\BronzebeardHud.HdtPlugin.dll).
3. Start HDT, then enable "Tavern Compass" in Options > Plugins.

Do not add Newtonsoft.Json.dll: HDT loads its own. https://github.com/elphono/tavern-compass
"""
with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
    for dll in ("BronzebeardHud.HdtPlugin.dll", "BronzebeardHud.Stats.dll"):
        z.write(f"{bin_dir}/{dll}", f"BronzebeardHud/{dll}")
    z.writestr("BronzebeardHud/INSTALL.txt", install.replace("\n", "\r\n"))
PY
unzip -l "out/$name.zip"
sha256sum "out/$name.zip"
echo "gh release create $tag out/$name.zip --target $(git rev-parse HEAD) --title 'Tavern Compass $version' --draft"
