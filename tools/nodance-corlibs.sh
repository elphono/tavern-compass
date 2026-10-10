#!/usr/bin/env bash
# Gets the complete class libraries ("unstripped corlibs") that BepInEx needs in Hearthstone, for tools/nodance-deploy.sh.
#   tools/nodance-corlibs.sh [--game-dir DIR] [--dry-run]
# DIR defaults to $HEARTHSTONE_DIR, else /mnt/e/JEUX/Hearthstone. Hearthstone ships its .NET class libraries stripped by
# Unity's linker: its mscorlib has no Module.GetPEKind, which BepInEx 5's preloader calls first, so BepInEx never starts
# (docs/journal/2026-10-10-bepinex-hearthstone.md). The complete Windows libraries of the game's exact Unity version (read
# in its UnityPlayer.dll) are in Unity's official Linux editor archive, profile unityjit-win32: the game's stripped files
# are subsets of these, and its Mono runtime is that version's, byte for byte. The corlibs of unity.bepinex.dev are NOT
# these: taken from the editor's 4.5 profile, they are the Unix build (P/Invoke into System.Native) since Unity 2021.2.
# The script downloads the editor archive once (about 4.5 GB) into lib/unity/<version>/ (ignored by git), checks it
# against the MD5 that Unity's release API publishes, extracts the libraries into lib/unity/<version>/unstripped_corlib/,
# refuses a Unix build (System.Native in mscorlib), and checks every SHA-256 against tools/nodance-corlibs.sha256. For a
# Unity version that file does not list yet it prints the lines to add and exits 3: review them, add them, commit.
# Once the libraries match the manifest, the archive is no longer needed (delete it to get the space back).
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

game="${HEARTHSTONE_DIR:-/mnt/e/JEUX/Hearthstone}"
dry_run=0
while [ $# -gt 0 ]; do
  case "$1" in
    --game-dir) game="${2:?--game-dir needs a folder}"; shift 2 ;;
    --dry-run) dry_run=1; shift ;;
    -h|--help) sed -n '2,14p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "unknown argument: $1 (see --help)" >&2; exit 2 ;;
  esac
done
game="${game%/}"

# BepInEx's own list of corlibs (UnityDataMiner's _importantCorlibs), kept to those the game ships: each stripped
# library of the game is replaced by its complete self, so no stripped one is left calling into a complete one.
names=(Mono.Security mscorlib System.Configuration System.Core System.Data System System.Net.Http System.Numerics
  System.Runtime.Serialization System.Security System.Xml System.Xml.Linq)
profile="Editor/Data/MonoBleedingEdge/lib/mono/unityjit-win32"
manifest="$repo/tools/nodance-corlibs.sha256"

say() { printf '%s\n' "$*"; }
fail() { say "STOP: $*" >&2; exit 1; }
for tool in strings curl tar xz python3 sha256sum; do
  command -v "$tool" > /dev/null || fail "$tool is needed"
done

