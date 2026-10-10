#!/usr/bin/env bash
# Builds the plugin in Release against the newest HDT installed and copies it into HDT's plugin folder, from WSL.
#   tools/deploy.sh [--hdt-dir DIR] [--plugins-dir DIR] [--dry-run]
# DIR for --hdt-dir is an HDT install, .../AppData/Local/HearthstoneDeckTracker/app-<version>/; by default the newest
# app-<version>/ found there (`sort -V`, tools/hdt-install.sh), so a build always follows HDT's auto-updater.
# --plugins-dir defaults to <same Windows profile>/AppData/Roaming/HearthstoneDeckTracker/Plugins/BronzebeardHud.
# Copies BronzebeardHud.HdtPlugin.dll and BronzebeardHud.Stats.dll, never Newtonsoft.Json.dll (HDT loads its own),
# then compares the SHA-1 of both sides and fails if they differ. The build is judged on its exit code (-warnaserror).
# HDT only loads plugins at startup: restart it afterwards. If HDT is running (tasklist.exe), the script says so and
# copies once (it worked with HDT 1.58.10 running, 2026-10-10); if the copy fails it stops, never retries nor kills HDT.
# --dry-run shows the HDT version, the folders and what is deployed now; it builds and writes nothing.
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=tools/hdt-install.sh
. "$repo/tools/hdt-install.sh"

hdt=""
plugins=""
dry_run=0
while [ $# -gt 0 ]; do
    case "$1" in
        --hdt-dir) hdt="${2:?--hdt-dir needs an app-<version>/ folder}"; shift 2 ;;
        --plugins-dir) plugins="${2:?--plugins-dir needs a folder}"; shift 2 ;;
        --dry-run) dry_run=1; shift ;;
        -h|--help) sed -n '2,11p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Unknown argument: $1 (see --help)" >&2; exit 2 ;;
    esac
done

[ -n "$hdt" ] || hdt="$(hdt_latest_app_dir)"
hdt="${hdt%/}/"
[ -f "${hdt}HearthstoneDeckTracker.exe" ] || { echo "No HearthstoneDeckTracker.exe in $hdt" >&2; exit 1; }
hdt_version="$(hdt_version_of "$hdt")"
[ -n "$plugins" ] || plugins="$(hdt_plugins_dir)"
plugins="${plugins%/}"

dlls=(BronzebeardHud.HdtPlugin.dll BronzebeardHud.Stats.dll)
bin="$repo/src/BronzebeardHud.HdtPlugin/bin/Release/net48"

echo "HDT target:  $hdt_version ($hdt)"
echo "Plugins dir: $plugins"
echo "Source:      $(git -C "$repo" rev-parse --short HEAD)$([ -z "$(git -C "$repo" status --porcelain)" ] || echo ' (uncommitted changes)')"

hdt_running=unknown
if command -v tasklist.exe >/dev/null 2>&1; then
    # CSV keeps the full image name (the table format cuts it to 25 characters).
    if tasklist.exe /FI "IMAGENAME eq HearthstoneDeckTracker.exe" /FO CSV /NH 2>/dev/null \
        | tr -d '\r' | grep -qi '^"HearthstoneDeckTracker.exe"'; then
        hdt_running=yes
        echo "HDT is running: it keeps the plugin it loaded; close it before deploying, or restart it afterwards."
    else
        hdt_running=no
    fi
else
    echo "tasklist.exe not found: cannot tell whether HDT is running."
fi

if [ "$dry_run" -eq 1 ]; then
    echo "Dry run: would build"
    echo "  dotnet build src/BronzebeardHud.HdtPlugin -c Release -warnaserror -p:HdtInstallDir=$hdt"
    echo "and copy ${dlls[*]} into $plugins. Deployed now:"
    for dll in "${dlls[@]}"; do
        if [ -f "$plugins/$dll" ]; then
            echo "  $(sha1sum "$plugins/$dll" | cut -d' ' -f1)  $dll  $(date -r "$plugins/$dll" '+%F %T')"
        else
            echo "  (absent)  $dll"
        fi
    done
    exit 0
fi

cd "$repo"
if ! dotnet build src/BronzebeardHud.HdtPlugin -c Release -warnaserror -p:HdtInstallDir="$hdt"; then
    echo "Build failed against HDT $hdt_version: nothing copied." >&2
    exit 1
fi
for dll in "${dlls[@]}"; do
    [ -f "$bin/$dll" ] || { echo "Build output missing: $bin/$dll" >&2; exit 1; }
done

mkdir -p "$plugins"
for dll in "${dlls[@]}"; do
    if ! cp "$bin/$dll" "$plugins/$dll"; then
        [ "$hdt_running" = no ] || echo "Copy of $dll failed while HDT may be running: close HDT and run again." >&2
        exit 1
    fi
done
echo "Copied at $(date '+%F %T %Z')"

mismatch=0
for dll in "${dlls[@]}"; do
    built="$(sha1sum "$bin/$dll" | cut -d' ' -f1)"
    deployed="$(sha1sum "$plugins/$dll" | cut -d' ' -f1)"
    echo "  $dll  built $built  deployed $deployed"
    [ "$built" = "$deployed" ] || mismatch=1
done
[ "$mismatch" -eq 0 ] || { echo "SHA-1 mismatch between the build and the plugin folder." >&2; exit 1; }
[ ! -f "$plugins/Newtonsoft.Json.dll" ] \
    || echo "Warning: $plugins/Newtonsoft.Json.dll exists; HDT loads its own, remove it." >&2
echo "Deployed for HDT $hdt_version. $([ "$hdt_running" = yes ] && echo 'Restart HDT' || echo 'Start HDT') to load it."
