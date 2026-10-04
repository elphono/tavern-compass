#!/usr/bin/env bash
# Builds the harness, copies it to a Windows disk (a WinExe is not loaded comfortably from \\wsl$), and runs it.
#   ./launch.sh                 the window, for hands-on debugging, from C:\temp\BronzebeardHarness
#   ./launch.sh --selftest      checks the scene without anyone at the keyboard; prints the report, exit 0 when it all passes
#   ./launch.sh --screenshot    also writes C:\temp\BronzebeardHarness-ci\out\shot.png (the overlay at its own size)
# --selftest and --screenshot run from their own copy, C:\temp\BronzebeardHarness-ci: a harness window left open keeps
# its exe locked (a running exe cannot be replaced), and neither its folder nor its exe is touched by them. They also
# read their own layout, C:\temp\BronzebeardHarness-ci\layout.json (never written: the default layout), unless --layout
# is given, so that a capture does not depend on how the window's panels were arranged. The window's log and picture
# cache (%TEMP%\BronzebeardHarness) stay shared.
# Further arguments go to the harness: --size 1600x900, --layout <path>.
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

case " $* " in
  *" --selftest "*|*" --screenshot "*)
    headless=1
    folder='C:\temp\BronzebeardHarness-ci'
    dest=/mnt/c/temp/BronzebeardHarness-ci
    busy="a previous --selftest or --screenshot run is still going"
    ;;
  *)
    headless=0
    folder='C:\temp\BronzebeardHarness'
    dest=/mnt/c/temp/BronzebeardHarness
    busy="close the harness window first"
    ;;
esac

dotnet build "$repo/tools/BronzebeardHud.Harness" -c Release 2>&1 | grep -E "error|Build succeeded" | sort -u
mkdir -p "$dest"
cp -r "$repo/tools/BronzebeardHud.Harness/bin/Release/net48/." "$dest/" \
  || { echo "Copy to $folder failed: $busy (a running exe cannot be replaced)." >&2; exit 1; }

exe="$folder\\BronzebeardHud.Harness.exe"
if [ "$headless" = 1 ]; then
  rm -rf "$dest/out"
  layout=('--layout' "$folder\\layout.json")
  case " $* " in *" --layout "*) layout=() ;; esac
  args=$(printf "'%s'," "$@" "${layout[@]}" '--out' "$folder\\out")
  powershell.exe -NoProfile -Command "\$p = Start-Process -FilePath '$exe' -ArgumentList ${args%,} -Wait -PassThru; exit \$p.ExitCode" || code=$?
  [ -f "$dest/out/selftest.txt" ] && cat "$dest/out/selftest.txt"
  [ -f "$dest/out/error.txt" ] && cat "$dest/out/error.txt"
  [ -f "$dest/out/shot.png" ] && echo "screenshot: $dest/out/shot.png"
  exit "${code:-0}"
fi

# Start-Process, not "cmd.exe /c start": a window started through cmd stays attached to its console, and WSL then
# waits for it to close, so this script (and whoever called it) would never return.
# Counted on $#, not on the printed text: printf with no argument still prints the empty quotes, and an empty
# -ArgumentList is an error for Start-Process.
if [ "$#" -gt 0 ]; then
  args=$(printf "'%s'," "$@")
  powershell.exe -NoProfile -Command "Start-Process -FilePath '$exe' -ArgumentList ${args%,}" </dev/null
else
  powershell.exe -NoProfile -Command "Start-Process -FilePath '$exe'" </dev/null
fi
