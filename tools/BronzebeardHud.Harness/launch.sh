#!/usr/bin/env bash
# Builds the harness, copies it to a Windows disk (C:\temp\BronzebeardHarness: a WinExe is not loaded comfortably from
# \\wsl$), and runs it.
#   ./launch.sh                 the window, for hands-on debugging
#   ./launch.sh --selftest      checks the scene without anyone at the keyboard; prints the report, exit 0 when it all passes
#   ./launch.sh --screenshot    also writes C:\temp\BronzebeardHarness\out\shot.png (the overlay at its own size)
# Further arguments go to the harness: --size 1600x900, --layout <path>.
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
dest=/mnt/c/temp/BronzebeardHarness

dotnet build "$repo/tools/BronzebeardHud.Harness" -c Release 2>&1 | grep -E "error|Build succeeded" | sort -u
mkdir -p "$dest"
cp -r "$repo/tools/BronzebeardHud.Harness/bin/Release/net48/." "$dest/" \
  || { echo "Copy failed: close the harness window first (a running exe cannot be replaced)." >&2; exit 1; }

exe='C:\temp\BronzebeardHarness\BronzebeardHud.Harness.exe'
case " $* " in
  *" --selftest "*|*" --screenshot "*)
    rm -rf "$dest/out"
    args=$(printf "'%s'," "$@" '--out' 'C:\temp\BronzebeardHarness\out')
    powershell.exe -NoProfile -Command "\$p = Start-Process -FilePath '$exe' -ArgumentList ${args%,} -Wait -PassThru; exit \$p.ExitCode" || code=$?
    [ -f "$dest/out/selftest.txt" ] && cat "$dest/out/selftest.txt"
    [ -f "$dest/out/error.txt" ] && cat "$dest/out/error.txt"
    [ -f "$dest/out/shot.png" ] && echo "screenshot: $dest/out/shot.png"
    exit "${code:-0}"
    ;;
  *)
    # Start-Process, not "cmd.exe /c start": a window started through cmd stays attached to its console, and WSL then
    # waits for it to close, so this script (and whoever called it) would never return.
    args=$(printf "'%s'," "$@")
    if [ -n "$args" ]; then
      powershell.exe -NoProfile -Command "Start-Process -FilePath '$exe' -ArgumentList ${args%,}" </dev/null
    else
      powershell.exe -NoProfile -Command "Start-Process -FilePath '$exe'" </dev/null
    fi
    ;;
esac