[ -f "$game/UnityPlayer.dll" ] || fail "no UnityPlayer.dll in $game: pass --game-dir or set HEARTHSTONE_DIR"
# UnityPlayer.dll's ProductVersion: "6000.3.11f1 (3000ef702840)", the version and its changeset.
product="$(strings -el "$game/UnityPlayer.dll" | sed -n '/^ProductVersion$/{n;p;q}')"
[[ "$product" =~ ^([0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+)\ \(([0-9a-f]{12})\)$ ]] || fail "cannot read the Unity version in UnityPlayer.dll (read: '$product')"
unity="${BASH_REMATCH[1]}"
changeset="${BASH_REMATCH[2]}"
dir="$repo/lib/unity/$unity"
out="$dir/unstripped_corlib"
archive="$dir/Unity-$unity.tar.xz"
url="https://download.unity3d.com/download_unity/$changeset/LinuxEditorInstaller/Unity-$unity.tar.xz"
say "Game: $game, Unity $unity (changeset $changeset)"

listed="$(awk -v p="$unity/unstripped_corlib/" 'index($2, p) == 1' "$manifest" 2>/dev/null || true)"
# Lines in the manifest's format ("<sha256>  <version>/unstripped_corlib/<name>.dll", paths relative to lib/unity/)
# for what is in $out now; nothing when a file is missing.
current() {
  local n rel=()
  for n in "${names[@]}"; do
    [ -f "$out/$n.dll" ] || return 0
    rel+=("$unity/unstripped_corlib/$n.dll")
  done
  (cd "$repo/lib/unity" && sha256sum -- "${rel[@]}")
}
if [ -n "$listed" ] && [ "$(current | sort)" = "$(printf '%s\n' "$listed" | sort)" ]; then
  say "Already there: $out (${#names[@]} files, SHA-256 as in the manifest). Nothing to do."
  exit 0
fi

if [ ! -f "$archive" ]; then
  say "Will download: $url"
  say "           to: $archive (about 4.5 GB)"
else
  say "Archive: $archive (already downloaded)"
fi
say "Will extract: $profile/{$(IFS=,; printf '%s' "${names[*]}")}.dll"
say "         to: $out"
if [ "$dry_run" -eq 1 ]; then say "Dry run: nothing downloaded or written."; exit 0; fi

# What Unity publishes for this version: the Linux editor's URL and its MD5 ("md5-<base64>").
api="https://services.api.unity.com/unity/editor/release/v1/releases?version=$unity&limit=1"
published="$(curl -sS --fail "$api" | python3 -I -c '
import json, sys
for release in json.load(sys.stdin).get("results", []):
    for d in release.get("downloads", []):
        if d.get("platform") == "LINUX" and d.get("architecture") == "X86_64":
            print(d.get("url", ""), d.get("integrity", ""))
            sys.exit(0)
sys.exit(1)
')" || fail "Unity's release API gave no Linux editor for $unity ($api)"
api_url="${published%% *}"
api_md5="${published#* }"
[ "$api_url" = "$url" ] || fail "Unity's API names $api_url, not $url"
[[ "$api_md5" == md5-* ]] || fail "Unity's API gives no MD5 for $url (integrity: '$api_md5')"

mkdir -p "$dir"
if [ ! -f "$archive" ]; then
  curl -sS --fail -o "$archive.part" "$url" || fail "download failed: $url"
  mv -- "$archive.part" "$archive"
fi
actual_md5="md5-$(python3 -I -c '
import base64, hashlib, sys
h = hashlib.md5()
with open(sys.argv[1], "rb") as f:
    for block in iter(lambda: f.read(1 << 22), b""):
        h.update(block)
print(base64.b64encode(h.digest()).decode())
' "$archive")"
[ "$actual_md5" = "$api_md5" ] || fail "$archive: $actual_md5, Unity publishes $api_md5 (delete it to download it again)"
say "Archive checked: $actual_md5 as Unity publishes, SHA-256 $(sha256sum "$archive" | cut -d' ' -f1)"

work="$(mktemp -d "$dir/extract.XXXXXX")"
trap 'rm -rf -- "$work"' EXIT
members=()
for n in "${names[@]}"; do members+=("$profile/$n.dll"); done
say "Extracting (a full pass over the archive, a few minutes)..."
tar -xJf "$archive" -C "$work" -- "${members[@]}" || fail "tar could not extract every library from $archive"
for n in "${names[@]}"; do
  [ -f "$work/$profile/$n.dll" ] || fail "$profile/$n.dll is not in $archive"
done
if grep -a -q 'System.Native' "$work/$profile/mscorlib.dll"; then
  fail "$profile/mscorlib.dll calls System.Native: a Unix build, which a Windows game cannot run"
fi
rm -rf -- "$out"
mkdir -p "$out"
for n in "${names[@]}"; do mv -- "$work/$profile/$n.dll" "$out/$n.dll"; done
say "Extracted ${#names[@]} files into $out (no System.Native in mscorlib: the Windows build)."

now="$(current)"
if [ -z "$listed" ]; then
  say "Unity $unity is not in $manifest yet. Lines to add, once reviewed:"
  say "$now"
  exit 3
fi
[ "$(printf '%s\n' "$now" | sort)" = "$(printf '%s\n' "$listed" | sort)" ] || fail "the extracted files differ from $manifest:"$'\n'"$(diff <(printf '%s\n' "$listed" | sort) <(printf '%s\n' "$now" | sort) || true)"
say "SHA-256 of the ${#names[@]} files as in the manifest. Install them: tools/nodance-deploy.sh"
